using System.Text;

namespace Synesthesia_Audio.Core;

/// <summary>
/// Turns a cast/credit string into readable romaji. Kanji is resolved through the name table and
/// katakana/hiragana through the injected kana romaniser.
/// <para>
/// The governing rule is <b>never drop characters</b>. The previous implementation silently deleted
/// any kanji it did not recognise, which turned 五十嵐 into an empty string and 小鳥遊まこ into
/// "mako". Anything that cannot be romanised is therefore emitted verbatim instead.
/// </para>
/// </summary>
public sealed class NameRomanizer
{
    private readonly NameReadingTable _table;
    private readonly Func<string, string> _kanaToRomaji;
    private readonly Func<string, string?>? _unknownKanjiFallback;

    /// <param name="unknownKanjiFallback">
    /// Optional last resort for kanji the table does not know, such as a MeCab/Kawazu reading.
    /// Returning null leaves the kanji verbatim, which is always preferable to dropping it.
    /// </param>
    public NameRomanizer(NameReadingTable table, Func<string, string> kanaToRomaji, Func<string, string?>? unknownKanjiFallback = null)
    {
        _table = table;
        _kanaToRomaji = kanaToRomaji;
        _unknownKanjiFallback = unknownKanjiFallback;
    }

    /// <summary>Splits a cast list on the separators DLsite uses and romanises each entry.</summary>
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

    /// <summary>Romanises one name, preserving any spacing the source already had.</summary>
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

    /// <summary>Strips "CV:", "CV.", "CV：" and "CV " markers that DLsite sometimes prefixes.</summary>
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
                    // Consume the whole unknown kanji run, then try the optional fallback once with
                    // the full span so a parser sees the word in context rather than character by
                    // character. If it yields nothing, the kanji is kept verbatim — losing the
                    // character is worse than showing it unromanised.
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

    private static bool IsKanji(char c) => c is >= '\u4e00' and <= '\u9fff' or >= '\u3400' and <= '\u4dbf';

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
                // Capitalise the start of every word, because a surname/given split can be inserted
                // inside what was originally a single token.
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
