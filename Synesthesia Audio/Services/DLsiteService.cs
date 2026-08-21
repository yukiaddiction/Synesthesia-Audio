using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls;
using Synesthesia_Audio.Core;
using Synesthesia_Audio.Models;

namespace Synesthesia_Audio.Services;

public class DLsiteService
{
    public const string DefaultUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    private readonly HttpClient _httpClient;
    private readonly TagMappingService _tagMappingService;
    private readonly ITranslationService _translationService;
    private readonly ILogger<DLsiteService> _logger;

    private static volatile bool _dlsiteUnreachableAlerted;
    private static readonly ConcurrentDictionary<string, Task> _englishTagFetches = new();

    public DLsiteService(TagMappingService tagMappingService, ITranslationService translationService, ILogger<DLsiteService> logger)
    {
        _tagMappingService = tagMappingService;
        _translationService = translationService;
        _logger = logger;
        var handler = new SocketsHttpHandler { MaxConnectionsPerServer = 16 };
        _httpClient = new HttpClient(handler);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", DefaultUserAgent);
        _httpClient.DefaultRequestHeaders.Add("Accept-Language", "ja,en-US;q=0.9,en;q=0.8");
        _httpClient.DefaultRequestHeaders.Add("Cookie", "adultchecked=1; work_view_mode=2;");
        _httpClient.Timeout = TimeSpan.FromSeconds(AppConstants.DlsiteClientTimeoutSeconds);
    }

    public static void ResetUnreachableAlert() => _dlsiteUnreachableAlerted = false;

    public async Task<(string CoverUrl, string Title, string TranslatedTitle, string Circle, string VA, string AgeRating, string Genre)> GetWorkMetadataAsync(string rjCode)
    {
        _logger.LogDebug("Fetching DLsite metadata for {RjCode}", rjCode);
        string[] apiUrls = new[]
        {
            $"https://www.dlsite.com/maniax/api/=/product.json?work_id={rjCode}",
            $"https://www.dlsite.com/home/api/=/product.json?work_id={rjCode}",
            $"https://www.dlsite.com/eng/api/=/product.json?work_id={rjCode}",
        };

        using var apiCts = new CancellationTokenSource(TimeSpan.FromSeconds(AppConstants.DlsiteApiTimeoutSeconds));
        var apiTasks = apiUrls.Select(async url =>
        {
            try
            {
                using var reqCts = CancellationTokenSource.CreateLinkedTokenSource(apiCts.Token);
                using var response = await _httpClient.GetAsync(url, reqCts.Token);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var raw = DLsiteParser.ParseProductJson(json, rjCode);
                    if (raw.TitleFound)
                        return (Success: true, raw: raw);
                }
            }
            catch (Exception ex) { _logger.LogDebug(ex, "DLsite API fetch failed for {Url}", url); }
            return (Success: false, raw: default(DLsiteParser.RawWorkPageData)!);
        }).ToList();

        while (apiTasks.Count > 0)
        {
            var done = await Task.WhenAny(apiTasks);
            apiTasks.Remove(done);
            var (success, raw) = await done;
            if (success)
            {
                apiCts.Cancel();
                _dlsiteUnreachableAlerted = false;
                _logger.LogDebug("DLsite API metadata fetched for {RjCode}", rjCode);
                var mappedGenres = await ResolveAndTranslateGenresAsync(raw.Genre, rjCode);
                return (raw.CoverUrl, raw.ParsedTitle, "", raw.Circle, raw.VaRaw, raw.AgeRating, mappedGenres);
            }
        }

        string url = $"https://www.dlsite.com/eng/work/=/product_id/{rjCode}.html";
        HttpResponseMessage? response = null;
        try
        {
            using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(AppConstants.DlsiteHtmlTimeoutSeconds));
            response = await _httpClient.GetAsync(url, cts.Token);
        }
        catch (Exception ex) { _logger.LogDebug(ex, "DLsite HTML fetch failed (eng) for {RjCode}", rjCode); }

        if (response == null || !response.IsSuccessStatusCode)
        {
            url = $"https://www.dlsite.com/maniax/work/=/product_id/{rjCode}.html";
            try
            {
                using var cts2 = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(AppConstants.DlsiteHtmlTimeoutSeconds));
                response = await _httpClient.GetAsync(url, cts2.Token);
            }
            catch (Exception ex) { _logger.LogDebug(ex, "DLsite HTML fetch failed (maniax) for {RjCode}", rjCode); }
        }

        if (response == null || !response.IsSuccessStatusCode)
        {
            url = $"https://www.dlsite.com/home/work/=/product_id/{rjCode}.html";
            try
            {
                using var cts3 = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(AppConstants.DlsiteHtmlTimeoutSeconds));
                response = await _httpClient.GetAsync(url, cts3.Token);
            }
            catch (Exception ex) { _logger.LogDebug(ex, "DLsite HTML fetch failed (home) for {RjCode}", rjCode); }
        }

        if (response == null || !response.IsSuccessStatusCode)
        {
            if (!_dlsiteUnreachableAlerted)
            {
                _dlsiteUnreachableAlerted = true;
                _logger.LogWarning("DLsite unreachable — all API and HTML endpoints failed for {RjCode} (alert shown)", rjCode);
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    var shell = Shell.Current;
                    if (shell == null) return;
                    await shell.DisplayAlert("DLsite Unreachable",
                        "Could not reach dlsite.com to fetch work metadata.\n\n" +
                        "Possible causes:\n" +
                        "- DNS or VPN not configured to bypass region lock\n" +
                        "- No internet connection\n" +
                        "- DLsite server is down\n\n" +
                        "Works will appear with folder names only. Previously cached metadata will still be shown.",
                        "OK");
                });
            }
            return ("", "", "", "", "", "SFW", "");
        }

        _dlsiteUnreachableAlerted = false;

        try
        {
            var html = await response.Content.ReadAsStringAsync();
            var raw = DLsiteParser.ParseWorkPageHtml(html, url);

            string title;
            string translatedTitle;

            if (raw.IsEngPage && !string.IsNullOrEmpty(raw.OriginalTitleRaw))
            {
                title = raw.OriginalTitleRaw;
                translatedTitle = raw.ParsedTitle;
            }
            else
            {
                title = raw.ParsedTitle;
                translatedTitle = string.Empty;
            }

            var mappedGenres = await ResolveAndTranslateGenresAsync(raw.Genre, rjCode);
            _logger.LogDebug("DLsite HTML parse success for {RjCode}", rjCode);
            return (raw.CoverUrl, title, translatedTitle, raw.Circle, raw.VaRaw, raw.AgeRating, mappedGenres);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DLsite HTML parsing fallback failed for {RjCode}", rjCode);
        }
        _logger.LogWarning("DLsite all attempts failed for {RjCode}", rjCode);
        return ("", "", "", "", "", "SFW", "");
    }

    private async Task FetchAndMapEnglishTagsAsync(string rjCode)
    {
        try
        {
            await _englishTagFetches.GetOrAdd(rjCode, _ => DoFetchAndMapEnglishTagsAsync(rjCode));
        }
        finally
        {
            _englishTagFetches.TryRemove(rjCode, out _);
        }
    }

    private async Task DoFetchAndMapEnglishTagsAsync(string rjCode)
    {
        _logger.LogDebug("Fetching English tag mappings for {RjCode}", rjCode);
        string jsonUrl = $"https://www.dlsite.com/maniax/api/=/product.json?work_id={rjCode}&locale=en_US";
        try
        {
            using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(AppConstants.DlsiteTagFetchTimeoutSeconds));
            using var response = await _httpClient.GetAsync(jsonUrl, cts.Token);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var root = doc.RootElement;
                System.Text.Json.JsonElement element;

                if (root.ValueKind == System.Text.Json.JsonValueKind.Array && root.GetArrayLength() > 0)
                {
                    element = root[0];
                }
                else if (root.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    element = root;
                }
                else
                {
                    return;
                }

                if (element.TryGetProperty("genres", out var genresProp) && genresProp.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    foreach (var genreItem in genresProp.EnumerateArray())
                    {
                        if (genreItem.TryGetProperty("name", out var engNameProp) && 
                            genreItem.TryGetProperty("name_base", out var jpNameProp))
                        {
                            var engTag = engNameProp.GetString()?.Trim();
                            var jpTag = jpNameProp.GetString()?.Trim();
                            if (!string.IsNullOrEmpty(jpTag) && !string.IsNullOrEmpty(engTag) && 
                                !string.Equals(jpTag, engTag, StringComparison.OrdinalIgnoreCase))
                            {
                                _tagMappingService.AddOrUpdateMapping(jpTag, engTag);
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "FetchAndMapEnglishTagsAsync failed for {RjCode}", rjCode);
        }
    }

    private async Task<string> ResolveAndTranslateGenresAsync(string rawGenres, string rjCode)
    {
        if (string.IsNullOrWhiteSpace(rawGenres)) return string.Empty;
        var tags = rawGenres.Split(new[] { ',', '/' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(t => t.Trim())
                            .Where(t => !string.IsNullOrEmpty(t))
                            .ToList();
        
        var mapped = new List<string>();
        bool fetchedEnglishTags = false;

        foreach (var tag in tags)
        {
            var english = _tagMappingService.MapTag(tag);
            if (english.Equals(tag, StringComparison.OrdinalIgnoreCase) && AppConstants.JapaneseTextRegex.IsMatch(tag))
            {
                if (!fetchedEnglishTags)
                {
                    fetchedEnglishTags = true;
                    await FetchAndMapEnglishTagsAsync(rjCode);
                    english = _tagMappingService.MapTag(tag);
                }

                if (english.Equals(tag, StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        var translated = await TranslateTrackTitleAsync(tag);
                        if (!string.IsNullOrEmpty(translated) && !translated.Equals(tag, StringComparison.OrdinalIgnoreCase))
                        {
                            _tagMappingService.AddOrUpdateMapping(tag, translated);
                            mapped.Add(translated);
                            continue;
                        }
                    }
                    catch (Exception ex) { _logger.LogDebug(ex, "DLsite tag translation fallback failed for '{Tag}'", tag); }
                }
            }
            mapped.Add(english);
        }
        return string.Join(", ", mapped.Distinct());
    }

    public async Task TranslateWorkAsync(WorkMetadata work)
    {
        if (work == null) return;

        if (string.IsNullOrEmpty(work.TranslatedTitle) && !string.IsNullOrEmpty(work.Title))
        {
            if (AppConstants.JapaneseTextRegex.IsMatch(work.Title))
            {
                work.TranslatedTitle = await TranslateTrackTitleAsync(work.Title);
            }
        }

        if (!string.IsNullOrEmpty(work.Circle))
        {
            work.Circle = TransliterateToRomaji(work.Circle);
        }

        if (!string.IsNullOrEmpty(work.VA))
        {
            work.VA = TranslateVaNames(work.VA);
        }

        await TranslateWorkTracksAsync(work);
    }

    public async Task TranslateWorkTracksAsync(WorkMetadata work)
    {
        var tracksToTranslate = new List<Track>();
        foreach (var sa in work.SubAlbums)
        {
            foreach (var t in sa.Tracks)
            {
                if (string.IsNullOrEmpty(t.TranslatedTitle) && AppConstants.JapaneseTextRegex.IsMatch(t.Title))
                {
                    tracksToTranslate.Add(t);
                }
            }
        }

        if (tracksToTranslate.Any())
        {
            var titles = tracksToTranslate.Select(t => t.Title).ToList();
            var translations = await TranslateTrackTitlesAsync(titles);
            for (int i = 0; i < tracksToTranslate.Count; i++)
            {
                tracksToTranslate[i].TranslatedTitle = translations[i];
            }
        }
    }

    private string TranslateVaNames(string vaText)
    {
        if (string.IsNullOrWhiteSpace(vaText)) return string.Empty;

        var names = vaText.Split(new[] { ',', '/' }, StringSplitOptions.RemoveEmptyEntries)
                          .Select(n => n.Trim())
                          .ToList();

        var translatedNames = new List<string>();
        foreach (var name in names)
        {
            translatedNames.Add(TransliterateToRomaji(name));
        }

        return string.Join(", ", translatedNames);
    }

    private string TransliterateToRomaji(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        if (!AppConstants.JapaneseTextRegex.IsMatch(text)) return text;
        return _translationService.ToRomaji(text);
    }

    public async Task<string> TranslateTrackTitleAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        if (!AppConstants.JapaneseTextRegex.IsMatch(text)) return string.Empty;
        return await _translationService.TranslateJaToEnAsync(text);
    }

    public async Task<List<string>> TranslateTrackTitlesAsync(List<string> texts)
    {
        var textsToTranslate = texts
            .Where(t => !string.IsNullOrWhiteSpace(t) && AppConstants.JapaneseTextRegex.IsMatch(t))
            .ToList();

        if (textsToTranslate.Count == 0)
            return texts.Select(_ => string.Empty).ToList();

        var translated = await _translationService.TranslateJaToEnBatchAsync(textsToTranslate);

        var transDict = new Dictionary<string, string>();
        for (int i = 0; i < textsToTranslate.Count; i++)
            transDict[textsToTranslate[i]] = translated[i];

        return texts.Select(t => transDict.TryGetValue(t, out var tr) ? tr : string.Empty).ToList();
    }
}
