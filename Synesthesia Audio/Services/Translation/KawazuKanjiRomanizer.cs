using System.IO.Compression;
using Kawazu;
using Microsoft.Extensions.Logging;

namespace Synesthesia_Audio.Services.Translation;

/// <summary>
/// Optional last-resort reading for kanji the name table does not contain, using Kawazu (MeCab).
/// <para>
/// Kawazu needs an IPADIC dictionary on disk, which is ~50 MB — far too large to bundle in the APK
/// or commit to git — so it is fetched once, in the background, from a release asset and cached in
/// app data. Until it arrives this returns null and the romaniser keeps the kanji verbatim, so the
/// feature degrades to table-only rather than failing.
/// </para>
/// </summary>
public sealed class KawazuKanjiRomanizer
{
    private const string DictionaryZipUrl =
        "https://github.com/yukiaddiction/Synesthesia-Audio/releases/download/names-data/ipadic.zip";

    private readonly ILogger<KawazuKanjiRomanizer> _logger;
    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromMinutes(5) };
    private readonly SemaphoreSlim _gate = new(1, 1);

    private KawazuConverter? _converter;
    private bool _attempted;

    public KawazuKanjiRomanizer(ILogger<KawazuKanjiRomanizer> logger) => _logger = logger;

    public bool IsReady => _converter is not null;

    private static string DictionaryDirectory => Path.Combine(FileSystem.AppDataDirectory, "IpaDic");

    /// <summary>Downloads and prepares the dictionary once. Safe to call repeatedly.</summary>
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
                _logger.LogInformation("Fetching the Kawazu dictionary (one-time, ~50 MB)");
                var zipPath = Path.Combine(FileSystem.CacheDirectory, "ipadic.zip");
                var bytes = await _httpClient.GetByteArrayAsync(DictionaryZipUrl).ConfigureAwait(false);

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
            // Expected while the repository has no ipadic.zip asset published yet.
            _logger.LogInformation(ex, "Kawazu fallback unavailable; names fall back to the table");
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Romanises a kanji span, or returns null when the dictionary is absent.
    /// Kawazu's conversion is CPU-bound and completes synchronously, so blocking here is safe; the
    /// callers already run off the UI thread during scans.
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
