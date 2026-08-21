using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Synesthesia_Audio.Services;

public partial class LingvaTranslationService : ITranslationService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<LingvaTranslationService> _logger;
    private readonly string _baseUrl;

    public string? LastError { get; private set; }
    public void ClearError() => LastError = null;

    private static readonly Dictionary<string, string> KanaToRomaji = new()
    {
        { "あ", "a" }, { "い", "i" }, { "う", "u" }, { "え", "e" }, { "お", "o" },
        { "か", "ka" }, { "き", "ki" }, { "く", "ku" }, { "け", "ke" }, { "こ", "ko" },
        { "さ", "sa" }, { "し", "shi" }, { "す", "su" }, { "せ", "se" }, { "そ", "so" },
        { "た", "ta" }, { "ち", "chi" }, { "つ", "tsu" }, { "て", "te" }, { "と", "to" },
        { "な", "na" }, { "に", "ni" }, { "ぬ", "nu" }, { "ね", "ne" }, { "の", "no" },
        { "は", "ha" }, { "ひ", "hi" }, { "ふ", "fu" }, { "へ", "he" }, { "ほ", "ho" },
        { "ま", "ma" }, { "み", "mi" }, { "む", "mu" }, { "め", "me" }, { "も", "mo" },
        { "や", "ya" }, { "ゆ", "yu" }, { "よ", "yo" },
        { "ら", "ra" }, { "り", "ri" }, { "る", "ru" }, { "れ", "re" }, { "ろ", "ro" },
        { "わ", "wa" }, { "を", "wo" }, { "ん", "n" },
        { "が", "ga" }, { "ぎ", "gi" }, { "ぐ", "gu" }, { "げ", "ge" }, { "ご", "go" },
        { "ざ", "za" }, { "じ", "ji" }, { "ず", "zu" }, { "ぜ", "ze" }, { "ぞ", "zo" },
        { "だ", "da" }, { "ぢ", "ji" }, { "づ", "zu" }, { "で", "de" }, { "ど", "do" },
        { "ば", "ba" }, { "び", "bi" }, { "ぶ", "bu" }, { "べ", "be" }, { "ぼ", "bo" },
        { "ぱ", "pa" }, { "ぴ", "pi" }, { "ぷ", "pu" }, { "ぺ", "pe" }, { "ぽ", "po" },
        { "きゃ", "kya" }, { "きゅ", "kyu" }, { "きょ", "kyo" },
        { "しゃ", "sha" }, { "しゅ", "shu" }, { "しょ", "sho" },
        { "ちゃ", "cha" }, { "ちゅ", "chu" }, { "ちょ", "cho" },
        { "にゃ", "nya" }, { "にゅ", "nyu" }, { "にょ", "nyo" },
        { "ひゃ", "hya" }, { "ひゅ", "hyu" }, { "ひょ", "hyo" },
        { "みゃ", "mya" }, { "みゅ", "myu" }, { "みょ", "myo" },
        { "りゃ", "rya" }, { "りゅ", "ryu" }, { "りょ", "ryo" },
        { "ぎゃ", "gya" }, { "ぎゅ", "gyu" }, { "ぎょ", "gyo" },
        { "じゃ", "ja" }, { "じゅ", "ju" }, { "じょ", "jo" },
        { "びゃ", "bya" }, { "びゅ", "byu" }, { "びょ", "byo" },
        { "ぴゃ", "pya" }, { "ぴゅ", "pyu" }, { "ぴょ", "pyo" },
        { "ア", "a" }, { "イ", "i" }, { "ウ", "u" }, { "エ", "e" }, { "オ", "o" },
        { "カ", "ka" }, { "キ", "ki" }, { "ク", "ku" }, { "ケ", "ke" }, { "コ", "ko" },
        { "サ", "sa" }, { "シ", "shi" }, { "ス", "su" }, { "セ", "se" }, { "ソ", "so" },
        { "タ", "ta" }, { "チ", "chi" }, { "ツ", "tsu" }, { "テ", "te" }, { "ト", "to" },
        { "ナ", "na" }, { "ニ", "ni" }, { "ヌ", "nu" }, { "ネ", "ne" }, { "ノ", "no" },
        { "ハ", "ha" }, { "ヒ", "hi" }, { "フ", "fu" }, { "ヘ", "he" }, { "ホ", "ho" },
        { "マ", "ma" }, { "ミ", "mi" }, { "ム", "mu" }, { "メ", "me" }, { "モ", "mo" },
        { "ヤ", "ya" }, { "ユ", "yu" }, { "ヨ", "yo" },
        { "ラ", "ra" }, { "リ", "ri" }, { "ル", "ru" }, { "レ", "re" }, { "ロ", "ro" },
        { "ワ", "wa" }, { "ヲ", "wo" }, { "ン", "n" },
        { "ガ", "ga" }, { "ギ", "gi" }, { "グ", "gu" }, { "ゲ", "ge" }, { "ゴ", "go" },
        { "ザ", "za" }, { "ジ", "ji" }, { "ズ", "zu" }, { "ゼ", "ze" }, { "ゾ", "zo" },
        { "ダ", "da" }, { "ヂ", "ji" }, { "ヅ", "zu" }, { "デ", "de" }, { "ド", "do" },
        { "バ", "ba" }, { "ビ", "bi" }, { "ブ", "bu" }, { "ベ", "be" }, { "ボ", "bo" },
        { "パ", "pa" }, { "ピ", "pi" }, { "プ", "pu" }, { "ペ", "pe" }, { "ポ", "po" },
        { "キャ", "kya" }, { "キュ", "kyu" }, { "キョ", "kyo" },
        { "シャ", "sha" }, { "シュ", "shu" }, { "ショ", "sho" },
        { "チャ", "cha" }, { "チュ", "chu" }, { "チョ", "cho" },
        { "ニャ", "nya" }, { "ニュ", "nyu" }, { "ニョ", "nyo" },
        { "ヒャ", "hya" }, { "ヒュ", "hyu" }, { "ヒョ", "hyo" },
        { "ミャ", "mya" }, { "ミュ", "myu" }, { "ミョ", "myo" },
        { "リャ", "rya" }, { "リュ", "ryu" }, { "リョ", "ryo" },
        { "ギャ", "gya" }, { "ギュ", "gyu" }, { "ギョ", "gyo" },
        { "ジャ", "ja" }, { "ジュ", "ju" }, { "ジョ", "jo" },
        { "ビャ", "bya" }, { "ビュ", "byu" }, { "ビョ", "byo" },
        { "ピャ", "pya" }, { "ピュ", "pyu" }, { "ピョ", "pyo" },
        { "ッ", "" },
    };

    public LingvaTranslationService(HttpClient httpClient, ILogger<LingvaTranslationService> logger, string baseUrl = "https://lingva.ml")
    {
        _httpClient = httpClient;
        _logger = logger;
        _baseUrl = baseUrl.TrimEnd('/');
    }

    public bool ContainsJapanese(string text)
        => !string.IsNullOrEmpty(text) && AppConstants.JapaneseTextRegex.IsMatch(text);

    public async Task<string> TranslateJaToEnAsync(string japaneseText)
    {
        if (string.IsNullOrWhiteSpace(japaneseText)) return string.Empty;
        if (!ContainsJapanese(japaneseText)) return string.Empty;

        try
        {
            var result = await TranslateTextAsync(japaneseText);
            return result ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Translation failed for text");
            return string.Empty;
        }
    }

    public async Task<IReadOnlyList<string>> TranslateJaToEnBatchAsync(IReadOnlyList<string> japaneseTexts)
    {
        var results = new string[japaneseTexts.Count];
        var indicesToTranslate = new List<int>();
        var textsToTranslate = new List<string>();

        for (int i = 0; i < japaneseTexts.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(japaneseTexts[i]) && ContainsJapanese(japaneseTexts[i]))
            {
                indicesToTranslate.Add(i);
                textsToTranslate.Add(japaneseTexts[i]);
            }
        }

        if (textsToTranslate.Count == 0) return results;

        try
        {
            var combined = string.Join("\n", textsToTranslate);
            var translated = await TranslateTextAsync(combined);

            if (!string.IsNullOrEmpty(translated))
            {
                var lines = translated.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(l => l.Trim())
                    .ToList();

                if (lines.Count == textsToTranslate.Count)
                {
                    for (int i = 0; i < indicesToTranslate.Count; i++)
                        results[indicesToTranslate[i]] = lines[i];
                    return results;
                }
                _logger.LogDebug("Batch translation line count mismatch — expected {Expected}, got {Actual}, falling back to individual",
                    textsToTranslate.Count, lines.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Batch translation failed, falling back to individual translations");
        }

        for (int i = 0; i < indicesToTranslate.Count; i++)
            results[indicesToTranslate[i]] = await TranslateJaToEnAsync(textsToTranslate[i]);

        return results;
    }

    public string ToRomaji(string japaneseText)
    {
        if (string.IsNullOrWhiteSpace(japaneseText)) return string.Empty;
        if (!ContainsJapanese(japaneseText)) return japaneseText;

        var sb = new StringBuilder();
        int i = 0;
        while (i < japaneseText.Length)
        {
            if (i + 1 < japaneseText.Length)
            {
                var pair = japaneseText.Substring(i, 2);
                if (KanaToRomaji.TryGetValue(pair[0].ToString(), out _) || KanaToRomaji.ContainsKey(pair[0].ToString()))
                {
                    if (KanaToRomaji.TryGetValue(pair, out var romaji))
                    {
                        sb.Append(romaji);
                        i += 2;
                        continue;
                    }
                }
            }

            var c = japaneseText[i];
            if (KanaToRomaji.TryGetValue(c.ToString(), out var r))
            {
                sb.Append(r);
            }
            else if (!AppConstants.JapaneseTextRegex.IsMatch(c.ToString()))
            {
                sb.Append(c);
            }
            i++;
        }

        var result = sb.ToString().Trim();

        if (result.Length > 0)
            result = char.ToUpper(result[0]) + result.Substring(1);

        return result;
    }

    private async Task<string?> TranslateTextAsync(string text)
    {
        var result = await TryLingvaAsync(text);
        if (!string.IsNullOrEmpty(result)) return result;

        LastError = "Lingva is unavailable, trying MyMemory fallback...";
        _logger.LogInformation("Lingva primary down, trying MyMemory fallback");
        result = await TryMyMemoryAsync(text);
        if (!string.IsNullOrEmpty(result)) return result;

        LastError = "MyMemory is unavailable, trying Google Translate fallback...";
        _logger.LogInformation("MyMemory down, trying Google Translate fallback");
        result = await TryGoogleTranslateAsync(text);
        if (!string.IsNullOrEmpty(result)) return result;

        LastError = "Translation failed — Lingva, MyMemory, and Google Translate are unreachable. Check your internet connection.";
        _logger.LogWarning("All translation services unreachable for text of length {Length}", text.Length);
        return null;
    }

    private async Task<string?> TryLingvaAsync(string text)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(AppConstants.TranslationRequestTimeoutSeconds));
            var encoded = Uri.EscapeDataString(text);
            var url = $"https://lingva.ml/api/v1/ja/en/{encoded}";

            using var response = await _httpClient.GetAsync(url, cts.Token);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("translation", out var t))
                return System.Net.WebUtility.HtmlDecode(t.GetString() ?? "");
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "TryLingvaAsync failed");
        }
        return null;
    }

    private async Task<string?> TryMyMemoryAsync(string text)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(AppConstants.TranslationRequestTimeoutSeconds));
            var encoded = Uri.EscapeDataString(text);
            var url = $"https://api.mymemory.translated.net/get?q={encoded}&langpair=ja%7Cen";

            using var response = await _httpClient.GetAsync(url, cts.Token);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("responseData", out var rd) &&
                rd.TryGetProperty("translatedText", out var t))
                return System.Net.WebUtility.HtmlDecode(t.GetString() ?? "");
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "TryMyMemoryAsync failed");
        }
        return null;
    }

    private async Task<string?> TryGoogleTranslateAsync(string text)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(AppConstants.TranslationRequestTimeoutSeconds));
            var encoded = Uri.EscapeDataString(text);
            var url = $"https://translate.googleapis.com/translate_a/single?client=gtx&sl=ja&tl=en&dt=t&q={encoded}";

            using var response = await _httpClient.GetAsync(url, cts.Token);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind == System.Text.Json.JsonValueKind.Array && root.GetArrayLength() > 0)
            {
                var firstArray = root[0];
                if (firstArray.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    var sb = new StringBuilder();
                    foreach (var element in firstArray.EnumerateArray())
                    {
                        if (element.ValueKind == System.Text.Json.JsonValueKind.Array && element.GetArrayLength() > 0)
                        {
                            var part = element[0].GetString();
                            if (!string.IsNullOrEmpty(part))
                            {
                                sb.Append(part);
                            }
                        }
                    }
                    return sb.ToString();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "TryGoogleTranslateAsync failed");
        }
        return null;
    }
}
