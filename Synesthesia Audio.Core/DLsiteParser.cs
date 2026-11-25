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

        /// <summary>Property names of the JSON-LD node that was used (diagnostics only).</summary>
        public string JsonldKeys { get; init; } = "";

        /// <summary>Table row labels found on the page (diagnostics only).</summary>
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

    /// <summary>Row labels used for the cast / voice actor field.</summary>
    private static readonly string[] _vaLabels =
    {
        "Voice Actor", "Voice Actors", "Cast", "CV", "Character", "声優", "出演", "ボイス", "キャラクター", "キャラ"
    };

    /// <summary>Row labels used for the age restriction field.</summary>
    private static readonly string[] _ageLabels =
    {
        "年齢指定", "年齢制限", "Age Restriction", "Age rating"
    };

    /// <summary>Row labels used for the genre / tag field.</summary>
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
        // A real product page always carries an age row, so "no signal" means the page we parsed was not
        // the product page. Reporting SFW there put R-18 works in the All-Age list.
        bool ageRatingFound = false;
        string genre = "";

        // Strategy 1: JSON-LD structured data (most reliable, standards-based).
        // DLsite emits more than one ld+json block and the document root often carries the site
        // name, so every candidate node is tried and only a node with a *valid* title is used.
        // The remaining fields are harvested from that same node so they cannot come from a
        // different entity (which is why circle and genre used to come back empty).
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

        // Strategy 2: og:meta tags as secondary extraction.
        // A region-block or consent page carries the site's own name here, which used to be
        // stored as the work title, so only accept it when it actually looks like a title.
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

        // Strategy 3: Classic regex-based extraction for remaining fields
        if (!IsValidWorkTitle(parsedTitle))
        {
            // (.*?) instead of ([^<]+): the current template wraps the heading in nested tags,
            // which silently broke this strategy and made real product pages look unparseable.
            var titleMatch = Regex.Match(html, @"<h1[^>]*id=""work_name""[^>]*>(.*?)</h1>",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (titleMatch.Success)
            {
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

        // Only now default. This also replaces a non-empty but invalid title, which is what used
        // to short-circuit every later strategy - the JSON-LD site name "DLsite" did exactly that.
        if (!IsValidWorkTitle(parsedTitle))
        {
            parsedTitle = "Unknown Title";
            titleSource = "none";
        }

        if (isEng && string.IsNullOrEmpty(originalTitleRaw))
        {
            var origTitleMatch = Regex.Match(html, @"<th[^>]*>\s*Original\s+[Tt]itle\s*</th>\s*<td[^>]*>\s*(?:<span[^>]*>)?(.*?)(?:</span>)?\s*</td>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (origTitleMatch.Success)
                originalTitleRaw = System.Net.WebUtility.HtmlDecode(Regex.Replace(origTitleMatch.Groups[1].Value, @"<[^>]+>", "").Trim());
        }

        if (circle == "Unknown Circle")
        {
            var circleMatch = Regex.Match(html, @"<span[^>]*class=""maker_name""[^>]*>\s*<a[^>]+>([^<]+)</a>", RegexOptions.IgnoreCase);
            if (circleMatch.Success) circle = System.Net.WebUtility.HtmlDecode(circleMatch.Groups[1].Value.Trim());
        }

        if (string.IsNullOrEmpty(coverUrl))
        {
            var coverMatch = Regex.Match(html, @"<meta itemprop=""image"" content=""([^""]+)""", RegexOptions.IgnoreCase);
            if (coverMatch.Success)
            {
                coverUrl = coverMatch.Groups[1].Value;
                coverUrl = DlsiteUrl.EnsureAbsolute(coverUrl);
            }
        }

        if (string.IsNullOrEmpty(vaRaw))
        {
            // Current DLsite template: <dt class="c-productInfo__key">声優</dt><dd …>value</dd>
            vaRaw = FindDefinitionValue(html, _vaLabels);
        }

        if (string.IsNullOrEmpty(vaRaw))
        {
            vaRaw = FindTableValue(html, _vaLabels);
        }

        // Plenty of DLsite works list the cast inside the title ("...【CV:name】...") rather than
        // in a metadata row, which is why a row-only scan came back empty on every work.
        if (string.IsNullOrEmpty(vaRaw))
        {
            vaRaw = ExtractCastFromTitle(parsedTitle);
        }

        if (ageRating == "SFW")
        {
            // Current template: <dt>年齢指定</dt><dd><span class="label_r18">R18</span></dd>.
            // Without this every work came back SFW, so the library filters were meaningless.
            var ageText = FindDefinitionValue(html, _ageLabels);
            if (string.IsNullOrEmpty(ageText))
                ageText = FindTableValue(html, _ageLabels);

            if (!string.IsNullOrEmpty(ageText))
            {
                ageRating = ResolveAgeRating(ageText);
                ageRatingFound = true;
            }
            else if (Regex.IsMatch(html, "label_r18", RegexOptions.IgnoreCase))
            {
                ageRating = AgeRatings.Nsfw;
                ageRatingFound = true;
            }

            var ageMatch = Regex.Match(html, @"<th[^>]*>\s*(?:年齢指定|年齢制限|Age\s+Restriction|Age\s+rating)\s*</th>\s*<td[^>]*>(.*?)</td>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (ageMatch.Success)
            {
                ageRatingFound = true;
                var tdContent = ageMatch.Groups[1].Value;
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
            // Current DLsite template links each tag as .../fsr/=/genre/<id>/... - the most
            // reliable signal, and the only one that survived the redesign.
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

        // Diagnostics for the fields that still fail: which labels the page actually uses.
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

    /// <summary>
    /// The document title, used only to tell an age gate apart from a region block when a page
    /// turns out not to be a product page.
    /// </summary>
    public static string ExtractDocumentTitle(string html)
    {
        if (string.IsNullOrEmpty(html))
            return string.Empty;

        var match = Regex.Match(html, @"<title[^>]*>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return match.Success ? System.Net.WebUtility.HtmlDecode(match.Groups[1].Value.Trim()) : string.Empty;
    }

    /// <summary>
    /// The work title as it appears in the document title, without the site suffix
    /// ("Work name | DLsite 同人 - R18" becomes "Work name").
    /// </summary>
    private static string ExtractTitleFromDocument(string html)
    {
        var raw = ExtractDocumentTitle(html);
        if (string.IsNullOrEmpty(raw))
            return string.Empty;

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

    /// <summary>Reads a nested string such as brand.name.</summary>
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

    /// <summary>Maps the 年齢指定 text to NSFW/SFW. "全年齢" is the all-ages marker.</summary>
    private static string ResolveAgeRating(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "SFW";

        if (Regex.IsMatch(value, @"(R-?18|18禁|18\+|成人|18歳未満|adult)", RegexOptions.IgnoreCase))
            return "NSFW";

        return "SFW";
    }

    /// <summary>
    /// Definition-list metadata used by the current template:
    /// &lt;dt class="c-productInfo__key"&gt;label&lt;/dt&gt;&lt;dd&gt;value&lt;/dd&gt;.
    /// </summary>
    private static string FindDefinitionValue(string html, string[] labels)
    {
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

    /// <summary>
    /// Genre tags as anchor text of links into the genre index. Survives the template redesign,
    /// unlike the old table markup.
    /// </summary>
    private static string ExtractGenreLinks(string html)
    {
        var names = new List<string>();
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
    /// Scans table rows for a label cell followed by a value cell. Far more tolerant of markup
    /// changes than one exact &lt;th&gt;...&lt;td&gt; pattern, which is what broke genre and cast.
    /// </summary>
    private static string FindTableValue(string html, string[] labels)
    {
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
        var text = Regex.Replace(value, @"<[^>]+>", " ");
        text = System.Net.WebUtility.HtmlDecode(text);
        return Regex.Replace(text, @"\s+", " ").Trim();
    }

    /// <summary>
    /// Cast written into the title itself, e.g. "【CV:立花日菜】" or "(cv.鈴代紗弓)".
    /// The capture stops at the closing bracket so the rest of the title is not swallowed.
    /// </summary>
    private static string ExtractCastFromTitle(string title)
    {
        if (string.IsNullOrEmpty(title))
            return string.Empty;

        var match = Regex.Match(title, @"[CcＣ][VvＶ]\s*[:：.]\s*([^】\]\)）]+)");
        if (!match.Success)
            return string.Empty;

        var cast = StripTags(match.Groups[1].Value).Trim(' ', '、', ',', '，', '/');
        return cast;
    }

    /// <summary>
    /// DLsite answers blocked or consent pages with its own name in the title meta tags, so a
    /// non-product page must never be mistaken for a parsed work.
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

            string targetDigits = Regex.Replace(rjCode, @"\D", "");
            
            // the product.json endpoint can return either a single object or an array.
            // the workno field sometimes includes the RJ prefix and sometimes doesn't,
            // so we strip all non-digits and match by number only.
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
                        string itemDigits = Regex.Replace(workno, @"\D", "");
                        if (string.Equals(targetDigits, itemDigits, StringComparison.OrdinalIgnoreCase))
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
                    string itemDigits = Regex.Replace(workno, @"\D", "");
                    if (string.Equals(targetDigits, itemDigits, StringComparison.OrdinalIgnoreCase))
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
