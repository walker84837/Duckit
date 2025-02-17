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
    private const int maxDescriptionLength = 20;

    static async Task Main(string[] args)
    {
        Console.Write("Enter your search query: ");
        var query = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(query))
        {
            Console.WriteLine("No search query entered.");
            return;
        }

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

    /// <summary>
    /// Parse the HTML content to extract search results.
    /// </summary>
    private static List<Result> ParseHTML(System.IO.Stream htmlStream)
    {
        var results = new List<Result>();
    
        var doc = new HtmlDocument();
        doc.Load(htmlStream);
    
        // every result from the page
        var nodes = doc.DocumentNode.SelectNodes("//div[contains(@class, 'links_main links_deep result__body')]");

        if (nodes == null)
        {
            Console.WriteLine("No results found.");
            return Enumerable.Empty<Result>().ToList();
        }

        foreach (var node in nodes)
        {
            var result = new Result();
    
            // Extract the title from the <h2> tag
            var h2Node = node.SelectSingleNode(".//h2[contains(@class, 'result__title')]");
            if (h2Node != null)
            {
                var titleLink = h2Node.SelectSingleNode(".//a");
                if (titleLink != null)
                {
                    result.Title = WebUtility.HtmlDecode(titleLink.InnerText.Trim());
                }
            }
    
            // Extract the URL from the <a> tag
            var aNodes = node.SelectNodes(".//a");
            if (aNodes != null)
            {
                foreach (var a in aNodes)
                {
                    result.URL = a.GetAttributeValue("href", "");
                }
    
                // Extract the snippet from the <a> tag with class 'result__snippet'
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
        
        return results;
    }

    /// <summary>
    /// Abbreviates the snippet if it is too long.
    /// </summary>
    private static string AbbreviateSnippet(string snippet)
    {
        if (string.IsNullOrWhiteSpace(snippet))
            return snippet;

        var words = snippet.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length > maxDescriptionLength)
        {
            return string.Join(" ", words.Take(maxDescriptionLength)) + "...";
        }

        return snippet;
    }

    /// <summary>
    /// Prints the results in a formatted manner.
    /// </summary>
    private static void PrintFancyResults(List<Result> results, int maxResults)
    {
        string greenBold = "\u001b[1;32m";
        string cyanBold = "\u001b[1;36m";
        string reset = "\u001b[0m";

        Console.WriteLine($"{cyanBold}Search Results{reset}\n");

        // Limit results to maximum specified.
        if (maxResults > results.Count)
        {
            Console.WriteLine("Maximum number of results exceeded. Showing all results.");
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
