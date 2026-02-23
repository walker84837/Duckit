using System.Net;
using System.Text;
using System.Globalization;
using HtmlAgilityPack;
using Serilog;

namespace Duckit.Search;

public class DuckDuckGoEngine : ISearchEngine
{
    private const string DdgHtmlUrl = "https://html.duckduckgo.com/html/";
    private readonly HttpClient _httpClient;

    public DuckDuckGoEngine(HttpClient client) => _httpClient = client;

    public Task<List<Result>> SearchAsync(string query, CancellationToken ct) => SafeSearch(query, ct);

    private async Task<List<Result>> SearchDdg(string query, CancellationToken ct)
    {
        var formData = new Dictionary<string, string>
        {
            { "q", query },
            { "b", "" },
            { "kl", "" },
            { "df", "" }
        };
        var content = new FormUrlEncodedContent(formData);

        Log.Information("Sending POST request to: {DuckDuckGoUrl} with query: {Query}", DdgHtmlUrl, query);

        try
        {
            var response = await _httpClient.PostAsync(DdgHtmlUrl, content, ct);
            Log.Information("Received response: {StatusCode} {ReasonPhrase}", (int)response.StatusCode, response.ReasonPhrase);

            if (!response.IsSuccessStatusCode)
                throw new Exception($"Bad response: {(int)response.StatusCode} {response.ReasonPhrase}");

            var responseBody = await response.Content.ReadAsStringAsync(ct);

            await SaveHtmlIfRequestedAsync(responseBody);

            using var contentStream = new MemoryStream(Encoding.UTF8.GetBytes(responseBody));
            var results = ParseHtml(contentStream);
            Log.Information("Successfully parsed HTML response");
            return results;
        }
        catch (Exception ex)
        {
            Log.Error("Error during HTTP POST request: {Exception}", ex.Message);
            throw;
        }
    }

    private static List<Result> ParseHtml(Stream htmlStream)
    {
        List<Result> results = [];
        try
        {
            var doc = new HtmlDocument();
            doc.Load(htmlStream);

            var nodes = doc.DocumentNode.SelectNodes("//div[contains(@class, 'links_main') and contains(@class, 'result__body')]");
            if (nodes == null)
            {
                Log.Warning("No results found in the HTML response.");
                return results;
            }

            foreach (var node in nodes)
            {
                var result = new Result();

                result.ResultType = GetResultType(node);

                var h2Node = node.SelectSingleNode(".//h2[contains(@class, 'result__title')]");
                if (h2Node != null)
                {
                    var titleLink = h2Node.SelectSingleNode(".//a");
                    if (titleLink != null)
                        result.Title = WebUtility.HtmlDecode(titleLink.InnerText.Trim());
                }

                var urlLink = node.SelectSingleNode(".//a[contains(@class, 'result__a')]");
                if (urlLink != null)
                {
                    result.Url = urlLink.GetAttributeValue("href", "");
                }

                var displayUrlNode = node.SelectSingleNode(".//a[contains(@class, 'result__url')]");
                if (displayUrlNode != null)
                {
                    result.DisplayUrl = WebUtility.HtmlDecode(displayUrlNode.InnerText.Trim());
                }

                var snippetNode = node.SelectSingleNode(".//a[contains(@class, 'result__snippet')]");
                if (snippetNode != null)
                {
                    result.Snippet = WebUtility.HtmlDecode(snippetNode.InnerText.Trim());
                }

                result.FaviconUrl = ExtractFavicon(node);

                var dateNode = node.SelectSingleNode(".//div[contains(@class, 'result__extras')]//span[not(contains(@class, 'result__icon')) and not(contains(@class, 'result__url'))]");
                if (dateNode != null)
                {
                    var dateText = dateNode.InnerText.Replace("&nbsp;", "").Trim();
                    if (!string.IsNullOrWhiteSpace(dateText))
                    {
                        string[] formats =
                        [
                            "yyyy-MM-ddTHH:mm:ss.fffffff",
                            "yyyy-MM-ddTHH:mm:ss.fffffffK",
                            "yyyy-MM-ddTHH:mm:ss",
                            "yyyy-MM-ddTHH:mm:ssK",
                            "yyyy-MM-dd"
                        ];

                        if (DateTime.TryParseExact(dateText, formats, null, DateTimeStyles.None, out var parsedDate))
                        {
                            result.Date = parsedDate;
                        }
                        else
                        {
                            Log.Warning("Failed to parse date string: {DateText}", dateText);
                            result.Date = null;
                        }
                    }
                }

                if (!string.IsNullOrEmpty(result.Url))
                    results.Add(result);
            }
        }
        catch (Exception ex)
        {
            Log.Error("Error parsing HTML: {Exception}", ex.Message);
        }
        return results;
    }

    private static string? GetResultType(HtmlNode node)
    {
        var classAttr = node.GetAttributeValue("class", "");
        if (string.IsNullOrEmpty(classAttr))
            return "web";

        if (classAttr.Contains("news-result") || classAttr.Contains("result--news"))
            return "news";
        if (classAttr.Contains("image-result") || classAttr.Contains("result--images"))
            return "image";
        if (classAttr.Contains("video-result") || classAttr.Contains("result--videos"))
            return "video";
        if (classAttr.Contains("ad-result") || classAttr.Contains("result--ad"))
            return "ad";

        return "web";
    }

    private static string? ExtractFavicon(HtmlNode node)
    {
        var faviconNode = node.SelectSingleNode(".//img[contains(@class, 'result__icon__img')]");
        if (faviconNode == null) return null;

        var faviconUrl = faviconNode.GetAttributeValue("src", "")
                         ?? faviconNode.GetAttributeValue("data-src", "");

        return string.IsNullOrEmpty(faviconUrl) ? null : faviconUrl;
    }

    private async Task<List<Result>> SafeSearch(string query, CancellationToken ct)
    {
        try
        {
            return await SearchDdg(query, ct);
        }
        catch (Exception ex)
        {
            Log.Error("Error while searching for \"{Query}\": {ExMessage}", query, ex.Message);
            return [];
        }
    }

    private async Task SaveHtmlIfRequestedAsync(string html)
    {
        var env = Environment.GetEnvironmentVariable("SHOW_HTML");
        if (string.IsNullOrWhiteSpace(env))
            return;

        if (bool.TryParse(env, out var result))
        {
            if (result)
                Console.WriteLine(html);
            return;
        }

        try
        {
            await File.WriteAllTextAsync(env, html, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "SHOW_HTML environment variable requests HTML to be written to {Path}, but failed", env);
        }
    }
}
