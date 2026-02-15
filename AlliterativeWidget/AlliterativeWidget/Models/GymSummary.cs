using System.Text.Json.Serialization;

namespace AlliterativeWidget.Models;

/// <summary>
/// API response from GET /gym/summary
/// </summary>
public class GymSummaryResponse
{
    [JsonPropertyName("currentPeriod")]
    public CurrentPeriodData CurrentPeriod { get; set; } = new();

    [JsonPropertyName("heatmap")]
    public HeatmapData Heatmap { get; set; } = new();

    [JsonPropertyName("stats")]
    public StatsData Stats { get; set; } = new();

    [JsonPropertyName("cycling")]
    public CyclingData? Cycling { get; set; }

    [JsonPropertyName("generatedAt")]
    public string GeneratedAt { get; set; } = "";
}

public class CurrentPeriodData
{
    [JsonPropertyName("label")]
    public string Label { get; set; } = "This Week";

    [JsonPropertyName("visits")]
    public int Visits { get; set; }

    [JsonPropertyName("target")]
    public int Target { get; set; }

    [JsonPropertyName("progressPercent")]
    public int ProgressPercent { get; set; }
}

public class HeatmapData
{
    [JsonPropertyName("weeks")]
    public int Weeks { get; set; }

    [JsonPropertyName("grid")]
    public List<WeekData> Grid { get; set; } = [];

    [JsonPropertyName("monthLabels")]
    public List<MonthLabel> MonthLabels { get; set; } = [];
}

public class WeekData
{
    [JsonPropertyName("weekStart")]
    public string WeekStart { get; set; } = "";

    [JsonPropertyName("days")]
    public List<DayData> Days { get; set; } = [];
}

public class DayData
{
    [JsonPropertyName("date")]
    public string Date { get; set; } = "";

    [JsonPropertyName("dayOfWeek")]
    public int DayOfWeek { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "future";
}

public class MonthLabel
{
    [JsonPropertyName("month")]
    public string Month { get; set; } = "";

    [JsonPropertyName("weekIndex")]
    public int WeekIndex { get; set; }

    [JsonPropertyName("weekSpan")]
    public int WeekSpan { get; set; }
}

public class StatsData
{
    [JsonPropertyName("totalVisits")]
    public int TotalVisits { get; set; }

    [JsonPropertyName("totalMinutes")]
    public int TotalMinutes { get; set; }

    [JsonPropertyName("currentStreak")]
    public int CurrentStreak { get; set; }

    [JsonPropertyName("longestStreak")]
    public int LongestStreak { get; set; }
}

public class CyclingData
{
    [JsonPropertyName("weeks")]
    public List<CyclingWeekData> Weeks { get; set; } = [];
}

public class CyclingWeekData
{
    [JsonPropertyName("weekStart")]
    public string WeekStart { get; set; } = "";

    [JsonPropertyName("hasRide")]
    public bool HasRide { get; set; }

    [JsonPropertyName("totalRides")]
    public int TotalRides { get; set; }
}
