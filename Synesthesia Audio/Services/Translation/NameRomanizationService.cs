using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Synesthesia_Audio.Core;

namespace Synesthesia_Audio.Services.Translation;

/// <summary>
/// Loads the name table (bundled copy, plus any refreshed copy from app data) and keeps it current.
/// <para>
/// Name data is EDRDG JMnedict, CC BY-SA 4.0, used in modified form. EDRDG treats shipping stale
/// data as a licence violation, so a refresh runs on first use: it fetches a small manifest, and
/// only when the published data is newer does it download the table, verify its SHA-256, and swap
/// it in atomically. Every failure is soft — the bundled copy is always the fallback.
/// </para>
/// </summary>
public sealed class NameRomanizationService
{
    // Points at this repository's release asset. While the repository is private this returns 404,
    // which is treated as "no update available"; making the repository public activates it with no
    // app rebuild.
    private const string MetaUrl = "https://github.com/yukiaddiction/Synesthesia-Audio/releases/download/names-data/name_readings.meta.json";
    private const string DataUrl = "https://github.com/yukiaddiction/Synesthesia-Audio/releases/download/names-data/name_readings.tsv.gz";

    private readonly ILogger<NameRomanizationService> _logger;
    private readonly ITranslationService _translationService;
    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(30) };
    private readonly SemaphoreSlim _gate = new(1, 1);

    private Task<NameRomanizer>? _loadTask;
    private bool _refreshStarted;

    public NameRomanizationService(ILogger<NameRomanizationService> logger, ITranslationService translationService)
    {
        _logger = logger;
        _translationService = translationService;
    }

    /// <summary>Creation date of the name data actually in use.</summary>
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

    private async Task<NameRomanizer> LoadAsync()
    {
        if (!_refreshStarted)
        {
            _refreshStarted = true;
            // Deliberately fire-and-forget: refreshing must never delay or block playback.
            _ = RefreshAsync();
        }

        NameReadingTable table;

        var cached = TryGetCachedTable();
        table = cached ?? await LoadBundledTableAsync().ConfigureAwait(false);

        var meta = await ReadMetaAsync().ConfigureAwait(false);
        if (meta is { } bundledMeta && bundledMeta.TryGetProperty("created", out var created))
            BundledDataCreated = created.GetString();

        try
        {
            var overrides = await ReadLinesAsync("name_overrides.tsv").ConfigureAwait(false);
            table = NameReadingTable.FromLines(overrides, table);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load name overrides");
        }

        return new NameRomanizer(table, _translationService.ToRomaji);
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
    /// Fetches the manifest and downloads newer data. Treated as completely optional: a private
    /// repository, no network, or a bad hash all simply leave the bundled copy in place.
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

            var metaJson = await metaResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
            using var document = JsonDocument.Parse(metaJson);
            var root = document.RootElement;

            var publishedCreated = root.TryGetProperty("created", out var created) ? created.GetString() : null;
            var expectedSha = root.TryGetProperty("sha256", out var sha) ? sha.GetString() : null;
            if (string.IsNullOrEmpty(publishedCreated) || string.IsNullOrEmpty(expectedSha)) return;

            if (File.Exists(CachedDataPath) && string.Equals(publishedCreated, BundledDataCreated, StringComparison.Ordinal))
                return;

            _logger.LogInformation("Downloading name data published {Created}", publishedCreated);

            var bytes = await _httpClient.GetByteArrayAsync(DataUrl).ConfigureAwait(false);
            var actualSha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            if (!string.Equals(actualSha, expectedSha, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Name data download rejected — hash mismatch");
                return;
            }

            // Write beside the target then move, so an interrupted download cannot corrupt the cache.
            var temporary = CachedDataPath + ".tmp";
            await File.WriteAllBytesAsync(temporary, bytes).ConfigureAwait(false);
            File.Move(temporary, CachedDataPath, overwrite: true);

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
