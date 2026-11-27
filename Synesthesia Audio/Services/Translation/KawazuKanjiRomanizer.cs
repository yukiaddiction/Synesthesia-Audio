using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Kawazu;
using Microsoft.Extensions.Logging;

namespace Synesthesia_Audio.Services.Translation;

/// <summary>
/// Last-resort MeCab reading for kanji missing from the name table. The ~50 MB IPADIC dictionary is too big to
/// bundle, so it is downloaded once in the background; until then this returns null and kanji stay verbatim.
/// </summary>
public sealed class KawazuKanjiRomanizer
{
    private const string DictionaryZipUrl =
        "https://github.com/yukiaddiction/Synesthesia-Audio/releases/download/names-data/ipadic.zip";

    // Publishes the zip's digest; the only way to verify the download.
    private const string DictionaryMetaUrl =
        "https://github.com/yukiaddiction/Synesthesia-Audio/releases/download/names-data/name_readings.meta.json";

    private readonly ILogger<KawazuKanjiRomanizer> _logger;
    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private KawazuConverter? _converter;
    private bool _attempted;

    public KawazuKanjiRomanizer(ILogger<KawazuKanjiRomanizer> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient(HttpClients.NameData);
    }

    public bool IsReady => _converter is not null;

    private static string DictionaryDirectory => Path.Combine(FileSystem.AppDataDirectory, "IpaDic");

    public async Task WarmUpAsync()
    {
        if (_converter is not null || _attempted) return;

        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_converter is not null || _attempted) return;
            _attempted = true;

            if (!File.Exists(Path.Combine(DictionaryDirectory, "sys.dic")))
            {
                // Without a published digest there is nothing to verify against, so refuse the download.
                var expectedHash = await TryReadPublishedDictionaryHashAsync().ConfigureAwait(false);
                if (expectedHash is null)
                {
                    _logger.LogWarning("Kawazu dictionary not fetched — no published digest to verify it against");
                    return;
                }

                _logger.LogInformation("Fetching the Kawazu dictionary (one-time, ~50 MB)");
                var zipPath = Path.Combine(FileSystem.CacheDirectory, "ipadic.zip");
                var bytes = await _httpClient.GetByteArrayAsync(DictionaryZipUrl).ConfigureAwait(false);

                var actualHash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
                if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("Kawazu dictionary rejected — digest mismatch (expected {Expected}, got {Actual})",
                        expectedHash, actualHash);
                    return;
                }

                Directory.CreateDirectory(DictionaryDirectory);
                await File.WriteAllBytesAsync(zipPath, bytes).ConfigureAwait(false);
                ZipFile.ExtractToDirectory(zipPath, DictionaryDirectory, true);
                File.Delete(zipPath);
            }

            _converter = new KawazuConverter(DictionaryDirectory);
            _logger.LogInformation("Kawazu fallback ready");
        }
        catch (Exception ex)
        {
            // Expected while no ipadic.zip release asset is published.
            _logger.LogInformation(ex, "Kawazu fallback unavailable; names fall back to the table");
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<string?> TryReadPublishedDictionaryHashAsync()
    {
        try
        {
            using var response = await _httpClient.GetAsync(DictionaryMetaUrl).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Dictionary digest unavailable (HTTP {Status})", (int)response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.TryGetProperty("ipadicSha256", out var hash))
            {
                var value = hash.GetString();
                if (!string.IsNullOrWhiteSpace(value)) return value.Trim().ToLowerInvariant();
            }

            _logger.LogInformation("Published name-data meta does not carry an ipadicSha256 digest yet");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not read the published dictionary digest");
            return null;
        }
    }

    /// <summary>
    /// Blocking on the result is safe: Kawazu completes synchronously and callers already run off the UI thread.
    /// </summary>
    public string? TryRomanize(string kanjiSpan)
    {
        if (_converter is null || string.IsNullOrWhiteSpace(kanjiSpan)) return null;

        try
        {
            var romaji = _converter
                .Convert(kanjiSpan, To.Romaji, Mode.Normal, RomajiSystem.Hepburn, string.Empty, string.Empty)
                .GetAwaiter().GetResult();

            return string.IsNullOrWhiteSpace(romaji) ? null : romaji;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Kawazu could not romanise '{Span}'", kanjiSpan);
            return null;
        }
    }
}
