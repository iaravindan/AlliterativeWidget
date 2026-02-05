using System.Net.Http.Headers;
using System.Text.Json;
using AlliterativeWidget.Models;

namespace AlliterativeWidget.Services;

public class GymService : IGymService, IDisposable
{
    private readonly GymConfig _config;
    private readonly HttpClient _httpClient;
    private readonly TimeSpan _cacheLifetime = TimeSpan.FromMinutes(15);
    private readonly TimeSpan _staleCacheLifetime = TimeSpan.FromHours(24);
    private readonly TimeSpan _requestTimeout = TimeSpan.FromSeconds(30);

    private GymSummaryResponse? _cachedResponse;
    private DateTime? _cacheTime;
    private int _consecutiveFailures;
    private readonly int _maxRetryDelay = 300; // 5 minutes max backoff

    public DateTime? LastFetchTime => _cacheTime;
    public bool IsLoading { get; private set; }
    public string? LastError { get; private set; }

    public GymService(GymConfig config)
    {
        _config = config;
        _httpClient = new HttpClient
        {
            Timeout = _requestTimeout
        };
    }

    public async Task<GymSummaryResponse?> GetSummaryAsync()
    {
        // Return cached response if still fresh
        if (_cachedResponse != null && _cacheTime.HasValue)
        {
            var cacheAge = DateTime.UtcNow - _cacheTime.Value;
            if (cacheAge < _cacheLifetime)
            {
                return _cachedResponse;
            }
        }

        // Try to fetch fresh data
        var result = await FetchFromApiAsync();

        // If fetch failed but we have stale cache, use it
        if (result == null && _cachedResponse != null && _cacheTime.HasValue)
        {
            var cacheAge = DateTime.UtcNow - _cacheTime.Value;
            if (cacheAge < _staleCacheLifetime)
            {
                return _cachedResponse;
            }
        }

        return result;
    }

    public async Task<GymSummaryResponse?> RefreshSummaryAsync()
    {
        // Force a fresh fetch
        _consecutiveFailures = 0; // Reset backoff on manual refresh
        return await FetchFromApiAsync();
    }

    private async Task<GymSummaryResponse?> FetchFromApiAsync()
    {
        if (string.IsNullOrEmpty(_config.ApiBaseUrl) || string.IsNullOrEmpty(_config.ReadToken))
        {
            LastError = "Gym API not configured";
            return null;
        }

        // Apply exponential backoff on consecutive failures
        if (_consecutiveFailures > 0)
        {
            var backoffSeconds = Math.Min(
                Math.Pow(2, _consecutiveFailures),
                _maxRetryDelay
            );
            // In a real implementation, we'd track time since last attempt
            // For simplicity, we'll just proceed with the attempt
        }

        IsLoading = true;
        LastError = null;

        try
        {
            var url = BuildSummaryUrl();
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("X-Auth-Token", _config.ReadToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                _consecutiveFailures++;
                LastError = $"API returned {(int)response.StatusCode}";
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var summary = JsonSerializer.Deserialize<GymSummaryResponse>(content, options);

            if (summary != null)
            {
                _cachedResponse = summary;
                _cacheTime = DateTime.UtcNow;
                _consecutiveFailures = 0;
                LastError = null;
            }

            return summary;
        }
        catch (TaskCanceledException)
        {
            _consecutiveFailures++;
            LastError = "Request timed out";
            return null;
        }
        catch (HttpRequestException ex)
        {
            _consecutiveFailures++;
            LastError = $"Network error: {ex.Message}";
            return null;
        }
        catch (JsonException ex)
        {
            _consecutiveFailures++;
            LastError = $"Invalid response: {ex.Message}";
            return null;
        }
        catch (Exception ex)
        {
            _consecutiveFailures++;
            LastError = $"Unexpected error: {ex.Message}";
            return null;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private string BuildSummaryUrl()
    {
        var baseUrl = _config.ApiBaseUrl.TrimEnd('/');
        var mode = _config.TargetPeriod;
        var target = _config.TargetVisits;

        // If a start date is configured, pass it to the API so it builds
        // the heatmap from that date through end of year
        if (!string.IsNullOrEmpty(_config.HeatmapStartDate))
        {
            return $"{baseUrl}/gym/summary?mode={mode}&target={target}&start={_config.HeatmapStartDate}";
        }

        var weeks = Math.Clamp(_config.HeatmapWeeks, 12, 52);
        return $"{baseUrl}/gym/summary?mode={mode}&weeks={weeks}&target={target}";
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        GC.SuppressFinalize(this);
    }
}
