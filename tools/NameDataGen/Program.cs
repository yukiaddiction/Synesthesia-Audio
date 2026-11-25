using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace NameDataGen;

/// <summary>
/// Builds the compact kanji -> romaji name table the app ships, from the EDRDG JMnedict XML.
///
/// The file is parsed line by line rather than with XmlReader: JMnedict's DOCTYPE declares entity
/// references (&amp;surname; etc.) that the runtime XML parser rejects, and the generated file is
/// strictly one element per line, so a scanner is both simpler and far faster than entity juggling.
///
/// JMnedict has no frequency data and stores one variant reading per entry, so a name like 五十嵐
/// appears as several entries (いかざき / いからし / ...). Picking the right variant is the hard part;
/// this tool applies a documented heuristic and leaves genuine ties to the hand-curated override
/// file, which wins at runtime.
/// </summary>
internal static class Program
{
    private sealed record Candidate(string Romaji, int Order, int Score);

    private static int Main(string[] args)
    {
        var input = GetArg(args, "--input");
        var output = GetArg(args, "--output");
        var meta = GetArg(args, "--meta");
        var created = GetArg(args, "--created");

        if (input is null || output is null)
        {
            Console.Error.WriteLine("usage: NameDataGen --input <JMnedict.xml> --output <name_readings.tsv.gz> [--meta <meta.json>] [--created yyyy-MM-dd]");
            return 2;
        }

        var createdDate = created
            ?? File.GetLastWriteTimeUtc(input).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        var byKanji = new Dictionary<string, List<Candidate>>(StringComparer.Ordinal);
        var entryCount = 0;
        var candidateCount = 0;

        var kebs = new List<string>();
        string? reb = null;
        string? romaji = null;
        var types = new List<string>();
        var inEntry = false;
        var order = 0;

        foreach (var line in File.ReadLines(input, Encoding.UTF8))
        {
            if (!inEntry)
            {
                if (line.Trim() == "<entry>")
                {
                    inEntry = true;
                    order++;
                    kebs.Clear();
                    types.Clear();
                    reb = null;
                    romaji = null;
                }
                continue;
            }

            if (line.Trim() == "</entry>")
            {
                inEntry = false;
                entryCount++;

                if (kebs.Count > 0 && reb is not null && romaji is not null && IsWanted(types, romaji))
                {
                    candidateCount++;
                    var score = ScoreFor(types);
                    var candidate = new Candidate(romaji, order, score);

                    foreach (var keb in kebs)
                    {
                        // Only kanji-bearing keys need a table entry; kana romanises locally anyway.
                        if (keb.Length is 0 or > 8 || !ContainsKanji(keb)) continue;

                        if (!byKanji.TryGetValue(keb, out var list))
                        {
                            list = new List<Candidate>();
                            byKanji[keb] = list;
                        }
                        list.Add(candidate);
                    }
                }
                continue;
            }

            var trimmed = line.Trim();
            if (trimmed.StartsWith("<keb>", StringComparison.Ordinal)) kebs.Add(TagText(trimmed));
            else if (trimmed.StartsWith("<reb>", StringComparison.Ordinal)) reb ??= TagText(trimmed);
            else if (trimmed.StartsWith("<name_type>", StringComparison.Ordinal))
            {
                var type = TagText(trimmed);
                if (type.Length > 0) types.Add(type);
            }
            else if (trimmed.StartsWith("<trans_det>", StringComparison.Ordinal)) romaji ??= TagText(trimmed);
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(output));
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        // How many distinct kanji spellings share each romanisation. A common surname's romanisation
        // is shared across many spellings (Satou for 佐藤/佐東/沙藤) while a rare variant belongs to
        // one spelling only — this is the best ordering signal JMnedict offers.
        var romajiSpread = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var list in byKanji.Values)
        {
            var distinct = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var candidate in list) distinct.Add(candidate.Romaji);
            foreach (var reading in distinct)
                romajiSpread[reading] = (romajiSpread.TryGetValue(reading, out var seen) ? seen : 0) + 1;
        }

        var text = new StringBuilder(byKanji.Count * 24);
        foreach (var (kanji, list) in byKanji.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            var best = list
                .OrderByDescending(c => c.Score)
                .ThenByDescending(c => romajiSpread.TryGetValue(c.Romaji, out var spread) ? spread : 0)
                .ThenBy(c => c.Order)
                .First();

            text.Append(kanji).Append('\t').Append(best.Romaji).Append('\n');
        }

        var raw = Encoding.UTF8.GetBytes(text.ToString());
        using (var file = File.Create(output))
        using (var gzip = new GZipStream(file, CompressionLevel.SmallestSize))
        {
            gzip.Write(raw, 0, raw.Length);
        }

        var gzipped = File.ReadAllBytes(output);
        var sha = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(gzipped)).ToLowerInvariant();

        if (meta is not null)
        {
            var payload = JsonSerializer.Serialize(new
            {
                created = createdDate,
                count = byKanji.Count,
                entries = entryCount,
                candidates = candidateCount,
                compressedBytes = gzipped.Length,
                rawBytes = raw.Length,
                sha256 = sha
            }, new JsonSerializerOptions { WriteIndented = true });

            File.WriteAllText(meta, payload);
            Console.WriteLine($"meta written: {meta}");
        }

        Console.WriteLine($"entries={entryCount} candidates={candidateCount} readings={byKanji.Count} raw={raw.Length / 1024}KB gz={gzipped.Length / 1024}KB created={createdDate}");
        return 0;
    }

    /// <summary>Extracts the text of a single-line element and strips XML entity markers.</summary>
    private static string TagText(string line)
    {
        var start = line.IndexOf('>');
        var end = line.LastIndexOf('<');
        if (start < 0 || end <= start) return string.Empty;
        return line[(start + 1)..end].Replace("&", string.Empty).Replace(";", string.Empty).Trim();
    }

    /// <summary>
    /// Surname/given-name entries are the only ones this app uses. An entry carrying more than one
    /// type (typically surname + place) is an established real-world reading and outranks a
    /// single-tagged variant — that is the signal that picks たなか over たなた.
    /// </summary>
    private static int ScoreFor(List<string> types)
    {
        // Type priority only. A multi-type bonus was tried and made things worse (it selected
        // Denchuu for 田中), so variant ordering is left to the romanisation-spread signal.
        var score = 0;

        foreach (var type in types)
        {
            score += type switch
            {
                "surname" => 5,
                "given" => 4,
                "person" => 3,
                "fem" or "masc" => 3,
                "place" => 0,
                _ => 0
            };
        }

        return score;
    }

    /// <summary>
    /// Keeps only personal-name entries. Unfiltered JMnedict yields ~530k keys (11 MB) dominated by
    /// place names, products and works, which would win longest-match against real surnames.
    /// </summary>
    private static bool IsWanted(List<string> types, string romaji)
    {
        var personal = false;
        foreach (var type in types)
        {
            if (type is "surname" or "given" or "person" or "fem" or "masc") { personal = true; break; }
        }

        if (!personal) return false;
        if (romaji.Length is 0 or > 40) return false;

        foreach (var c in romaji)
        {
            var ok = c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or '\'' or '-' or ' ';
            if (!ok) return false;
        }

        return true;
    }

    private static bool ContainsKanji(string text)
    {
        foreach (var c in text)
            if (c is >= '\u4e00' and <= '\u9fff' or >= '\u3400' and <= '\u4dbf') return true;
        return false;
    }

    private static string? GetArg(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
            if (args[i] == name) return args[i + 1];
        return null;
    }
}
