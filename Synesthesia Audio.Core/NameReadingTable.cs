namespace Synesthesia_Audio.Core;

/// <summary>
/// Kanji-to-romaji name readings from EDRDG JMnedict (CC BY-SA 4.0), overlaid with a curated override file for
/// names where JMnedict's variant ordering is unreliable.
/// </summary>
public sealed class NameReadingTable
{
    private readonly Dictionary<string, string> _entries;
    private readonly int _maxKeyLength;

    private NameReadingTable(Dictionary<string, string> entries, int maxKeyLength)
    {
        _entries = entries;
        _maxKeyLength = maxKeyLength;
    }

    public static NameReadingTable Empty { get; } =
        new(new Dictionary<string, string>(StringComparer.Ordinal), 0);

    public int Count => _entries.Count;

    /// <summary>Bounds the greedy match loop.</summary>
    public int MaxKeyLength => _maxKeyLength;

    /// <summary>Later sources win, so overrides must be applied after the generated table.</summary>
    public static NameReadingTable FromLines(IEnumerable<string> lines, NameReadingTable? baseTable = null)
    {
        var entries = baseTable is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(baseTable._entries, StringComparer.Ordinal);

        var max = baseTable?._maxKeyLength ?? 0;

        foreach (var line in lines)
        {
            if (line.Length == 0 || line[0] == '#') continue;

            var tab = line.IndexOf('\t');
            if (tab <= 0) continue;

            var kanji = line[..tab].Trim();
            var romaji = line[(tab + 1)..].Trim();
            if (kanji.Length == 0 || romaji.Length == 0) continue;

            entries[kanji] = romaji;
            if (kanji.Length > max) max = kanji.Length;
        }

        return new NameReadingTable(entries, max);
    }

    /// <summary>Longest-first matters: 一ノ瀬 must beat 一.</summary>
    public bool TryMatchLongest(string text, int start, out int length, out string romaji)
    {
        length = 0;
        romaji = string.Empty;

        if (_maxKeyLength == 0) return false;

        var limit = Math.Min(_maxKeyLength, text.Length - start);
        for (var len = limit; len >= 1; len--)
        {
            var key = text.Substring(start, len);
            if (_entries.TryGetValue(key, out var found))
            {
                length = len;
                romaji = found;
                return true;
            }
        }

        return false;
    }

    public bool Contains(string kanji) => _entries.ContainsKey(kanji);
}
