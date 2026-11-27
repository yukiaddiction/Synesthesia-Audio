using System.Text.RegularExpressions;

namespace Synesthesia_Audio.Core;

public static class DLsiteParser
{
    public sealed class RawWorkPageData
    {
        public string ParsedTitle { get; init; } = "";
        public string OriginalTitleRaw { get; init; } = "";
        public string Circle { get; init; } = "Unknown Circle";
        public string CoverUrl { get; init; } = "";
        public string VaRaw { get; init; } = "";
        public string AgeRating { get; init; } = AgeRatings.Unknown;
        public string Genre { get; init; } = "";
        public bool IsEngPage { get; init; }
        public bool TitleFound { get; init; }
        public bool CircleFound { get; init; }
        public bool CoverFound { get; init; }

        /// <summary>Which strategy produced the title: jsonld, og:title, h1, document-title or none.</summary>
        public string TitleSource { get; init; } = "none";

        public bool VaFound { get; init; }
        public bool GenreFound { get; init; }
        public bool HasJsonLd { get; init; }
        public bool HasMetaTable { get; init; }

        /// <summary>A title candidate that failed validation, kept only for the diagnostic log.</summary>
        public string TitleRaw { get; init; } = "";

        public string JsonldKeys { get; init; } = "";

        public string TableLabels { get; init; } = "";
    }

    /// <summary>Titles that only an error or region-block page can produce.</summary>
    private static readonly string[] _blockedTitleWords =
    {
        "ページが見つかりません",
        "ご利用いただけません",
        "アクセスが制限",
        "Not Found",
        "Forbidden"
    };

    private static readonly string[] _vaLabels =
    {
        "Voice Actor", "Voice Actors", "Cast", "CV", "Character", "声優", "出演", "ボイス", "キャラクター", "キャラ"
    };

    private static readonly string[] _ageLabels =
    {
        "年齢指定", "年齢制限", "Age Restriction", "Age rating"
    };

    private static readonly string[] _genreLabels =
    {
        "ジャンル", "Genre", "Genres", "Genre(s)", "分類", "タグ", "カテゴリ", "カテゴリー", "Tag", "Tags"
    };

    public static RawWorkPageData ParseWorkPageHtml(string html, string url)
    {
        bool isEng = url.Contains("/eng/");
        string parsedTitle = "";
        string titleSource = "none";
        string titleRaw = "";
        string jsonldKeys = "";
        string originalTitleRaw = "";
        string circle = "Unknown Circle";
        string coverUrl = "";
        string vaRaw = "";
        string ageRating = AgeRatings.Sfw;
        // A product page always has an age row, so no signal means we parsed some other page; reporting SFW
        // there leaked R-18 works into the All-Age list.
        bool ageRatingFound = false;
        string genre = "";

        // Strategy 1: JSON-LD. DLsite emits several blocks and the root often names the site, so only a node with a
        // valid title is used, and the other fields come from that same node.
        // Regex: each <script type="application/ld+json"> block; group 1 is its JSON body.
        var jsonLdMatches = Regex.Matches(html,
            @"<script\s+type=""application/ld\+json""[^>]*>(.*?)</script>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);

        foreach (Match jsonLdMatch in jsonLdMatches)
        {
            if (IsValidWorkTitle(parsedTitle) || !string.IsNullOrEmpty(originalTitleRaw))
                break;

            try
            {
                var json = jsonLdMatch.Groups[1].Value.Trim();
                using var doc = System.Text.Json.JsonDocument.Parse(json);

                var usedNode = false;
                foreach (var node in CandidateJsonLdNodes(doc.RootElement))
                {
                    var nameStr = ReadJsonString(node, "name");
                    if (string.IsNullOrEmpty(nameStr))
                        continue;

                    if (!IsValidWorkTitle(nameStr))
                    {
                        if (string.IsNullOrEmpty(titleRaw))
                            titleRaw = nameStr;
                        continue;
                    }

                    if (isEng)
                    {
                        if (string.IsNullOrEmpty(originalTitleRaw))
                            originalTitleRaw = nameStr;
                    }
                    else
                    {
                        parsedTitle = nameStr;
                        titleSource = "jsonld";
                    }

                    if (string.IsNullOrEmpty(coverUrl))
                    {
                        var imgStr = ReadJsonString(node, "image");
                        if (!string.IsNullOrEmpty(imgStr))
                        {
                            imgStr = DlsiteUrl.EnsureAbsolute(imgStr);
                            coverUrl = imgStr;
                        }
                    }

                    if (circle == "Unknown Circle")
                    {
                        var brandName = ReadJsonNestedString(node, "brand", "name");
                        if (!string.IsNullOrEmpty(brandName))
                            circle = brandName;
                    }

                    if (string.IsNullOrEmpty(genre))
                    {
                        var keywords = ReadJsonKeywords(node);
                        if (string.IsNullOrEmpty(keywords))
                        {
                            // schema.org sometimes carries tags as genre, occasionally as a URL.
                            var jsonGenre = ReadJsonString(node, "genre");
                            if (!string.IsNullOrEmpty(jsonGenre) &&
                                !jsonGenre.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                                keywords = jsonGenre;
                        }

                        if (!string.IsNullOrEmpty(keywords))
                            genre = keywords;
                    }

                    if (string.IsNullOrEmpty(vaRaw))
                    {
                        var people = ReadJsonPeople(node);
                        if (!string.IsNullOrEmpty(people))
                            vaRaw = people;
                    }

                    var keys = new List<string>();
                    foreach (var property in node.EnumerateObject())
                        keys.Add(property.Name);
                    jsonldKeys = string.Join(",", keys);

                    usedNode = true;
                    break;
                }

                if (usedNode)
                    break;
            }
            catch { }
        }

        // Strategy 2: og: meta. Block and consent pages put the site name here, so only a plausible title is accepted.
        if (!IsValidWorkTitle(parsedTitle) && !isEng)
        {
            var ogTitle = System.Net.WebUtility.HtmlDecode(ExtractMetaContent(html, "og:title"));
            if (IsValidWorkTitle(ogTitle))
            {
                parsedTitle = ogTitle;
                titleSource = "og:title";
            }
            else if (!string.IsNullOrEmpty(ogTitle) && string.IsNullOrEmpty(titleRaw))
            {
                titleRaw = ogTitle;
            }
        }
        if (string.IsNullOrEmpty(coverUrl))
        {
            // <meta itemprop="image" content="...">; group 1 is the URL.
            var itempropMatch = Regex.Match(html, @"<meta\s+itemprop=""image""\s+content=""([^""]*)""", RegexOptions.IgnoreCase);
            if (itempropMatch.Success)
                coverUrl = itempropMatch.Groups[1].Value;
            else
                coverUrl = ExtractMetaContent(html, "og:image");

            coverUrl = DlsiteUrl.EnsureAbsolute(coverUrl);
        }
        if (circle == "Unknown Circle")
        {
            var maker = ExtractMetaContent(html, "product:maker");
            if (!string.IsNullOrEmpty(maker)) circle = System.Net.WebUtility.HtmlDecode(maker);
        }

        // Strategy 3: classic markup scraping for the remaining fields.
        if (!IsValidWorkTitle(parsedTitle))
        {
            // Inner HTML of <h1 id="work_name">; lazy (.*?) rather than [^<]+ because the current template nests
            // tags inside the heading.
            var titleMatch = Regex.Match(html, @"<h1[^>]*id=""work_name""[^>]*>(.*?)</h1>",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (titleMatch.Success)
            {
                // Strip every tag.
                var heading = Regex.Replace(titleMatch.Groups[1].Value, @"<[^>]+>", string.Empty);
                var headingText = System.Net.WebUtility.HtmlDecode(heading.Trim());
                if (IsValidWorkTitle(headingText))
                {
                    parsedTitle = headingText;
                    titleSource = "h1";
                }
                else if (!string.IsNullOrEmpty(headingText) && string.IsNullOrEmpty(titleRaw))
                {
                    titleRaw = headingText;
                }
            }
        }

        if (!IsValidWorkTitle(parsedTitle))
        {
            var docTitle = ExtractTitleFromDocument(html);
            if (IsValidWorkTitle(docTitle))
            {
                parsedTitle = docTitle;
                titleSource = "document-title";
            }
        }

        // Also replaces a non-empty but invalid title, such as the JSON-LD site name "DLsite".
        if (!IsValidWorkTitle(parsedTitle))
        {
            parsedTitle = "Unknown Title";
            titleSource = "none";
        }

        if (isEng && string.IsNullOrEmpty(originalTitleRaw))
        {
            // English pages: <th>Original title</th><td>[<span>]value[</span>]</td>; group 1 is the value, whose
            // remaining tags are then stripped.
            var origTitleMatch = Regex.Match(html, @"<th[^>]*>\s*Original\s+[Tt]itle\s*</th>\s*<td[^>]*>\s*(?:<span[^>]*>)?(.*?)(?:</span>)?\s*</td>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (origTitleMatch.Success)
                originalTitleRaw = System.Net.WebUtility.HtmlDecode(Regex.Replace(origTitleMatch.Groups[1].Value, @"<[^>]+>", "").Trim());
        }

        if (circle == "Unknown Circle")
        {
            // <span class="maker_name"><a ...>name</a>; group 1 is the circle name.
            var circleMatch = Regex.Match(html, @"<span[^>]*class=""maker_name""[^>]*>\s*<a[^>]+>([^<]+)</a>", RegexOptions.IgnoreCase);
            if (circleMatch.Success) circle = System.Net.WebUtility.HtmlDecode(circleMatch.Groups[1].Value.Trim());
        }

        if (string.IsNullOrEmpty(coverUrl))
        {
            // Exactly <meta itemprop="image" content="...">; group 1 is the URL.
            var coverMatch = Regex.Match(html, @"<meta itemprop=""image"" content=""([^""]+)""", RegexOptions.IgnoreCase);
            if (coverMatch.Success)
            {
                coverUrl = coverMatch.Groups[1].Value;
                coverUrl = DlsiteUrl.EnsureAbsolute(coverUrl);
            }
        }

        if (string.IsNullOrEmpty(vaRaw))
        {
            vaRaw = FindDefinitionValue(html, _vaLabels);
        }

        if (string.IsNullOrEmpty(vaRaw))
        {
            vaRaw = FindTableValue(html, _vaLabels);
        }

        // Many works list the cast only inside the title ("...【CV:name】...").
        if (string.IsNullOrEmpty(vaRaw))
        {
            vaRaw = ExtractCastFromTitle(parsedTitle);
        }

        if (ageRating == "SFW")
        {
            // Current template: <dt>年齢指定</dt><dd><span class="label_r18">R18</span></dd>.
            var ageText = FindDefinitionValue(html, _ageLabels);
            if (string.IsNullOrEmpty(ageText))
                ageText = FindTableValue(html, _ageLabels);

            if (!string.IsNullOrEmpty(ageText))
            {
                ageRating = ResolveAgeRating(ageText);
                ageRatingFound = true;
            }
            // The R18 badge's CSS class anywhere on the page.
            else if (Regex.IsMatch(html, "label_r18", RegexOptions.IgnoreCase))
            {
                ageRating = AgeRatings.Nsfw;
                ageRatingFound = true;
            }

            // Legacy row <th>年齢指定|年齢制限|Age Restriction|Age rating</th><td>value</td>; group 1 is the cell.
            var ageMatch = Regex.Match(html, @"<th[^>]*>\s*(?:年齢指定|年齢制限|Age\s+Restriction|Age\s+rating)\s*</th>\s*<td[^>]*>(.*?)</td>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (ageMatch.Success)
            {
                ageRatingFound = true;
                var tdContent = ageMatch.Groups[1].Value;
                // Adult markers: R18/R-18, 18+, ADL, 18禁, 18歳未満使用禁止.
                if (Regex.IsMatch(tdContent, @"(R-?18|18\+|ADL|18禁|18歳未満使用禁止)", RegexOptions.IgnoreCase))
                    ageRating = AgeRatings.Nsfw;
            }
        }

        if (!ageRatingFound)
        {
            ageRating = AgeRatings.Unknown;
        }

        if (string.IsNullOrEmpty(genre))
        {
            // Genre-index links are the only tag signal that survived the template redesign.
            genre = ExtractGenreLinks(html);
        }

        if (string.IsNullOrEmpty(genre))
        {
            genre = FindDefinitionValue(html, _genreLabels);
        }

        if (string.IsNullOrEmpty(genre))
        {
            genre = FindTableValue(html, _genreLabels);
        }

        bool titleFound = IsValidWorkTitle(parsedTitle);
        bool circleFound = circle != "Unknown Circle";
        bool coverFound = !string.IsNullOrEmpty(coverUrl);

        // Diagnostics: the first ten distinct <th> labels, to show which labels the page actually uses.
        var labelList = new List<string>();
        foreach (Match labelMatch in Regex.Matches(html, @"<th\b[^>]*>(.*?)</th>",
                     RegexOptions.IgnoreCase | RegexOptions.Singleline))
        {
            var labelText = StripTags(labelMatch.Groups[1].Value);
            if (!string.IsNullOrEmpty(labelText) && !labelList.Contains(labelText))
                labelList.Add(labelText);
            if (labelList.Count >= 10)
                break;
        }
        var tableLabels = string.Join(" | ", labelList);

        return new RawWorkPageData
        {
            ParsedTitle = parsedTitle,
            OriginalTitleRaw = originalTitleRaw,
            Circle = circle,
            CoverUrl = coverUrl,
            VaRaw = vaRaw,
            AgeRating = ageRating,
            Genre = genre,
            IsEngPage = isEng,
            TitleFound = titleFound,
            CircleFound = circleFound,
            CoverFound = coverFound,
            TitleSource = titleSource,
            VaFound = !string.IsNullOrEmpty(vaRaw),
            GenreFound = !string.IsNullOrEmpty(genre),
            HasJsonLd = jsonLdMatches.Count > 0,
            HasMetaTable = html.Contains("<th", StringComparison.OrdinalIgnoreCase),
            TitleRaw = titleRaw,
            JsonldKeys = jsonldKeys,
            TableLabels = tableLabels
        };
    }

    private static string ExtractMetaContent(string html, string property)
    {
        // <meta property|name="X" content="...">, tried with double then single quotes; group 1 is the content.
        var match = Regex.Match(html,
            $@"<meta\s+(?:property|name)=""{Regex.Escape(property)}""\s+content=""([^""]*)""",
            RegexOptions.IgnoreCase);
        if (match.Success)
            return match.Groups[1].Value;

        match = Regex.Match(html,
            $@"<meta\s+(?:property|name)='{Regex.Escape(property)}'\s+content='([^']*)'",
            RegexOptions.IgnoreCase);
        if (match.Success)
            return match.Groups[1].Value;

        return string.Empty;
    }

    /// <summary>Used only to tell an age gate from a region block when a page is not a product page.</summary>
    public static string ExtractDocumentTitle(string html)
    {
        if (string.IsNullOrEmpty(html))
            return string.Empty;

        // Inner text of the <title> element.
        var match = Regex.Match(html, @"<title[^>]*>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return match.Success ? System.Net.WebUtility.HtmlDecode(match.Groups[1].Value.Trim()) : string.Empty;
    }

    private static string ExtractTitleFromDocument(string html)
    {
        var raw = ExtractDocumentTitle(html);
        if (string.IsNullOrEmpty(raw))
            return string.Empty;

        // Drops a "| DLsite..." suffix (ASCII or full-width bar): "Work name | DLsite 同人 - R18" becomes "Work name".
        var stripped = Regex.Replace(raw, @"\s*[|｜]\s*DLsite.*$", string.Empty, RegexOptions.IgnoreCase).Trim();
        return stripped;
    }

    /// <summary>The JSON-LD root plus every @graph entry, so a nested product node can be found.</summary>
    private static IEnumerable<System.Text.Json.JsonElement> CandidateJsonLdNodes(System.Text.Json.JsonElement root)
    {
        yield return root;

        if (root.ValueKind != System.Text.Json.JsonValueKind.Object)
            yield break;

        if (root.TryGetProperty("@graph", out var graph) &&
            graph.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            foreach (var item in graph.EnumerateArray())
                yield return item;
        }
    }

    /// <summary>Reads a plain string, the first string of an array, or an object's url/name.</summary>
    private static string ReadJsonString(System.Text.Json.JsonElement node, string property)
    {
        if (node.ValueKind != System.Text.Json.JsonValueKind.Object ||
            !node.TryGetProperty(property, out var value))
            return string.Empty;

        switch (value.ValueKind)
        {
            case System.Text.Json.JsonValueKind.String:
                return System.Net.WebUtility.HtmlDecode(value.GetString() ?? string.Empty).Trim();

            case System.Text.Json.JsonValueKind.Array:
                foreach (var item in value.EnumerateArray())
                {
                    if (item.ValueKind == System.Text.Json.JsonValueKind.String)
                        return System.Net.WebUtility.HtmlDecode(item.GetString() ?? string.Empty).Trim();
                }
                return string.Empty;

            case System.Text.Json.JsonValueKind.Object:
                if (value.TryGetProperty("url", out var url) && url.ValueKind == System.Text.Json.JsonValueKind.String)
                    return url.GetString() ?? string.Empty;
                if (value.TryGetProperty("name", out var nested) && nested.ValueKind == System.Text.Json.JsonValueKind.String)
                    return System.Net.WebUtility.HtmlDecode(nested.GetString() ?? string.Empty).Trim();
                return string.Empty;

            default:
                return string.Empty;
        }
    }

    private static string ReadJsonNestedString(System.Text.Json.JsonElement node, string property, string nestedProperty)
    {
        if (node.ValueKind != System.Text.Json.JsonValueKind.Object ||
            !node.TryGetProperty(property, out var value) ||
            value.ValueKind != System.Text.Json.JsonValueKind.Object ||
            !value.TryGetProperty(nestedProperty, out var nested) ||
            nested.ValueKind != System.Text.Json.JsonValueKind.String)
            return string.Empty;

        return System.Net.WebUtility.HtmlDecode(nested.GetString() ?? string.Empty).Trim();
    }

    /// <summary>Reads keywords as either a comma separated string or an array.</summary>
    private static string ReadJsonKeywords(System.Text.Json.JsonElement node)
    {
        if (node.ValueKind != System.Text.Json.JsonValueKind.Object ||
            !node.TryGetProperty("keywords", out var keywords))
            return string.Empty;

        var list = new List<string>();

        if (keywords.ValueKind == System.Text.Json.JsonValueKind.String)
        {
            var text = keywords.GetString();
            if (!string.IsNullOrEmpty(text))
            {
                foreach (var keyword in text.Split(','))
                {
                    var trimmed = keyword.Trim();
                    if (!string.IsNullOrEmpty(trimmed))
                        list.Add(System.Net.WebUtility.HtmlDecode(trimmed));
                }
            }
        }
        else if (keywords.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            foreach (var keyword in keywords.EnumerateArray())
            {
                if (keyword.ValueKind != System.Text.Json.JsonValueKind.String)
                    continue;

                var text = keyword.GetString();
                if (!string.IsNullOrEmpty(text))
                    list.Add(System.Net.WebUtility.HtmlDecode(text.Trim()));
            }
        }

        return string.Join(", ", list);
    }

    /// <summary>Cast / voice actors from schema.org person-ish properties.</summary>
    private static string ReadJsonPeople(System.Text.Json.JsonElement node)
    {
        if (node.ValueKind != System.Text.Json.JsonValueKind.Object)
            return string.Empty;

        var properties = new[] { "actor", "performer", "byArtist", "contributor", "creator", "author" };
        foreach (var property in properties)
        {
            if (!node.TryGetProperty(property, out var value))
                continue;

            var names = new List<string>();
            if (value.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var item in value.EnumerateArray())
                    AddPersonName(item, names);
            }
            else
            {
                AddPersonName(value, names);
            }

            if (names.Count > 0)
                return string.Join(", ", names);
        }

        return string.Empty;
    }

    private static void AddPersonName(System.Text.Json.JsonElement item, List<string> names)
    {
        if (item.ValueKind == System.Text.Json.JsonValueKind.String)
        {
            var direct = System.Net.WebUtility.HtmlDecode(item.GetString() ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(direct))
                names.Add(direct);
            return;
        }

        if (item.ValueKind == System.Text.Json.JsonValueKind.Object &&
            item.TryGetProperty("name", out var name) &&
            name.ValueKind == System.Text.Json.JsonValueKind.String)
        {
            var text = System.Net.WebUtility.HtmlDecode(name.GetString() ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(text))
                names.Add(text);
        }
    }

    /// <summary>Anything without an adult marker, including "全年齢", is SFW.</summary>
    private static string ResolveAgeRating(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "SFW";

        // Adult markers: R18/R-18, 18禁, 18+, 成人, 18歳未満, adult.
        if (Regex.IsMatch(value, @"(R-?18|18禁|18\+|成人|18歳未満|adult)", RegexOptions.IgnoreCase))
            return "NSFW";

        return "SFW";
    }

    private static string FindDefinitionValue(string html, string[] labels)
    {
        // Adjacent <dt>label</dt><dd>value</dd> pairs, as the current template uses; groups 1 and 2 are their inner HTML.
        foreach (Match pair in Regex.Matches(html,
                     @"<dt\b[^>]*>(.*?)</dt>\s*<dd\b[^>]*>(.*?)</dd>",
                     RegexOptions.IgnoreCase | RegexOptions.Singleline))
        {
            var label = StripTags(pair.Groups[1].Value);
            if (!MatchesLabel(label, labels))
                continue;

            var value = ExtractCellText(pair.Groups[2].Value);
            if (!string.IsNullOrEmpty(value))
                return value;
        }

        return string.Empty;
    }

    private static string ExtractGenreLinks(string html)
    {
        var names = new List<string>();
        // <a> elements whose href contains /fsr/=/genre/; group 1 is the anchor's inner HTML.
        foreach (Match anchor in Regex.Matches(html,
                     @"<a\b[^>]*href=""[^""]*/fsr/=/genre/[^""]*""[^>]*>(.*?)</a>",
                     RegexOptions.IgnoreCase | RegexOptions.Singleline))
        {
            var text = StripTags(anchor.Groups[1].Value);
            if (!string.IsNullOrEmpty(text) && !names.Contains(text))
                names.Add(text);
        }

        return string.Join(", ", names);
    }

    /// <summary>
    /// Any row whose first cell matches a label: an exact &lt;th&gt;...&lt;td&gt; pattern broke on markup changes.
    /// </summary>
    private static string FindTableValue(string html, string[] labels)
    {
        // Each <tr>'s inner HTML, then its <th>/<td> cells; \1 makes the closing tag match the opening one.
        foreach (Match row in Regex.Matches(html, @"<tr\b[^>]*>(.*?)</tr>",
                     RegexOptions.IgnoreCase | RegexOptions.Singleline))
        {
            var cells = Regex.Matches(row.Groups[1].Value, @"<(th|td)\b[^>]*>(.*?)</\1>",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (cells.Count < 2)
                continue;

            var label = StripTags(cells[0].Groups[2].Value);
            if (!MatchesLabel(label, labels))
                continue;

            for (var i = 1; i < cells.Count; i++)
            {
                var value = ExtractCellText(cells[i].Groups[2].Value);
                if (!string.IsNullOrEmpty(value))
                    return value;
            }
        }

        return string.Empty;
    }

    private static bool MatchesLabel(string label, string[] labels)
    {
        var normalized = label.Replace("：", string.Empty).Replace(":", string.Empty).Trim();
        if (string.IsNullOrEmpty(normalized))
            return false;

        foreach (var candidate in labels)
        {
            if (normalized.Equals(candidate, StringComparison.OrdinalIgnoreCase))
                return true;

            // Tolerate decoration inside the label cell (icons, parentheticals, extra words).
            if (normalized.Contains(candidate, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>Anchor texts when present, otherwise the cell's own text.</summary>
    private static string ExtractCellText(string cellHtml)
    {
        // Inner HTML of every <a> element.
        var anchors = Regex.Matches(cellHtml, @"<a\b[^>]*>(.*?)</a>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        if (anchors.Count > 0)
        {
            var names = new List<string>();
            foreach (Match anchor in anchors)
            {
                var text = StripTags(anchor.Groups[1].Value);
                if (!string.IsNullOrEmpty(text))
                    names.Add(text);
            }

            if (names.Count > 0)
                return string.Join(", ", names);
        }

        return StripTags(cellHtml);
    }

    private static string StripTags(string value)
    {
        // Tags become spaces, then whitespace runs collapse to one space.
        var text = Regex.Replace(value, @"<[^>]+>", " ");
        text = System.Net.WebUtility.HtmlDecode(text);
        return Regex.Replace(text, @"\s+", " ").Trim();
    }

    /// <summary>Cast written into the title, e.g. "【CV:立花日菜】" or "(cv.鈴代紗弓)".</summary>
    private static string ExtractCastFromTitle(string title)
    {
        if (string.IsNullOrEmpty(title))
            return string.Empty;

        // "CV" (ASCII or full-width) + ":", "：" or "."; group 1 runs up to a closing 】 ] ) or ）.
        var match = Regex.Match(title, @"[CcＣ][VvＶ]\s*[:：.]\s*([^】\]\)）]+)");
        if (!match.Success)
            return string.Empty;

        var cast = StripTags(match.Groups[1].Value).Trim(' ', '、', ',', '，', '/');
        return cast;
    }

    /// <summary>
    /// Block and consent pages carry DLsite's own name as the title, so a non-product page must never pass as a work.
    /// </summary>
    private static bool IsValidWorkTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return false;

        var trimmed = title.Trim();

        if (trimmed.Equals("Unknown Title", StringComparison.OrdinalIgnoreCase))
            return false;

        if (trimmed.Equals("DLsite", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("DLsite.com", StringComparison.OrdinalIgnoreCase))
            return false;

        // Stripping the site suffix from an error page would otherwise leave a plausible title.
        foreach (var blocked in _blockedTitleWords)
        {
            if (trimmed.Contains(blocked, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    public static string ParseTranslateJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return string.Empty;

        var sb = new System.Text.StringBuilder();
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind == System.Text.Json.JsonValueKind.Array && root.GetArrayLength() > 0)
            {
                var firstArray = root[0];
                if (firstArray.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    foreach (var element in firstArray.EnumerateArray())
                    {
                        if (element.ValueKind == System.Text.Json.JsonValueKind.Array && element.GetArrayLength() > 0)
                        {
                            sb.Append(element[0].GetString());
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DLsiteParser] ParseTranslateJson failed: {ex.Message}");
        }

        return sb.ToString();
    }

    public static RawWorkPageData ParseProductJson(string json, string rjCode)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;
            System.Text.Json.JsonElement element = default;
            bool elementFound = false;

            // product.json returns an object or an array, and workno may or may not carry the RJ prefix.
            if (root.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var item in root.EnumerateArray())
                {
                    string workno = "";
                    if (item.TryGetProperty("workno", out var worknoProp))
                    {
                        workno = worknoProp.GetString() ?? "";
                    }
                    if (string.IsNullOrEmpty(workno) && item.TryGetProperty("product_id", out var prodProp))
                    {
                        workno = prodProp.GetString() ?? "";
                    }

                    if (!string.IsNullOrEmpty(workno))
                    {
                        if (DlsiteWorkNumber.RefersToSameWork(workno, rjCode))
                        {
                            element = item;
                            elementFound = true;
                            break;
                        }
                    }
                }
            }
            else if (root.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                string workno = "";
                if (root.TryGetProperty("workno", out var worknoProp))
                {
                    workno = worknoProp.GetString() ?? "";
                }
                if (string.IsNullOrEmpty(workno) && root.TryGetProperty("product_id", out var prodProp))
                {
                    workno = prodProp.GetString() ?? "";
                }

                if (!string.IsNullOrEmpty(workno))
                {
                    if (DlsiteWorkNumber.RefersToSameWork(workno, rjCode))
                    {
                        element = root;
                        elementFound = true;
                    }
                }
            }

            if (!elementFound)
            {
                return new RawWorkPageData { TitleFound = false };
            }

            string title = "";
            if (element.TryGetProperty("work_name", out var titleProp))
            {
                title = titleProp.GetString() ?? "";
            }

            string circle = "Unknown Circle";
            if (element.TryGetProperty("maker_name", out var makerProp))
            {
                circle = makerProp.GetString() ?? "Unknown Circle";
            }

            string coverUrl = "";
            if (element.TryGetProperty("image_main", out var imageMainProp) && imageMainProp.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                if (imageMainProp.TryGetProperty("url", out var urlProp))
                {
                    coverUrl = urlProp.GetString() ?? "";
                }
            }
            if (string.IsNullOrEmpty(coverUrl) && element.TryGetProperty("image_thumb", out var imageThumbProp))
            {
                coverUrl = imageThumbProp.GetString() ?? "";
            }
            coverUrl = DlsiteUrl.EnsureAbsolute(coverUrl);

            // The API only reports an adult flag, so a missing field is "not stated" rather than all-ages.
            string ageRating = AgeRatings.Unknown;
            if (element.TryGetProperty("age_category_string", out var ageProp))
            {
                var ageStr = ageProp.GetString();
                ageRating = string.Equals(ageStr, "adult", StringComparison.OrdinalIgnoreCase)
                    ? AgeRatings.Nsfw
                    : AgeRatings.Sfw;
            }

            string vaRaw = "";
            if (element.TryGetProperty("creaters", out var creatorsProp) && creatorsProp.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                if (creatorsProp.TryGetProperty("voice_by", out var voiceProp) && voiceProp.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    var vaList = new List<string>();
                    foreach (var vaItem in voiceProp.EnumerateArray())
                    {
                        if (vaItem.TryGetProperty("name", out var vaNameProp))
                        {
                            var name = vaNameProp.GetString();
                            if (!string.IsNullOrEmpty(name))
                            {
                                vaList.Add(name.Trim());
                            }
                        }
                    }
                    vaRaw = string.Join(", ", vaList);
                }
            }

            string genre = "";
            if (element.TryGetProperty("genres", out var genresProp) && genresProp.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                var genreList = new List<string>();
                foreach (var gItem in genresProp.EnumerateArray())
                {
                    if (gItem.TryGetProperty("name", out var gNameProp))
                    {
                        var name = gNameProp.GetString();
                        if (!string.IsNullOrEmpty(name))
                        {
                            genreList.Add(name.Trim());
                        }
                    }
                }
                genre = string.Join(", ", genreList);
            }

            return new RawWorkPageData
            {
                ParsedTitle = title,
                OriginalTitleRaw = "",
                Circle = circle,
                CoverUrl = coverUrl,
                VaRaw = vaRaw,
                AgeRating = ageRating,
                Genre = genre,
                IsEngPage = false,
                TitleFound = !string.IsNullOrEmpty(title),
                CircleFound = !string.Equals(circle, "Unknown Circle"),
                CoverFound = !string.IsNullOrEmpty(coverUrl)
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DLsiteParser] ParseProductJson failed for {rjCode}: {ex.Message}");
            return new RawWorkPageData { TitleFound = false };
        }
    }
}
