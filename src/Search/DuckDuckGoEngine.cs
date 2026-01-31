using System.Net;
using System.Text;
using HtmlAgilityPack;
using Serilog;

namespace Duckit.Search;

public class DuckDuckGoEngine : ISearchEngine
{
    private const string DdgHtmlUrl = "https://html.duckduckgo.com/html/";
    private HttpClient _httpClient;

    public DuckDuckGoEngine()
    {
        _httpClient = new();
    }

    public DuckDuckGoEngine(HttpClient client)
    {
        _httpClient = client;
    }

    public Task<List<Result>> SearchAsync(string query, CancellationToken ct)
        => SafeSearch(query);

    /// <summary>
    /// Performs a POST request to DuckDuckGo's HTML endpoint.
    /// </summary>
    private async Task<List<Result>> SearchDdg(string query)
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
            var response = await _httpClient.PostAsync(DdgHtmlUrl, content);
            Log.Information("Received response: {StatusCode} {ReasonPhrase}", (int)response.StatusCode, response.ReasonPhrase);

            if (!response.IsSuccessStatusCode)
                throw new Exception($"Bad response: {(int)response.StatusCode} {response.ReasonPhrase}");

            var responseBody = await response.Content.ReadAsStringAsync();

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

    /// <summary>
    /// Parse the HTML content to extract search results.
    /// </summary>
    private static List<Result> ParseHtml(Stream htmlStream)
    {
        List<Result> results = [];
        try
        {
            var doc = new HtmlDocument();
            doc.Load(htmlStream);

            // Select nodes that represent search results
            var nodes = doc.DocumentNode.SelectNodes("//div[contains(@class, 'links_main') and contains(@class, 'result__body')]");
            if (nodes == null)
            {
                Log.Warning("No results found in the HTML response.");
                return results;
            }

            foreach (var node in nodes)
            {
                var result = new Result();

                // Extract the title from an <h2> element
                var h2Node = node.SelectSingleNode(".//h2[contains(@class, 'result__title')]");
                if (h2Node != null)
                {
                    var titleLink = h2Node.SelectSingleNode(".//a");
                    if (titleLink != null)
                        result.Title = WebUtility.HtmlDecode(titleLink.InnerText.Trim());
                }

                // Extract URL, DisplayUrl, and snippet.
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

                // Extract Date if available
                var dateNode = node.SelectSingleNode(".//div[contains(@class, 'result__extras')]//span[not(contains(@class, 'result__icon')) and not(contains(@class, 'result__url'))]");
                if (dateNode != null)
                {
                    // Clean up the date string (remove leading/trailing spaces and the HTML entity for space)
                    var dateText = dateNode.InnerText.Replace("&nbsp;", "").Trim();
                    if (!string.IsNullOrWhiteSpace(dateText))
                    {
                        // TODO: Attempt to parse the date. The format in the HTML looks like RFC 3339, so
                        // "2025-06-07T00:00:00.0000000", or just "2025-06-07" if the time part is omitted in some cases.
                        // We can try to parse it as DateTime and then format it nicely, or just keep the string. For
                        // now, let's just keep the string after cleaning it up.
                        result.Date = dateText;
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


    /// <summary>
    /// Wraps the search call in simple error handling: if an exception is thrown, it's logged as an error and returns an empty list.
    /// </summary>
    private async Task<List<Result>> SafeSearch(string query)
    {
        try
        {
            return await SearchDdg(query);
        }
        catch (Exception ex)
        {
            Log.Error("Error while searching for \"{Query}\": {ExMessage}", query, ex.Message);
            return [];
        }
    }
}