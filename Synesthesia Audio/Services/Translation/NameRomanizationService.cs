using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Synesthesia_Audio.Core;

namespace Synesthesia_Audio.Services.Translation;

/// <summary>
/// Loads the name table (bundled, or a refreshed copy in app data) and keeps it current. The data is EDRDG JMnedict,
/// whose licence forbids shipping stale data, so a refresh runs on first use. Every failure falls back to the bundled copy.
/// </summary>
public sealed class NameRomanizationService
{
    // A 404 (e.g. while the repository is private) just means "no update available".
    private const string MetaUrl = "https://github.com/yukiaddiction/Synesthesia-Audio/releases/download/names-data/name_readings.meta.json";
    private const string DataUrl = "https://github.com/yukiaddiction/Synesthesia-Audio/releases/download/names-data/name_readings.tsv.gz";

    // Detached signature over the manifest bytes; absent until a signing key is configured.
    private const string SignatureUrl = "https://github.com/yukiaddiction/Synesthesia-Audio/releases/download/names-data/name_readings.meta.json.sig";

    // When the package has no key, signatures are simply not enforced.
    private const string PublicKeyAsset = "names_pubkey.pem";

    private readonly ILogger<NameRomanizationService> _logger;
    private readonly ITranslationService _translationService;
    private readonly KawazuKanjiRomanizer _kawazu;
    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private Task<NameRomanizer>? _loadTask;
    private bool _refreshStarted;

    private ECDsa? _manifestPublicKey;
    private bool _publicKeyLoaded;

    /// <summary>
    /// Null means "signatures are not enforced yet" and must never be read as "verified".
    /// </summary>
    private async Task<ECDsa?> GetManifestPublicKeyAsync()
    {
        if (_publicKeyLoaded) return _manifestPublicKey;
        _publicKeyLoaded = true;

        try
        {
            await using var stream = await FileSystem.OpenAppPackageFileAsync(PublicKeyAsset).ConfigureAwait(false);
            using var reader = new StreamReader(stream);
            var pem = await reader.ReadToEndAsync().ConfigureAwait(false);

            _manifestPublicKey = NameDataSignature.TryLoadPublicKey(pem);
            if (_manifestPublicKey != null)
            {
                _logger.LogInformation("Name data manifest signatures are enforced");
            }
            else
            {
                _logger.LogWarning("The bundled name-data public key could not be parsed; manifest signatures are not enforced");
            }
        }
        catch (FileNotFoundException)
        {
            _logger.LogInformation("No name-data public key is bundled, so manifest signatures are not enforced yet");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Name-data public key could not be read; manifest signatures are not enforced");
        }

        return _manifestPublicKey;
    }

    private async Task<string?> TryDownloadSignatureAsync()
    {
        try
        {
            using var response = await _httpClient.GetAsync(SignatureUrl).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Name-data signature could not be downloaded");
            return null;
        }
    }

    public NameRomanizationService(ILogger<NameRomanizationService> logger, ITranslationService translationService, KawazuKanjiRomanizer kawazu, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _translationService = translationService;
        _kawazu = kawazu;
        _httpClient = httpClientFactory.CreateClient(HttpClients.NameData);
    }

    public string? BundledDataCreated { get; private set; }

    public async Task<string?> GetBundledDataCreatedAsync()
    {
        await GetRomanizerAsync().ConfigureAwait(false);
        return BundledDataCreated;
    }

    public async Task<string> RomanizeCastAsync(string rawCast)
    {
        var romanizer = await GetRomanizerAsync().ConfigureAwait(false);
        return romanizer.RomanizeCastList(rawCast);
    }

    public async Task<string> RomanizeNameAsync(string rawName)
    {
        var romanizer = await GetRomanizerAsync().ConfigureAwait(false);
        return romanizer.RomanizeName(rawName);
    }

    private async Task<NameRomanizer> GetRomanizerAsync()
    {
        Task<NameRomanizer> task;
        lock (_gate)
        {
            _loadTask ??= LoadAsync();
            task = _loadTask;
        }

        return await task.ConfigureAwait(false);
    }

    private static string CachedDataPath => Path.Combine(FileSystem.AppDataDirectory, "name_readings.tsv.gz");

    // Records which published revision the cached table came from, so it is not downloaded again.
    private static string AppliedMetaPath => Path.Combine(FileSystem.AppDataDirectory, "name_readings.applied.json");

    private string? _appliedCreated;

    private string? TryReadAppliedCreated()
    {
        try
        {
            if (!File.Exists(AppliedMetaPath)) return null;

            using var document = JsonDocument.Parse(File.ReadAllText(AppliedMetaPath));
            return document.RootElement.TryGetProperty("created", out var created) ? created.GetString() : null;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Applied name-data revision could not be read");
            return null;
        }
    }

    private void WriteAppliedCreated(string created)
    {
        try
        {
            var json = JsonSerializer.Serialize(new { created });
            var temporary = AppliedMetaPath + ".tmp";
            File.WriteAllText(temporary, json);
            File.Move(temporary, AppliedMetaPath, overwrite: true);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Applied name-data revision could not be recorded");
        }
    }

    private async Task<NameRomanizer> LoadAsync()
    {
        if (!_refreshStarted)
        {
            _refreshStarted = true;
            // Fire-and-forget: refreshing must never delay playback.
            _ = RefreshAsync();
        }

        NameReadingTable table;

        // Gzip decompression is synchronous, so keep it off the caller.
        var cached = await Task.Run(TryGetCachedTable).ConfigureAwait(false);
        table = cached ?? await LoadBundledTableAsync().ConfigureAwait(false);

        var meta = await ReadMetaAsync().ConfigureAwait(false);
        if (meta is { } bundledMeta && bundledMeta.TryGetProperty("created", out var created))
            BundledDataCreated = created.GetString();

        _appliedCreated = TryReadAppliedCreated();
        // The About page must show the revision actually in use, which for a refreshed table is the applied one.
        if (cached != null)
        {
            BundledDataCreated = _appliedCreated ?? BundledDataCreated;
        }
        else
        {
            _appliedCreated = null;
        }

        // Until the dictionary arrives the Kawazu fallback returns null and unknown kanji stay verbatim.
        _ = _kawazu.WarmUpAsync();

        try
        {
            var overrides = await ReadLinesAsync("name_overrides.tsv").ConfigureAwait(false);
            table = NameReadingTable.FromLines(overrides, table);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load name overrides");
        }

        return new NameRomanizer(table, _translationService.ToRomaji, _kawazu.TryRomanize);
    }

    private NameReadingTable? TryGetCachedTable()
    {
        try
        {
            if (!File.Exists(CachedDataPath)) return null;

            using var file = File.OpenRead(CachedDataPath);
            using var gzip = new GZipStream(file, CompressionMode.Decompress);
            var table = NameReadingTable.FromLines(ReadLines(gzip));
            _logger.LogInformation("Using refreshed name data — {Count} readings", table.Count);
            return table;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Refreshed name data was unreadable; falling back to the bundled copy");
            return null;
        }
    }

    private async Task<NameReadingTable> LoadBundledTableAsync()
    {
        try
        {
            var lines = await ReadLinesAsync("name_readings.tsv.gz").ConfigureAwait(false);
            var table = NameReadingTable.FromLines(lines);
            _logger.LogInformation("Bundled name data loaded — {Count} readings", table.Count);
            return table;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load the bundled name table; names fall back to kana only");
            return NameReadingTable.Empty;
        }
    }

    /// <summary>
    /// Downloads newer name data when the manifest advertises it. Optional by design: no network, a private repo or a
    /// bad hash all leave the current table in place.
    /// </summary>
    public async Task RefreshAsync()
    {
        try
        {
            using var metaResponse = await _httpClient.GetAsync(MetaUrl).ConfigureAwait(false);
            if (!metaResponse.IsSuccessStatusCode)
            {
                _logger.LogDebug("Name data refresh skipped — manifest returned HTTP {Status}", (int)metaResponse.StatusCode);
                return;
            }

            // Keep the exact bytes: the signature covers the manifest as published, not a re-serialised copy.
            var metaBytes = await metaResponse.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            var metaJson = Encoding.UTF8.GetString(metaBytes);

            // With a bundled key the manifest must prove its origin before its hashes are trusted; without one the
            // refresh falls back to hash-only checking.
            var publicKey = await GetManifestPublicKeyAsync().ConfigureAwait(false);
            if (publicKey != null)
            {
                var signature = await TryDownloadSignatureAsync().ConfigureAwait(false);
                if (!NameDataSignature.IsValid(publicKey, metaBytes, signature))
                {
                    _logger.LogWarning("Name data refresh rejected — the manifest signature is missing or invalid");
                    return;
                }
            }
            using var document = JsonDocument.Parse(metaJson);
            var root = document.RootElement;

            var publishedCreated = root.TryGetProperty("created", out var created) ? created.GetString() : null;
            var expectedSha = root.TryGetProperty("sha256", out var sha) ? sha.GetString() : null;
            if (string.IsNullOrEmpty(publishedCreated) || string.IsNullOrEmpty(expectedSha)) return;

            // Compare against the revision in use, never the bundled one.
            var appliedCreated = _appliedCreated ?? (_appliedCreated = TryReadAppliedCreated());
            if (!NameDataFreshness.IsNewRevision(publishedCreated, appliedCreated))
            {
                _logger.LogDebug("Name data already at published revision {Created}", publishedCreated);
                return;
            }

            _logger.LogInformation("Downloading name data published {Created}", publishedCreated);

            var bytes = await _httpClient.GetByteArrayAsync(DataUrl).ConfigureAwait(false);
            var actualSha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            if (!string.Equals(actualSha, expectedSha, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Name data download rejected — hash mismatch");
                return;
            }

            // Write then move, so an interrupted download cannot corrupt the cache.
            var temporary = CachedDataPath + ".tmp";
            await File.WriteAllBytesAsync(temporary, bytes).ConfigureAwait(false);
            File.Move(temporary, CachedDataPath, overwrite: true);

            WriteAppliedCreated(publishedCreated);
            _appliedCreated = publishedCreated;
            BundledDataCreated = publishedCreated;
            _logger.LogInformation("Name data refreshed to {Created}", publishedCreated);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Name data refresh failed; continuing with the bundled data");
        }
    }

    private async Task<List<string>> ReadLinesAsync(string assetName)
    {
        await using var stream = await FileSystem.OpenAppPackageFileAsync(assetName).ConfigureAwait(false);

        if (assetName.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
        {
            await using var gzip = new GZipStream(stream, CompressionMode.Decompress);
            return ReadLines(gzip);
        }

        return ReadLines(stream);
    }

    private static List<string> ReadLines(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var lines = new List<string>();
        while (reader.ReadLine() is { } line) lines.Add(line);
        return lines;
    }

    private async Task<JsonElement?> ReadMetaAsync()
    {
        try
        {
            await using var stream = await FileSystem.OpenAppPackageFileAsync("name_readings.meta.json");
            using var reader = new StreamReader(stream, Encoding.UTF8);
            using var document = JsonDocument.Parse(await reader.ReadToEndAsync());
            return document.RootElement.Clone();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not read name data metadata");
            return null;
        }
    }
}
