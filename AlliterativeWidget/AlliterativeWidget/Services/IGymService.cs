using AlliterativeWidget.Models;

namespace AlliterativeWidget.Services;

public interface IGymService
{
    /// <summary>
    /// Fetches the gym summary from the API, using cache when available
    /// </summary>
    /// <returns>The gym summary response, or null if unavailable</returns>
    Task<GymSummaryResponse?> GetSummaryAsync();

    /// <summary>
    /// Forces a refresh of the gym summary, bypassing the cache
    /// </summary>
    /// <returns>The gym summary response, or null if unavailable</returns>
    Task<GymSummaryResponse?> RefreshSummaryAsync();

    /// <summary>
    /// Gets the last fetch time for display purposes
    /// </summary>
    DateTime? LastFetchTime { get; }

    /// <summary>
    /// Indicates if the service is currently fetching data
    /// </summary>
    bool IsLoading { get; }

    /// <summary>
    /// Last error message if fetch failed
    /// </summary>
    string? LastError { get; }
}
