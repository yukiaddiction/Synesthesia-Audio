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
        public string AgeRating { get; init; } = "SFW";
        public string Genre { get; init; } = "";
        public bool IsEngPage { get; init; }
        public bool TitleFound { get; init; }
        public bool CircleFound { get; init; }
        public bool CoverFound { get; init; }
    }

    public static RawWorkPageData ParseWorkPageHtml(string html, string url)
    {
        bool isEng = url.Contains("/eng/");
        string parsedTitle = "";
        string originalTitleRaw = "";
        string circle = "Unknown Circle";
        string coverUrl = "";
        string vaRaw = "";
        string ageRating = "SFW";
        string genre = "";

        // Strategy 1: JSON-LD structured data (most reliable, standards-based)
        var jsonLdMatch = Regex.Match(html,
            @"<script\s+type=""application/ld\+json""[^>]*>(.*?)</script>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);
        if (jsonLdMatch.Success)
        {
            try
            {
                var json = jsonLdMatch.Groups[1].Value.Trim();
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var root = doc.RootElement;

                // Flatten @graph if present
                System.Text.Json.JsonElement graphRoot = root;
                if (root.TryGetProperty("@graph", out var graph) && graph.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    foreach (var item in graph.EnumerateArray())
                    {
                        if (item.TryGetProperty("@type", out var type) &&
                            (type.GetString() == "Product" || type.GetString() == "MusicAlbum" || type.GetString() == "CreativeWork"))
                        {
                            graphRoot = item;
                            break;
                        }
                    }
                }

                if (graphRoot.TryGetProperty("name", out var name))
                {
                    var nameStr = name.GetString() ?? "";
                    nameStr = System.Net.WebUtility.HtmlDecode(nameStr);
                    if (!string.IsNullOrEmpty(nameStr))
                    {
                        if (isEng)
                            originalTitleRaw = nameStr;
                        else
                            parsedTitle = nameStr;
                    }
                }
                if (graphRoot.TryGetProperty("image", out var image))
                {
                    var imgStr = image.GetString() ?? "";
                    if (imgStr.StartsWith("//")) imgStr = "https:" + imgStr;
                    coverUrl = imgStr;
                }
                if (graphRoot.TryGetProperty("brand", out var brand) &&
                    brand.TryGetProperty("name", out var brandName))
                {
                    circle = System.Net.WebUtility.HtmlDecode(brandName.GetString() ?? "Unknown Circle");
                }
                if (graphRoot.TryGetProperty("keywords", out var keywords))
                {
                    var kwList = new List<string>();
                    if (keywords.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        var kwStr = keywords.GetString();
                        if (!string.IsNullOrEmpty(kwStr))
                        {
                            foreach (var kw in kwStr.Split(','))
                            {
                                var t = kw.Trim();
                                if (!string.IsNullOrEmpty(t)) kwList.Add(System.Net.WebUtility.HtmlDecode(t));
                            }
                        }
                    }
                    else if (keywords.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        foreach (var kw in keywords.EnumerateArray())
                        {
                            var s = kw.GetString();
                            if (!string.IsNullOrEmpty(s)) kwList.Add(System.Net.WebUtility.HtmlDecode(s.Trim()));
                        }
                    }
                    genre = string.Join(", ", kwList);
                }
            }
            catch { }
        }

        // Strategy 2: og:meta tags as secondary extraction
        if (string.IsNullOrEmpty(parsedTitle) && !isEng)
            parsedTitle = System.Net.WebUtility.HtmlDecode(ExtractMetaContent(html, "og:title"));
        if (string.IsNullOrEmpty(coverUrl))
        {
            var itempropMatch = Regex.Match(html, @"<meta\s+itemprop=""image""\s+content=""([^""]*)""", RegexOptions.IgnoreCase);
            if (itempropMatch.Success)
                coverUrl = itempropMatch.Groups[1].Value;
            else
                coverUrl = ExtractMetaContent(html, "og:image");

            if (coverUrl.StartsWith("//")) coverUrl = "https:" + coverUrl;
        }
        if (circle == "Unknown Circle")
        {
            var maker = ExtractMetaContent(html, "product:maker");
            if (!string.IsNullOrEmpty(maker)) circle = System.Net.WebUtility.HtmlDecode(maker);
        }

        // Strategy 3: Classic regex-based extraction for remaining fields
        if (string.IsNullOrEmpty(parsedTitle))
        {
            var titleMatch = Regex.Match(html, @"<h1[^>]*id=""work_name""[^>]*>([^<]+)</h1>", RegexOptions.IgnoreCase);
            if (titleMatch.Success)
                parsedTitle = System.Net.WebUtility.HtmlDecode(titleMatch.Groups[1].Value.Trim());
            else
                parsedTitle = "Unknown Title";
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
                if (coverUrl.StartsWith("//")) coverUrl = "https:" + coverUrl;
            }
        }

        if (string.IsNullOrEmpty(vaRaw))
        {
            var vaMatch = Regex.Match(html, @"<th[^>]*>\s*(?:Voice\s+Actor|Cast|声優|キャラクター|出演)\s*</th>\s*<td[^>]*>(.*?)</td>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (vaMatch.Success)
            {
                var vaNames = Regex.Matches(vaMatch.Groups[1].Value, @"<a[^>]+>([^<]+)</a>", RegexOptions.IgnoreCase);
                vaRaw = string.Join(", ", vaNames.Select(m => m.Groups[1].Value.Trim()));
                vaRaw = System.Net.WebUtility.HtmlDecode(vaRaw);
            }
        }

        if (ageRating == "SFW")
        {
            var ageMatch = Regex.Match(html, @"<th[^>]*>\s*(?:年齢指定|年齢制限|Age\s+Restriction|Age\s+rating)\s*</th>\s*<td[^>]*>(.*?)</td>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (ageMatch.Success)
            {
                var tdContent = ageMatch.Groups[1].Value;
                if (Regex.IsMatch(tdContent, @"(R-?18|18\+|ADL|18禁|18歳未満使用禁止)", RegexOptions.IgnoreCase))
                    ageRating = "NSFW";
            }
        }

        if (string.IsNullOrEmpty(genre))
        {
            var genreMatch = Regex.Match(html, @"<th[^>]*>\s*(?:ジャンル|Genre|Genre\(s\)|分類|タグ|カテゴリ|カテゴリー)[：:\s]*</th>\s*<td[^>]*>(.*?)</td>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (genreMatch.Success)
            {
                var genreNames = Regex.Matches(genreMatch.Groups[1].Value, @"<a[^>]+>([^<]+)</a>", RegexOptions.IgnoreCase);
                genre = string.Join(", ", genreNames.Select(m => System.Net.WebUtility.HtmlDecode(m.Groups[1].Value.Trim())));
            }
        }

        bool titleFound = !string.IsNullOrEmpty(parsedTitle) && parsedTitle != "Unknown Title";
        bool circleFound = circle != "Unknown Circle";
        bool coverFound = !string.IsNullOrEmpty(coverUrl);

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
            CoverFound = coverFound
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
            if (coverUrl.StartsWith("//"))
            {
                coverUrl = "https:" + coverUrl;
            }

            string ageRating = "SFW";
            if (element.TryGetProperty("age_category_string", out var ageProp))
            {
                var ageStr = ageProp.GetString();
                if (string.Equals(ageStr, "adult", StringComparison.OrdinalIgnoreCase))
                {
                    ageRating = "NSFW";
                }
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
