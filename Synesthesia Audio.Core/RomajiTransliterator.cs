using System.Text;
using System.Text.RegularExpressions;

namespace Synesthesia_Audio.Core;

/// <summary>
/// Kana to romaji. This runs locally and is deliberately not a translation provider: voice actor and circle
/// names must be romanised here, never sent to a machine-translation API.
///
/// Beyond the plain syllable table it handles the three things a naive kana-by-kana walk gets wrong:
/// the sokuon (<c>ッ</c>, which doubles the next consonant, so ニッタ is "Nitta" not "Nita"), the
/// prolonged sound mark (<c>ー</c>, which repeats the previous vowel), and small-kana combos
/// (<c>ファ</c>, <c>ティ</c>, …) which are two characters but one syllable.
/// </summary>
public static class RomajiTransliterator
{
    private static readonly Regex JapaneseTextRegex =
        new(@"[\u3040-\u30ff\u3400-\u4dbf\u4e00-\u9fff]", RegexOptions.Compiled);

    private static readonly Dictionary<string, string> Map = BuildMap();

    private static Dictionary<string, string> BuildMap()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // Vowels
            { "あ", "a" }, { "い", "i" }, { "う", "u" }, { "え", "e" }, { "お", "o" },
            { "ア", "a" }, { "イ", "i" }, { "ウ", "u" }, { "エ", "e" }, { "オ", "o" },

            // K
            { "か", "ka" }, { "き", "ki" }, { "く", "ku" }, { "け", "ke" }, { "こ", "ko" },
            { "カ", "ka" }, { "キ", "ki" }, { "ク", "ku" }, { "ケ", "ke" }, { "コ", "ko" },

            // S
            { "さ", "sa" }, { "し", "shi" }, { "す", "su" }, { "せ", "se" }, { "そ", "so" },
            { "サ", "sa" }, { "シ", "shi" }, { "ス", "su" }, { "セ", "se" }, { "ソ", "so" },

            // T
            { "た", "ta" }, { "ち", "chi" }, { "つ", "tsu" }, { "て", "te" }, { "と", "to" },
            { "タ", "ta" }, { "チ", "chi" }, { "ツ", "tsu" }, { "テ", "te" }, { "ト", "to" },

            // N
            { "な", "na" }, { "に", "ni" }, { "ぬ", "nu" }, { "ね", "ne" }, { "の", "no" }, { "ん", "n" },
            { "ナ", "na" }, { "ニ", "ni" }, { "ヌ", "nu" }, { "ネ", "ne" }, { "ノ", "no" }, { "ン", "n" },

            // H
            { "は", "ha" }, { "ひ", "hi" }, { "ふ", "fu" }, { "へ", "he" }, { "ほ", "ho" },
            { "ハ", "ha" }, { "ヒ", "hi" }, { "フ", "fu" }, { "ヘ", "he" }, { "ホ", "ho" },

            // M
            { "ま", "ma" }, { "み", "mi" }, { "む", "mu" }, { "め", "me" }, { "も", "mo" },
            { "マ", "ma" }, { "ミ", "mi" }, { "ム", "mu" }, { "メ", "me" }, { "モ", "mo" },

            // Y
            { "や", "ya" }, { "ゆ", "yu" }, { "よ", "yo" },
            { "ヤ", "ya" }, { "ユ", "yu" }, { "ヨ", "yo" },

            // R
            { "ら", "ra" }, { "り", "ri" }, { "る", "ru" }, { "れ", "re" }, { "ろ", "ro" },
            { "ラ", "ra" }, { "リ", "ri" }, { "ル", "ru" }, { "レ", "re" }, { "ロ", "ro" },

            // W
            { "わ", "wa" }, { "を", "wo" }, { "ワ", "wa" }, { "ヲ", "wo" },

            // Voiced
            { "が", "ga" }, { "ぎ", "gi" }, { "ぐ", "gu" }, { "げ", "ge" }, { "ご", "go" },
            { "ガ", "ga" }, { "ギ", "gi" }, { "グ", "gu" }, { "ゲ", "ge" }, { "ゴ", "go" },
            { "ざ", "za" }, { "じ", "ji" }, { "ず", "zu" }, { "ぜ", "ze" }, { "ぞ", "zo" },
            { "ザ", "za" }, { "ジ", "ji" }, { "ズ", "zu" }, { "ゼ", "ze" }, { "ゾ", "zo" },
            { "だ", "da" }, { "ぢ", "ji" }, { "づ", "zu" }, { "で", "de" }, { "ど", "do" },
            { "ダ", "da" }, { "ヂ", "ji" }, { "ヅ", "zu" }, { "デ", "de" }, { "ド", "do" },
            { "ば", "ba" }, { "び", "bi" }, { "ぶ", "bu" }, { "べ", "be" }, { "ぼ", "bo" },
            { "バ", "ba" }, { "ビ", "bi" }, { "ブ", "bu" }, { "ベ", "be" }, { "ボ", "bo" },
            { "ぱ", "pa" }, { "ぴ", "pi" }, { "ぷ", "pu" }, { "ぺ", "pe" }, { "ぽ", "po" },
            { "パ", "pa" }, { "ピ", "pi" }, { "プ", "pu" }, { "ペ", "pe" }, { "ポ", "po" },
            { "ゔ", "vu" }, { "ヴ", "vu" },

            // Digraphs with small ya/yu/yo
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

            // Foreign-sound combos with small vowels (ファ, ティ, シェ, …)
            { "ふぁ", "fa" }, { "ふぃ", "fi" }, { "ふぇ", "fe" }, { "ふぉ", "fo" },
            { "ファ", "fa" }, { "フィ", "fi" }, { "フェ", "fe" }, { "フォ", "fo" },
            { "てぃ", "ti" }, { "てゅ", "tyu" }, { "でぃ", "di" }, { "でゅ", "dyu" },
            { "ティ", "ti" }, { "テュ", "tyu" }, { "ディ", "di" }, { "デュ", "dyu" },
            { "とぅ", "tu" }, { "どぅ", "du" }, { "トゥ", "tu" }, { "ドゥ", "du" },
            { "しぇ", "she" }, { "じぇ", "je" }, { "ちぇ", "che" },
            { "シェ", "she" }, { "ジェ", "je" }, { "チェ", "che" },
            { "うぃ", "wi" }, { "うぇ", "we" }, { "うぉ", "wo" },
            { "ウィ", "wi" }, { "ウェ", "we" }, { "ウォ", "wo" },
            { "ゔぁ", "va" }, { "ゔぃ", "vi" }, { "ゔぇ", "ve" }, { "ゔぉ", "vo" },
            { "ヴァ", "va" }, { "ヴィ", "vi" }, { "ヴェ", "ve" }, { "ヴォ", "vo" },
            { "つぁ", "tsa" }, { "つぃ", "tsi" }, { "つぇ", "tse" }, { "つぉ", "tso" },
            { "ツァ", "tsa" }, { "ツィ", "tsi" }, { "ツェ", "tse" }, { "ツォ", "tso" },
            { "くぁ", "kwa" }, { "くぃ", "kwi" }, { "くぇ", "kwe" }, { "くぉ", "kwo" },
            { "クァ", "kwa" }, { "クィ", "kwi" }, { "クェ", "kwe" }, { "クォ", "kwo" },
            { "ぐぁ", "gwa" }, { "ぐぃ", "gwi" }, { "ぐぇ", "gwe" }, { "ぐぉ", "gwo" },
            { "グァ", "gwa" }, { "グィ", "gwi" }, { "グェ", "gwe" }, { "グォ", "gwo" },
            { "すぃ", "si" }, { "ずぃ", "zi" }, { "スィ", "si" }, { "ズィ", "zi" },
            { "いぇ", "ye" }, { "イェ", "ye" },

            // A small kana that is not part of a combo still has a reading of its own.
            { "ぁ", "a" }, { "ぃ", "i" }, { "ぅ", "u" }, { "ぇ", "e" }, { "ぉ", "o" },
            { "ゃ", "ya" }, { "ゅ", "yu" }, { "ょ", "yo" }, { "ゎ", "wa" },
            { "ァ", "a" }, { "ィ", "i" }, { "ゥ", "u" }, { "ェ", "e" }, { "ォ", "o" },
            { "ャ", "ya" }, { "ュ", "yu" }, { "ョ", "yo" }, { "ヮ", "wa" }
        };

        return map;
    }

    public static string ToRomaji(string? japaneseText)
    {
        if (string.IsNullOrWhiteSpace(japaneseText)) return string.Empty;

        // Nothing Japanese (a name already in latin script): leave it exactly as written.
        if (!JapaneseTextRegex.IsMatch(japaneseText)) return japaneseText;

        var builder = new StringBuilder(japaneseText.Length);
        var doubleNextConsonant = false;
        var lastVowel = '\0';

        for (var index = 0; index < japaneseText.Length; index++)
        {
            var character = japaneseText[index];

            // Sokuon: the following consonant is doubled instead of the mark having a sound of its own.
            if (character is 'っ' or 'ッ')
            {
                doubleNextConsonant = true;
                continue;
            }

            // Prolonged sound mark: lengthen the vowel that came before it.
            if (character is 'ー' or 'ｰ' or '〜')
            {
                if (lastVowel != '\0') builder.Append(lastVowel);
                continue;
            }

            // The interpunct separates the halves of a joint credit, so it must not be swallowed.
            if (character is '・' or '･')
            {
                builder.Append(character);
                lastVowel = '\0';
                continue;
            }

            var romaji = Lookup(japaneseText, index, out var consumed);
            if (romaji is null)
            {
                // Kanji are dropped, as before; anything that is not Japanese is preserved.
                if (!JapaneseTextRegex.IsMatch(character.ToString())) builder.Append(character);
                lastVowel = '\0';
                continue;
            }

            if (doubleNextConsonant)
            {
                doubleNextConsonant = false;
                if (romaji.Length > 0 && !IsVowel(romaji[0]))
                {
                    // Hepburn spells the doubled chi/tsu with a t: マッチ is "matchi", not "macchi".
                    // Every other consonant simply doubles itself.
                    builder.Append(romaji.StartsWith("ch", StringComparison.Ordinal) ? 't' : romaji[0]);
                }
            }

            builder.Append(romaji);
            lastVowel = LastVowelOf(romaji);
            index += consumed - 1;
        }

        var result = builder.ToString().Trim();
        if (result.Length == 0) return string.Empty;

        return char.ToUpperInvariant(result[0]) + result[1..];
    }

    private static string? Lookup(string text, int index, out int consumed)
    {
        consumed = 1;

        if (index + 1 < text.Length && Map.TryGetValue(text.Substring(index, 2), out var pair))
        {
            consumed = 2;
            return pair;
        }

        return Map.TryGetValue(text[index].ToString(), out var single) ? single : null;
    }

    private static char LastVowelOf(string romaji)
    {
        for (var i = romaji.Length - 1; i >= 0; i--)
        {
            if (IsVowel(romaji[i])) return char.ToLowerInvariant(romaji[i]);
        }
        return '\0';
    }

    private static bool IsVowel(char c) =>
        c is 'a' or 'e' or 'i' or 'o' or 'u' or 'A' or 'E' or 'I' or 'O' or 'U';
}
