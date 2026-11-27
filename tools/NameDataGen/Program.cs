using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace NameDataGen;

/// <summary>
/// Builds the shipped kanji-to-romaji name table from EDRDG JMnedict. Parsed line by line because JMnedict's DOCTYPE
/// declares entities the runtime XML parser rejects, and the file is strictly one element per line. JMnedict has no
/// frequency data, so variant choice is heuristic; genuine ties are left to the curated override file.
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
        var ipadicHash = GetArg(args, "--ipadic-hash");

        if (input is null || output is null)
        {
            Console.Error.WriteLine("usage: NameDataGen --input <JMnedict.xml> --output <name_readings.tsv.gz> [--meta <meta.json>] [--created yyyy-MM-dd] [--ipadic-hash <sha256>]");
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
                        // Kana romanises locally anyway, so only kanji-bearing keys need an entry.
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

        // How many kanji spellings share each romanisation: a common surname's reading spans many spellings
        // (Satou for 佐藤/佐東/沙藤) while a rare variant has one. JMnedict's best ordering signal.
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
                sha256 = sha,
                // Lets the app reject a swapped IPADIC archive before extracting it.
                ipadicSha256 = string.IsNullOrWhiteSpace(ipadicHash) ? null : ipadicHash.ToLowerInvariant()
            }, new JsonSerializerOptions { WriteIndented = true });

            File.WriteAllText(meta, payload);
            Console.WriteLine($"meta written: {meta}");
        }

        Console.WriteLine($"entries={entryCount} candidates={candidateCount} readings={byKanji.Count} raw={raw.Length / 1024}KB gz={gzipped.Length / 1024}KB created={createdDate}");
        return 0;
    }

    private static string TagText(string line)
    {
        var start = line.IndexOf('>');
        var end = line.LastIndexOf('<');
        if (start < 0 || end <= start) return string.Empty;
        return line[(start + 1)..end].Replace("&", string.Empty).Replace(";", string.Empty).Trim();
    }

    private static int ScoreFor(List<string> types)
    {
        // Type priority only. A multi-type bonus was tried and picked Denchuu for 田中, so variant ordering
        // is left to the romanisation-spread signal.
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
    /// Personal names only: unfiltered JMnedict yields ~530k keys dominated by place and product names, which would
    /// win longest-match against real surnames.
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

    // CJK Unified Ideographs or Extension A.
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
