using HtmlAgilityPack;
using Serilog;
using System.CommandLine;
using System.Net;
using System.Text.Json;
using System.Text;
using Tomlyn.Model;
using Tomlyn;

namespace Duckit;

/// <summary>
/// Search result obtained by the search engine.
/// </summary>
public class Result
{
    public string? Title { get; set; }
    public string? Url { get; set; }
    public string? DisplayUrl { get; set; }
    public string? Snippet { get; set; }
    public string? Date { get; set; }
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
    private const string DdgHtmlUrl = "https://html.duckduckgo.com/html/ ";
    private const string BrowserAgent = "Mozilla/5.0 (X11; Linux x86_64; rv:146.0) Gecko/20100101 Firefox/146.0";
    private const int MaxDescriptionLength = 20;
    private static readonly string[] ExitKeywords = ["exit", "quit", "q", "bye"];
    private static readonly HttpClient HttpClient = new();

    static Program()
    {
        HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd(BrowserAgent);
    }

    static async Task<int> Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .CreateLogger();

        // Command-line options
        var rootCommand = new RootCommand("Search for things on DuckDuckGo");
        var searchTermOption = new Option<string>(["--term", "-t"], "The query to search for");
        var resultNumberOption = new Option<int>(["--results", "-r", "-res"], () => 10, "Maximum number of results");
        var linksOnlyOption = new Option<bool>(["--links-only", "-l"], "Output only URLs of the search results.");

        // Command-line subtopic option (can override config subtopics)
        var subtopicOption = new Option<string[]>(["--subtopic", "-s", "-sub"], "Subtopics to refine the search");
        var configOption = new Option<string>(["--config", "-c", "-conf"], "Path to the config file");
        var interactiveOption = new Option<bool>(["--interactive", "-i", "-int"], "Enable interactive mode");

        rootCommand.AddOption(searchTermOption);
        rootCommand.AddOption(configOption);
        rootCommand.AddOption(resultNumberOption);
        rootCommand.AddOption(interactiveOption);
        rootCommand.AddOption(subtopicOption);
        rootCommand.AddOption(linksOnlyOption);

        // Initialize a default config
        var config = new BrowserConfig();

        rootCommand.SetHandler(async (query, configPath, maxResults, cliSubtopics, interactive, linksOnly) =>
        {
            // If a config file is provided, load and parse it
            if (!string.IsNullOrWhiteSpace(configPath))
            {
                try
                {
                    config = LoadConfig(configPath);
                    Log.Information("Configuration loaded successfully.");
                }
                catch (Exception ex)
                {
                    Log.Error("Failed to load config: {Message}", ex.Message);
                }
            }

            // Enable interactive mode if set in config or via command-line
            if (config.Repl || interactive)
            {
                await RunInteractiveMode(config, maxResults, query, cliSubtopics, linksOnly);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(query))
                {
                    Log.Warning("No search query entered.");
                    return;
                }
                await ProcessQuery(query, config, maxResults, cliSubtopics, linksOnly);
            }
        },
        searchTermOption, configOption, resultNumberOption, subtopicOption, interactiveOption, linksOnlyOption);

        return await rootCommand.InvokeAsync(args);
    }

    /// <summary>
    /// REPL loop: repeatedly prompt the user for queries until "exit" is typed
    /// </summary>
    private static async Task RunInteractiveMode(BrowserConfig config, int maxResults, string? initialQuery, string[]? cliSubtopics, bool linksOnly)
    {
        Console.WriteLine("Entering interactive mode. Type 'exit' to quit.");
        if (!string.IsNullOrWhiteSpace(initialQuery))
        {
            await ProcessQuery(initialQuery, config, maxResults, cliSubtopics, linksOnly);
        }
        while (true)
        {
            string input = ReadLine.Read("Search> ").Trim().ToLower();
            if (string.IsNullOrWhiteSpace(input) || ExitKeywords.Contains(input))
            {
                break;
            }
            await ProcessQuery(input, config, maxResults, cliSubtopics, linksOnly);
        }
    }

    /// <summary>
    /// Processes a query: performs a base search then, if subtopics are provided, additional refined searches.
    /// </summary>
    private static async Task ProcessQuery(string query, BrowserConfig config, int maxResults, string[]? cliSubtopics, bool linksOnly)
    {
        // Command-line subtopics take precedence over config ones
        var subtopics = cliSubtopics is { Length: > 0 }
            ? cliSubtopics.ToList()
            : config.Subtopics;

        Log.Information("Performing search for query: {Query}", query);

        // Base search
        var baseResults = await SafeSearch(query);
        if (config.Sites.Count > 0)
        {
            baseResults = FilterResultsBySites(baseResults, config.Sites);
        }
        PrintResults(baseResults, maxResults, linksOnly);

        // If subtopics exist, run a refined search for each
        if (subtopics.Count > 0)
        {
            foreach (var sub in subtopics)
            {
                var refinedQuery = $"{query} {sub}";
                Log.Information("Performing subtopic search for: {RefinedQuery}", refinedQuery);
                var subResults = await SafeSearch(refinedQuery);
                if (config.Sites.Count > 0)
                {
                    subResults = FilterResultsBySites(subResults, config.Sites);
                }
                PrintResults(subResults, maxResults, linksOnly);
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
            if (string.IsNullOrEmpty(r.Url))
                return false;

            foreach (var site in allowedSites)
            {
                if (r.Url.Contains(site, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }).ToList();
    }

    /// <summary>
    /// Loads the config file provided in the arguments.
    /// </summary>
    private static BrowserConfig LoadConfig(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("Config file not found.");

        var tomlText = File.ReadAllText(path);
        var model = Toml.ToModel(tomlText);
        if (model == null)
            throw new Exception("Failed to parse TOML configuration.");

        if (!model.ContainsKey("browser"))
            throw new Exception("Browser configuration not found.");

        if (model["browser"] is not TomlTable browserTable)
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
                Log.Warning("Warning: Search engine {SearchEngine} not implemented. Defaulting to DuckDuckGo.", config.SearchEngine);
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
    /// Wraps the search call in error handling.
    /// </summary>
    private static async Task<List<Result>> SafeSearch(string query)
    {
        try
        {
            return await SearchDDG(query);
        }
        catch (Exception ex)
        {
            Log.Error($"Error while searching for \"{query}\": {ex.Message}");
            return new List<Result>();
        }
    }

    /// <summary>
    /// Performs a POST request to DuckDuckGo's HTML endpoint.
    /// </summary>
    private static async Task<List<Result>> SearchDDG(string query)
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
            var response = await HttpClient.PostAsync(DdgHtmlUrl, content);
            Log.Information("Received response: {StatusCode} {ReasonPhrase}", (int)response.StatusCode, response.ReasonPhrase);

            if (!response.IsSuccessStatusCode)
                throw new Exception($"Bad response: {(int)response.StatusCode} {response.ReasonPhrase}");

            var responseBody = await response.Content.ReadAsStringAsync();

            using var contentStream = new MemoryStream(Encoding.UTF8.GetBytes(responseBody));
            var results = ParseHTML(contentStream);
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
    private static List<Result> ParseHTML(Stream htmlStream)
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
                        // Attempt to parse the date. The format in the HTML looks like RFC 3339, so
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
    /// Abbreviates the snippet if it exceeds a maximum word count.
    /// </summary>
    private static string AbbreviateSnippet(string? snippet)
    {
        if (string.IsNullOrWhiteSpace(snippet))
        {
            return "";
        }

        var words = snippet.Split([' '], StringSplitOptions.RemoveEmptyEntries);
        if (words.Length > MaxDescriptionLength)
        {
            return string.Join(" ", words.Take(MaxDescriptionLength)) + "...";
        }
        return snippet;
    }

    /// <summary>
    /// Displays results either as fancy, colorized output or plain output for redirection.
    /// If linksOnly is true, only the URLs are output.
    /// </summary>
    private static void PrintResults(List<Result> results, int maxResults, bool linksOnly)
    {
        var isRedirected = Console.IsOutputRedirected;
        if (linksOnly)
        {
            // Print only URLs.
            foreach (var result in results.Take(maxResults))
            {
                Console.WriteLine(result.Url);
            }
            return;
        }

        if (!isRedirected)
        {
            PrintFormattedLinks(results, maxResults);
        }
        else
        {
            PrintLinksAsJSON(results);
        }
    }

    private static void PrintFormattedLinks(List<Result> results, int maxResults)
    {
        const string cyanBold = "\e[1;36m";
        const string green = "\u001b[32m";
        const string red = "\u001b[31m";
        const string yellow = "\u001b[33m";
        const string reset = "\u001b[0m";
        const string blue = "\u001b[34m";
        const string purple = "\u001b[35m";
        const string bold = "\u001b[1m";

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

        // We need just up to maxResults results, not more.
        for (int i = 0; i < maxResults; i++)
        {
            var result = results[i];

            Console.WriteLine($"- {blue}{bold}{result.Title}{reset}:");
            Console.WriteLine($"  {purple}{result.Url}{reset}");
            if (!string.IsNullOrEmpty(result.Date))
            {
                Console.WriteLine($"  {yellow}Date:{reset} {result.Date.Trim()}");
            }
            Console.WriteLine($"  {green}{AbbreviateSnippet(result.Snippet)}{reset}");
            Console.WriteLine();
        }
    }

    private static void PrintLinksAsJSON(List<Result> results)
    {
        Console.WriteLine(JsonSerializer.Serialize(results));
    }
}
