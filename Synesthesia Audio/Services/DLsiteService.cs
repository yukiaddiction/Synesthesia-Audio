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
using Synesthesia_Audio.Services.Translation;

namespace Synesthesia_Audio.Services;

public class DLsiteService
{
    /// <summary>
    /// Sent deliberately, and worth documenting rather than copying blindly (SEC-m-09): DLsite serves a reduced
    /// page to clients it does not recognise, and that page has no metadata in it. No login or access control is
    /// bypassed — the pages read here are the ones the user's own library already points at.
    /// </summary>
    public const string DefaultUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    /// <summary>
    /// The R-18 sections of DLsite gate on these flags; without them the site answers with the age-consent page,
    /// whose og:title is the bare site name. Acknowledging the gate once here is what makes the metadata rows
    /// reachable at all.
    /// </summary>
    private const string AgeGateCookie = "age_check_done=1; adultchecked=1; work_view_mode=2;";

    private readonly HttpClient _httpClient;
    private readonly TagMappingService _tagMappingService;
    private readonly ITranslationService _translationService;
    private readonly NameRomanizationService _nameRomanization;
    private readonly SettingsService _settingsService;
    private readonly IDialogService _dialog;
    private readonly ILogger<DLsiteService> _logger;

    /// <summary>
    /// Shared backoff for "the server is telling us to slow down" answers (PERF-04). Scraping a large library
    /// fans out several requests per work; without this a 429 was simply logged and the next work asked
    /// again immediately.
    /// </summary>
    private readonly RateLimiter _rateLimiter = new(
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(120));

    private static volatile bool _dlsiteUnreachableAlerted;
    private static volatile bool _nonProductPageWarned;
    private static readonly ConcurrentDictionary<string, Task> _englishTagFetches = new();

    public DLsiteService(TagMappingService tagMappingService, ITranslationService translationService, NameRomanizationService nameRomanization, SettingsService settingsService, IHttpClientFactory httpClientFactory, IDialogService dialogService, ILogger<DLsiteService> logger)
    {
        _tagMappingService = tagMappingService;
        _translationService = translationService;
        _nameRomanization = nameRomanization;
        _settingsService = settingsService;
        _dialog = dialogService;
        _logger = logger;
        // Headers, timeout and handler now come from the named client registration (DUP-02 / OVR-05).
        _httpClient = httpClientFactory.CreateClient(HttpClients.DLsite);
    }

    public static void ResetUnreachableAlert() => _dlsiteUnreachableAlerted = false;

    /// <summary>
    /// Called at the start of each scan so the detailed "not a product page" warning is emitted
    /// once per scan instead of once per work.
    /// </summary>
    public static void ResetScanDiagnostics() => _nonProductPageWarned = false;

    /// <summary>
    /// Trips the shared backoff when DLsite answers "slow down". Only 429 and 503 mean that; a 403 is a
    /// refusal and a 404 is a missing work, and neither gets better by waiting.
    /// </summary>
    private void NoteThrottling(HttpResponseMessage response)
    {
        var statusCode = (int)response.StatusCode;
        if (statusCode is not (429 or 503)) return;

        TimeSpan? retryAfter = response.Headers.RetryAfter is { } header
            ? RetryAfterParser.TryParse(header.Delta, header.Date, DateTimeOffset.UtcNow)
            : null;

        var delay = _rateLimiter.RecordRateLimit(DateTimeOffset.UtcNow, retryAfter);
        _logger.LogWarning(
            "DLsite answered {StatusCode}; pausing requests for {Seconds:0}s", statusCode, delay.TotalSeconds);
    }

    /// <summary>
    /// One compact line describing which fields a parsed page yielded, so a layout change on
    /// DLsite can be diagnosed from the log instead of guessed at.
    /// </summary>
    private static string DescribeFields(DLsiteParser.RawWorkPageData raw)
    {
        var summary =
            $"title={raw.TitleSource}({(raw.TitleFound ? "ok" : "no")}) " +
            $"circle={(raw.CircleFound ? "ok" : "no")} va={(raw.VaFound ? "ok" : "no")} " +
            $"genre={(raw.GenreFound ? "ok" : "no")} cover={(raw.CoverFound ? "ok" : "no")} " +
            $"jsonld={(raw.HasJsonLd ? "yes" : "no")} table={(raw.HasMetaTable ? "yes" : "no")} age={raw.AgeRating}";

        // Only spill the raw evidence when something is still missing, so normal scans stay quiet.
        if (!raw.VaFound || !raw.GenreFound)
        {
            summary += $" th=[{raw.TableLabels}]";
            if (!string.IsNullOrEmpty(raw.JsonldKeys))
                summary += $" keys=[{raw.JsonldKeys}]";
            if (!string.IsNullOrEmpty(raw.TitleRaw))
                summary += $" titleRaw='{raw.TitleRaw}'";
        }

        return summary;
    }

    public async Task<(string CoverUrl, string Title, string TranslatedTitle, string Circle, string VA, string AgeRating, string Genre)> GetWorkMetadataAsync(string rjCode)
    {
        // Still backing off from a 429/503: spend no request at all rather than earning another one.
        if (_rateLimiter.IsBlocked(DateTimeOffset.UtcNow))
        {
            _logger.LogDebug(
                "DLsite metadata for {RjCode} skipped — cooling down for another {Seconds:0}s",
                rjCode, _rateLimiter.Remaining(DateTimeOffset.UtcNow).TotalSeconds);
            return (string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, AgeRatings.Unknown, string.Empty);
        }

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
                NoteThrottling(response);
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
            NoteThrottling(response);
        }
        catch (Exception ex) { _logger.LogDebug(ex, "DLsite HTML fetch failed (eng) for {RjCode}", rjCode); }

        if (response == null || !response.IsSuccessStatusCode)
        {
            // The previous attempt's response is about to be forgotten; without this it leaked its socket
            // and buffered body on every fallback hop (BUG-m-23).
            response?.Dispose();

            url = $"https://www.dlsite.com/maniax/work/=/product_id/{rjCode}.html";
            try
            {
                using var cts2 = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(AppConstants.DlsiteHtmlTimeoutSeconds));
                response = await _httpClient.GetAsync(url, cts2.Token);
                NoteThrottling(response);
            }
            catch (Exception ex) { _logger.LogDebug(ex, "DLsite HTML fetch failed (maniax) for {RjCode}", rjCode); }
        }

        if (response == null || !response.IsSuccessStatusCode)
        {
            response?.Dispose();

            url = $"https://www.dlsite.com/home/work/=/product_id/{rjCode}.html";
            try
            {
                using var cts3 = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(AppConstants.DlsiteHtmlTimeoutSeconds));
                response = await _httpClient.GetAsync(url, cts3.Token);
                NoteThrottling(response);
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
                    await _dialog.AlertAsync("DLsite Unreachable",
                        "Could not reach dlsite.com to fetch work metadata.\n\n" +
                        "Possible causes:\n" +
                        "- DNS or VPN not configured to bypass region lock\n" +
                        "- No internet connection\n" +
                        "- DLsite server is down\n\n" +
                        "Works will appear with folder names only. Previously cached metadata will still be shown.",
                        "OK");
                });
            }

            // The final attempt's response is never read on this path, so release it here (BUG-m-23).
            response?.Dispose();
            return ("", "", "", "", "", AgeRatings.Unknown, "");
        }

        // Reached again only by a later successful fetch, so the outage alert can fire for a new outage.
        _dlsiteUnreachableAlerted = false;

        try
        {
            var statusCode = (int)response.StatusCode;
            NoteThrottling(response);
            var html = await response.Content.ReadAsStringAsync();

            // The body is fully buffered now, so release the response instead of leaving it to the GC: the
            // fallback chain allocated one HttpResponseMessage per attempt (BUG-m-23).
            response.Dispose();

            var raw = DLsiteParser.ParseWorkPageHtml(html, url);

            // The JSON endpoints gate on TitleFound; the HTML fallback did not, so a blocked or
            // consent page was stored as a work titled "DLsite" with every other field empty.
            if (!raw.TitleFound)
            {
                var detail = $"HTTP {statusCode}, <title>='{DLsiteParser.ExtractDocumentTitle(html)}', og:title='{raw.ParsedTitle}', {DescribeFields(raw)}";

                // One warning per scan; the rest stay at Information so the log stays readable.
                if (!_nonProductPageWarned)
                {
                    _nonProductPageWarned = true;
                    _logger.LogWarning(
                        "DLsite did not return a product page for {RjCode} ({Detail}) from {Url} - keeping folder metadata",
                        rjCode, detail, url);
                }
                else
                {
                    _logger.LogInformation("DLsite non-product page for {RjCode} ({Detail})", rjCode, detail);
                }

                return ("", "", "", "", "", AgeRatings.Unknown, "");
            }

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
            _logger.LogInformation("DLsite fields for {RjCode}: {Fields}", rjCode, DescribeFields(raw));
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
            NoteThrottling(response);
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
                    // Machine translation uploads the work's tags to a third-party service, so it only
                    // happens when the user has opted in. Locally mapped tags never reach this branch.
                    if (!_settingsService.CurrentSettings.TranslateDlsiteTags)
                    {
                        mapped.Add(english);
                        continue;
                    }

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
            work.Circle = await _nameRomanization.RomanizeNameAsync(work.Circle);
        }

        if (!string.IsNullOrEmpty(work.VA))
        {
            work.VA = await _nameRomanization.RomanizeCastAsync(work.VA);
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
