namespace Duckit.Search;

/// <summary>
/// Search result obtained by the search engine.
/// </summary>
public struct Result
{
    /// <summary>
    /// The title of the search result.
    /// </summary>
    public string? Title { get; set; }
    /// <summary>
    /// The URL of the search result.
    /// </summary>
    public string? Url { get; set; }
    /// <summary>
    /// The displayed URL of the search result.
    /// </summary>
    public string? DisplayUrl { get; set; }
    /// <summary>
    /// A short description or snippet of the content.
    /// </summary>
    public string? Snippet { get; set; }
    /// <summary>
    /// The date associated with the search result.
    /// </summary>
    public string? Date { get; set; }
}

public interface ISearchEngine
{
    Task<List<Result>> SearchAsync(string query, CancellationToken cancellationToken);
}