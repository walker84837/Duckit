using HtmlAgilityPack;
using Serilog;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System;
using Tomlyn.Model;
using Tomlyn;

namespace Duckit;

/// <summary>
/// Search result obtained by the search engine.
/// </summary>
public class Result
{
    public string? Title { get; set; }
    public string? URL { get; set; }
    public string? Snippet { get; set; }
}

/// <summary>
/// Configuration object reflecting our config options.
/// </summary>
public class BrowserConfig
{
    public List<string> Sites { get; set; } = new List<string>();
    public bool Repl { get; set; } = false;
    public string SearchEngine { get; set; } = "duckduckgo";
    public List<string> Subtopics { get; set; } = new List<string>();
}

class Program
{
    private const string ddgHTMLURL = "https://duckduckgo.com/html/";
    private const string browserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/74.0.3729.169 Safari/537.3";
    private const int maxDescriptionLength = 20;
    private static readonly string[] exitKeywords = new string[] { "exit", "quit", "q", "bye" };

    static async Task<int> Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .CreateLogger();

        // Define command-line options.
        var rootCommand = new RootCommand("Search for things on DuckDuckGo");
        var searchTermOption = new Option<string>(new[] { "--term", "-t" }, "The query to search for");
        var resultNumberOption = new Option<int>(new[] { "--results", "-r", "-res" }, () => 10, "Maximum number of results");

        // Command-line subtopic option (can override config subtopics)
        var subtopicOption = new Option<string[]>(new[] { "--subtopic", "-s", "-sub" }, "Subtopics to refine the search");
        var configOption = new Option<string>(new[] { "--config", "-c", "-conf" }, "Path to the config file");
        var interactiveOption = new Option<bool>(new[] { "--interactive", "-i", "-int" }, "Enable interactive mode");

        rootCommand.AddOption(searchTermOption);
        rootCommand.AddOption(configOption);
        rootCommand.AddOption(resultNumberOption);
        rootCommand.AddOption(interactiveOption);
        rootCommand.AddOption(subtopicOption);

        // Initialize a default config.
        BrowserConfig config = new BrowserConfig();

        rootCommand.SetHandler(async (query, configPath, maxResults, cliSubtopics, interactive) =>
        {
            // If a config file is provided, load and parse it.
            if (!string.IsNullOrWhiteSpace(configPath))
            {
                try
                {
                    config = LoadConfig(configPath);
                    Log.Information("Configuration loaded successfully.");
                }
                catch (Exception ex)
                {
                    Log.Error($"Failed to load config: {ex.Message}");
                }
            }

            // Enable interactive mode if set in config or via command-line.
            if (config.Repl || interactive)
            {
                await RunInteractiveMode(config, maxResults, query, cliSubtopics);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(query))
                {
                    Log.Warning("No search query entered.");
                    return;
                }
                await ProcessQuery(query, config, maxResults, cliSubtopics);
            }
        },
        searchTermOption, configOption, resultNumberOption, subtopicOption, interactiveOption);

        return await rootCommand.InvokeAsync(args);
    }

    /// <summary>
    /// REPL loop: repeatedly prompt the user for queries until "exit" is typed.
    /// </summary>
    private static async Task RunInteractiveMode(BrowserConfig config, int maxResults, string? initialQuery, string[]? cliSubtopics)
    {
        Console.WriteLine("Entering interactive mode. Type 'exit' to quit.");
        if (!string.IsNullOrWhiteSpace(initialQuery))
        {
            await ProcessQuery(initialQuery, config, maxResults, cliSubtopics);
        }
        while (true)
        {
            string input = ReadLine.Read("Search> ").Trim().ToLower();
            if (string.IsNullOrWhiteSpace(input) || exitKeywords.Contains(input))
            {
                break;
            }
            await ProcessQuery(input, config, maxResults, cliSubtopics);
        }
    }

    /// <summary>
    /// Processes a query: performs a base search then, if subtopics are provided, additional refined searches.
    /// </summary>
    private static async Task ProcessQuery(string query, BrowserConfig config, int maxResults, string[]? cliSubtopics)
    {
        // Command-line subtopics take precedence over config ones.
        List<string> subtopics = (cliSubtopics != null && cliSubtopics.Length > 0)
            ? cliSubtopics.ToList()
            : config.Subtopics;

        Log.Information($"Performing search for query: {query}");

        // Base search.
        var baseResults = await SearchDDG(query);
        if (config.Sites.Count > 0)
        {
            baseResults = FilterResultsBySites(baseResults, config.Sites);
        }
        PrintFancyResults(baseResults, maxResults);

        // If subtopics exist, run a refined search for each.
        if (subtopics.Count > 0)
        {
            foreach (var sub in subtopics)
            {
                string refinedQuery = $"{query} {sub}";
                Log.Information($"\nPerforming subtopic search for: {refinedQuery}");
                var subResults = await SearchDDG(refinedQuery);
                if (config.Sites.Count > 0)
                {
                    subResults = FilterResultsBySites(subResults, config.Sites);
                }
                PrintFancyResults(subResults, maxResults);
            }
        }
    }

    /// <summary>
    /// Filters results to include only those whose URL contains one of the allowed site strings.
    /// </summary>
    private static List<Result> FilterResultsBySites(List<Result> results, List<string> allowedSites)
    {
        return results.Where(r =>
        {
            if (string.IsNullOrEmpty(r.URL))
                return false;
            foreach (var site in allowedSites)
            {
                if (r.URL.Contains(site, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }).ToList();
    }

    /// <summary>
    /// Loads the config file provided in the arguments
    /// </summary>
    private static BrowserConfig LoadConfig(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("Config file not found.");

        var tomlText = File.ReadAllText(path);

        var model = Toml.ToModel(tomlText) as TomlTable;
        if (model == null)
            throw new Exception("Failed to parse TOML configuration.");

        if (!model.ContainsKey("browser"))
            throw new Exception("Browser configuration not found.");

        var browserTable = model["browser"] as TomlTable;
        if (browserTable == null)
            throw new Exception("Browser configuration is not a valid table.");

        var config = new BrowserConfig();

        if (browserTable.ContainsKey("sites") && browserTable["sites"] is TomlArray sitesArray)
        {
            foreach (var site in sitesArray)
            {
                config.Sites.Add(site.ToString());
            }
        }

        if (browserTable.ContainsKey("repl"))
        {
            config.Repl = Convert.ToBoolean(browserTable["repl"]);
        }

        if (browserTable.ContainsKey("search_engine"))
        {
            config.SearchEngine = browserTable["search_engine"].ToString();
            if (!config.SearchEngine.Equals("duckduckgo", StringComparison.OrdinalIgnoreCase))
            {
                Log.Warning($"Warning: Search engine {config.SearchEngine} not implemented. Defaulting to DuckDuckGo.");
                config.SearchEngine = "duckduckgo";
            }
        }

        if (browserTable.ContainsKey("subtopic") && browserTable["subtopic"] is TomlArray subtopicsArray)
        {
            foreach (var sub in subtopicsArray)
            {
                config.Subtopics.Add(sub.ToString());
            }
        }

        return config;
    }

    /// <summary>
    /// Performs a GET request to DuckDuckGo's HTML endpoint.
    /// </summary>
    private static async Task<List<Result>> SearchDDG(string query)
    {
        var queryParams = HttpUtility.ParseQueryString(string.Empty);
        queryParams["q"] = query;
        string searchUrl = $"{ddgHTMLURL}?{queryParams}";

        Log.Information($"Sending request to: {searchUrl}");

        using (var client = new HttpClient())
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd(browserAgent);
            HttpResponseMessage response = await client.GetAsync(searchUrl);
            Log.Information($"Received response: {(int)response.StatusCode} {response.ReasonPhrase}");

            if (!response.IsSuccessStatusCode)
                throw new Exception($"Bad response: {(int)response.StatusCode} {response.ReasonPhrase}");

            var contentStream = await response.Content.ReadAsStreamAsync();
            var results = ParseHTML(contentStream);
            Log.Information("Successfully parsed HTML response");
            return results;
        }
    }

    /// <summary>
    /// Parse the HTML content to extract search results.
    /// </summary>
    private static List<Result> ParseHTML(Stream htmlStream)
    {
        var results = new List<Result>();
        var doc = new HtmlDocument();
        doc.Load(htmlStream);

        // Select nodes that represent search results.
        var nodes = doc.DocumentNode.SelectNodes("//div[contains(@class, 'links_main') and contains(@class, 'result__body')]");
        if (nodes == null)
        {
            Console.WriteLine("No results found.");
            return results;
        }

        foreach (var node in nodes)
        {
            var result = new Result();

            // Extract the title from an <h2> element.
            var h2Node = node.SelectSingleNode(".//h2[contains(@class, 'result__title')]");
            if (h2Node != null)
            {
                var titleLink = h2Node.SelectSingleNode(".//a");
                if (titleLink != null)
                    result.Title = WebUtility.HtmlDecode(titleLink.InnerText.Trim());
            }

            // Extract URL and snippet.
            var aNodes = node.SelectNodes(".//a");
            if (aNodes != null)
            {
                foreach (var a in aNodes)
                    result.URL = a.GetAttributeValue("href", "");

                var snippetNode = aNodes.FirstOrDefault(n => n.GetAttributeValue("class", "").Contains("result__snippet"));
                if (snippetNode != null)
                    result.Snippet = WebUtility.HtmlDecode(snippetNode.InnerText.Trim());
            }

            if (!string.IsNullOrEmpty(result.URL))
                results.Add(result);
        }
        return results;
    }

    /// <summary>
    /// Abbreviates the snippet if it exceeds a maximum word count.
    /// </summary>
    private static string AbbreviateSnippet(string? snippet)
    {
        if (string.IsNullOrWhiteSpace(snippet))
        {
            return "";
        }

        var words = snippet.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length > maxDescriptionLength)
        {
            return string.Join(" ", words.Take(maxDescriptionLength)) + "...";
        }
        return snippet;
    }

    /// <summary>
    /// Displays results in a formatted, colorized way.
    /// </summary>
    private static void PrintFancyResults(List<Result> results, int maxResults)
    {
        const string cyanBold = "\e[1;36m";

        const string green = "\u001b[32m";
        const string cyan = "\u001b[36m";
        const string red = "\u001b[31m";
        const string yellow = "\u001b[33m";
        const string reset = "\u001b[0m";
        const string blue = "\u001b[34m";
        const string purple = "\u001b[35m";

        Console.WriteLine($"{cyanBold}Search results{reset}\n");

        if (results.Count == 0)
        {
            Console.WriteLine($"{red}No results to display.{reset}");
            return;
        }

        if (maxResults > results.Count)
        {
            Console.WriteLine($"{yellow}Maximum number of results exceeded.{reset} Showing all results.");
            maxResults = results.Count;
        }

        for (int i = 0; i < maxResults; i++)
        {
            var result = results[i];
            Console.WriteLine($"{blue}Title:{reset} {result.Title}");
            Console.WriteLine($"- {purple}URL:{reset} {result.URL}");
            Console.WriteLine($"- {green}Snippet:{reset} {AbbreviateSnippet(result.Snippet)}");
            Console.WriteLine();
        }
    }
}
