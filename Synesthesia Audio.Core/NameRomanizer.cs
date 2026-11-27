using System.Text;

namespace Synesthesia_Audio.Core;

/// <summary>
/// Romanises cast names: kanji via the name table, kana via the injected romaniser. Anything that cannot be
/// romanised is emitted verbatim, because dropping it turned 五十嵐 into "" and 小鳥遊まこ into "mako".
/// </summary>
public sealed class NameRomanizer
{
    private readonly NameReadingTable _table;
    private readonly Func<string, string> _kanaToRomaji;
    private readonly Func<string, string?>? _unknownKanjiFallback;

    /// <param name="unknownKanjiFallback">
    /// Optional last resort (e.g. a MeCab/Kawazu reading); returning null keeps the kanji verbatim.
    /// </param>
    public NameRomanizer(NameReadingTable table, Func<string, string> kanaToRomaji, Func<string, string?>? unknownKanjiFallback = null)
    {
        _table = table;
        _kanaToRomaji = kanaToRomaji;
        _unknownKanjiFallback = unknownKanjiFallback;
    }

    public string RomanizeCastList(string rawCast)
    {
        if (string.IsNullOrWhiteSpace(rawCast)) return string.Empty;

        var parts = rawCast.Split(new[] { ',', '/', '、', '，' }, StringSplitOptions.RemoveEmptyEntries);
        var names = new List<string>(parts.Length);

        foreach (var part in parts)
        {
            var romanized = RomanizeName(part);
            if (romanized.Length > 0) names.Add(romanized);
        }

        return string.Join(", ", names);
    }

    public string RomanizeName(string rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName)) return string.Empty;

        var name = StripCreditPrefix(rawName.Trim());
        if (name.Length == 0) return string.Empty;

        var words = new List<string>();
        foreach (var word in name.Split(new[] { ' ', '\u3000', '\t' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var romanized = RomanizeWord(word);
            if (romanized.Length > 0) words.Add(ToTitleCase(romanized));
        }

        return string.Join(" ", words);
    }

    public static string StripCreditPrefix(string text)
    {
        var trimmed = text.Trim();

        foreach (var prefix in new[] { "CV:", "CV：", "CV.", "CV ", "cv:", "cv." })
        {
            if (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed[prefix.Length..].Trim();
                break;
            }
        }

        return trimmed;
    }

    private string RomanizeWord(string word)
    {
        var builder = new StringBuilder(word.Length * 2);
        var kanaRun = new StringBuilder();
        var emittedKanji = false;

        void FlushKana(bool atEnd)
        {
            if (kanaRun.Length == 0) return;

            var kana = _kanaToRomaji(kanaRun.ToString());
            kanaRun.Clear();
            if (kana.Length == 0) return;

            // Japanese names frequently omit the space, so a kanji prefix followed by trailing kana
            // is the surname/given boundary (小鳥遊まこ -> Takanashi Mako). Kana in the middle of a
            // name stays joined (一ノ瀬 is one surname, not "Ichi No Se").
            if (atEnd && emittedKanji) builder.Append(' ');

            builder.Append(kana);
        }

        var i = 0;
        while (i < word.Length)
        {
            var c = word[i];

            if (IsKanji(c))
            {
                FlushKana(false);

                if (_table.TryMatchLongest(word, i, out var length, out var romaji))
                {
                    builder.Append(romaji);
                    emittedKanji = true;
                    i += length;
                }
                else
                {
                    // Hand the whole unknown run to the fallback at once so it sees the word in context,
                    // stopping early where the table knows a later kanji.
                    var runStart = i;
                    while (i < word.Length && IsKanji(word[i]))
                    {
                        if (i > runStart && _table.TryMatchLongest(word, i, out _, out _)) break;
                        i++;
                    }

                    var run = word[runStart..i];
                    var fallback = _unknownKanjiFallback?.Invoke(run);
                    builder.Append(string.IsNullOrWhiteSpace(fallback) ? run : fallback);
                    emittedKanji = true;
                }
            }
            else if (IsKana(c))
            {
                kanaRun.Append(c);
                i++;
            }
            else
            {
                FlushKana(false);
                builder.Append(c);
                i++;
            }
        }

        FlushKana(true);
        return builder.ToString();
    }

    // CJK Unified Ideographs, then Extension A.
    private static bool IsKanji(char c) => c is >= '\u4e00' and <= '\u9fff' or >= '\u3400' and <= '\u4dbf';

    // Hiragana, Katakana, Katakana Phonetic Extensions.
    private static bool IsKana(char c) =>
        c is >= '\u3040' and <= '\u309f' or >= '\u30a0' and <= '\u30ff' or >= '\u31f0' and <= '\u31ff';

    private static string ToTitleCase(string value)
    {
        if (value.Length == 0) return value;

        var builder = new StringBuilder(value.Length);
        var atWordStart = true;

        foreach (var c in value)
        {
            if (char.IsLetter(c))
            {
                // Every word starts upper-case because a surname/given split may have been inserted
                // inside what was originally one token.
                builder.Append(atWordStart ? char.ToUpperInvariant(c) : char.ToLowerInvariant(c));
                atWordStart = false;
            }
            else
            {
                builder.Append(c);
                if (c is ' ' or '-' or '\'') atWordStart = true;
            }
        }

        return builder.ToString();
    }
}
