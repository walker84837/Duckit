using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using HtmlAgilityPack;

namespace Browser;

public class Result
{
    public string Title { get; set; }
    public string URL { get; set; }
    public string Snippet { get; set; }
}

class Program
{
    private const string ddgHTMLURL = "https://duckduckgo.com/html/";

    static async Task Main(string[] args)
    {
        // Ask the user for a search query.
        Console.Write("Enter your search query: ");
        var query = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(query))
        {
            Console.WriteLine("No search query entered.");
            return;
        }

        // Ask the user how many results they want to see.
        Console.Write("How many results do you want to see? ");
        var nrInput = Console.ReadLine();
        if (!int.TryParse(nrInput.Trim(), out int maxResults) || maxResults <= 0)
        {
            Console.WriteLine("Invalid number of results. Please enter a valid positive integer.");
            return;
        }

        Console.WriteLine($"Performing search for query: {query}");
        try
        {
            var results = await SearchDDG(query);
            PrintFancyResults(results, maxResults);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error occurred: {ex.Message}");
        }
    }

    // Performs a GET request to DuckDuckGo HTML search for the given query.
    private static async Task<List<Result>> SearchDDG(string query)
    {
        // Build URL with the query string.
        var queryParams = HttpUtility.ParseQueryString(string.Empty);
        queryParams["q"] = query;
        string searchUrl = $"{ddgHTMLURL}?{queryParams}";

        Console.WriteLine($"Sending request to: {searchUrl}");

        using (var client = new HttpClient())
        {
            // Set a browser-like User-Agent header.
            const string browserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/85.0.4183.102 Safari/537.36";
            client.DefaultRequestHeaders.UserAgent.ParseAdd(browserAgent);
            HttpResponseMessage response = await client.GetAsync(searchUrl);
            Console.WriteLine($"Received response: {(int)response.StatusCode} {response.ReasonPhrase}");

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Bad response: {(int)response.StatusCode} {response.ReasonPhrase}");
            }

            var contentStream = await response.Content.ReadAsStreamAsync();
            var results = ParseHTML(contentStream);
            Console.WriteLine("Successfully parsed HTML response");
            return results;
        }
    }

    // Parse the HTML content to extract search results.
    private static List<Result> ParseHTML(System.IO.Stream htmlStream)
    {
        var results = new List<Result>();

        var doc = new HtmlDocument();
        doc.Load(htmlStream);

        var nodes = doc.DocumentNode.SelectNodes("//div[contains(@class, 'result__body')]");
        if (nodes != null)
        {
            foreach (var node in nodes)
            {
                var result = new Result();

                var h2Node = node.SelectSingleNode(".//h2[contains(@class, 'result__title')]");
                if (h2Node != null)
                {
                    result.Title = WebUtility.HtmlDecode(h2Node.InnerText.Trim());
                }

                var aNodes = node.SelectNodes(".//a");
                if (aNodes != null)
                {
                    foreach (var a in aNodes)
                    {
                        var hrefValue = a.GetAttributeValue("href", "");
                        if (hrefValue.StartsWith("//duckduckgo.com/l/?uddg="))
                        {
                            try
                            {
                                result.URL = ExtractFinalURL(hrefValue);
                                break;
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error extracting final URL: {ex.Message}");
                            }
                        }
                    }

                    var snippetNode = aNodes.FirstOrDefault(n => n.GetAttributeValue("class", "").Contains("result__snippet"));
                    if (snippetNode != null)
                    {
                        result.Snippet = WebUtility.HtmlDecode(snippetNode.InnerText.Trim());
                    }
                }

                if (!string.IsNullOrEmpty(result.URL))
                {
                    results.Add(result);
                }
            }
        }

        return results;
    }

    private static string ExtractFinalURL(string redirectURL)
    {
        var urlWithScheme = "https:" + redirectURL;
        var uri = new Uri(urlWithScheme);
        var queryParams = HttpUtility.ParseQueryString(uri.Query);
        var uddg = queryParams.Get("uddg");

        if (string.IsNullOrEmpty(uddg))
        {
            throw new Exception("No uddg parameter found in redirect URL");
        }

        var finalUrl = HttpUtility.UrlDecode(uddg);
        return finalUrl;
    }

    private static string AbbreviateSnippet(string snippet)
    {
        if (string.IsNullOrWhiteSpace(snippet))
            return snippet;

        var words = snippet.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length > 20)
        {
            return string.Join(" ", words.Take(20)) + " ...";
        }

        return snippet;
    }

    // Prints the results in a formatted manner.
    private static void PrintFancyResults(List<Result> results, int maxResults)
    {
        // ANSI escape codes for colors.
        string greenBold = "\u001b[1;32m";
        string cyanBold = "\u001b[1;36m";
        string reset = "\u001b[0m";

        Console.WriteLine($"{cyanBold}Search Results{reset}\n");

        // Limit results to maximum specified.
        if (maxResults > results.Count)
        {
            maxResults = results.Count;
        }

        for (int i = 0; i < maxResults; i++)
        {
            var result = results[i];
            Console.WriteLine($"{greenBold}Title:{reset}\t{result.Title}");
            Console.WriteLine($"{greenBold}URL:{reset}\t{result.URL}");
            Console.WriteLine($"{greenBold}Snippet:{reset}\t{AbbreviateSnippet(result.Snippet)}");
            Console.WriteLine();
        }
    }
}
