using System.Text.Json.Serialization;

namespace AlliterativeWidget.Models;

public class WidgetConfig
{
    [JsonPropertyName("ui")]
    public UiConfig Ui { get; set; } = new();

    [JsonPropertyName("schedule")]
    public ScheduleConfig Schedule { get; set; } = new();

    [JsonPropertyName("rules")]
    public RulesConfig Rules { get; set; } = new();

    [JsonPropertyName("tone")]
    public ToneConfig Tone { get; set; } = new();

    [JsonPropertyName("content")]
    public ContentConfig Content { get; set; } = new();

    [JsonPropertyName("gym")]
    public GymConfig Gym { get; set; } = new();
}

public class UiConfig
{
    [JsonPropertyName("width")]
    public int Width { get; set; } = 420;

    [JsonPropertyName("height")]
    public int Height { get; set; } = 120;

    [JsonPropertyName("padding")]
    public int Padding { get; set; } = 20;

    [JsonPropertyName("corner_radius")]
    public int CornerRadius { get; set; } = 14;

    [JsonPropertyName("opacity")]
    public double Opacity { get; set; } = 0.85;

    [JsonPropertyName("auto_start")]
    public bool AutoStart { get; set; } = true;
}

public class ScheduleConfig
{
    [JsonPropertyName("active_days")]
    public List<string> ActiveDays { get; set; } = ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday"];

    [JsonPropertyName("refresh_time")]
    public string RefreshTime { get; set; } = "00:00";
}

public class RulesConfig
{
    [JsonPropertyName("headline_no_repeat_window")]
    public int HeadlineNoRepeatWindow { get; set; } = 8;

    [JsonPropertyName("punchline_no_repeat_days")]
    public int PunchlineNoRepeatDays { get; set; } = 75;
}

public class ToneConfig
{
    [JsonPropertyName("headline_style")]
    public string HeadlineStyle { get; set; } = "alliterative_weekday";

    [JsonPropertyName("punchline_style")]
    public string PunchlineStyle { get; set; } = "corporate_satire";
}

public class ContentConfig
{
    [JsonPropertyName("prefixes")]
    public PrefixesConfig Prefixes { get; set; } = new();

    [JsonPropertyName("punchlines")]
    public List<string> Punchlines { get; set; } = [];
}

public class PrefixesConfig
{
    [JsonPropertyName("M_prefixes")]
    public List<string> Monday { get; set; } = [];

    [JsonPropertyName("T_prefixes_tue")]
    public List<string> Tuesday { get; set; } = [];

    [JsonPropertyName("W_prefixes")]
    public List<string> Wednesday { get; set; } = [];

    [JsonPropertyName("T_prefixes_thu")]
    public List<string> Thursday { get; set; } = [];

    [JsonPropertyName("F_prefixes")]
    public List<string> Friday { get; set; } = [];

    public List<string> GetPrefixesForDay(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => Monday,
        DayOfWeek.Tuesday => Tuesday,
        DayOfWeek.Wednesday => Wednesday,
        DayOfWeek.Thursday => Thursday,
        DayOfWeek.Friday => Friday,
        _ => []
    };

    public string GetDayName(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => "Monday",
        DayOfWeek.Tuesday => "Tuesday",
        DayOfWeek.Wednesday => "Wednesday",
        DayOfWeek.Thursday => "Thursday",
        DayOfWeek.Friday => "Friday",
        _ => ""
    };
}

public class GymConfig
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = false;

    [JsonPropertyName("api_base_url")]
    public string ApiBaseUrl { get; set; } = "";

    [JsonPropertyName("read_token")]
    public string ReadToken { get; set; } = "";

    [JsonPropertyName("target_period")]
    public string TargetPeriod { get; set; } = "weekly";

    [JsonPropertyName("target_visits")]
    public int TargetVisits { get; set; } = 4;

    [JsonPropertyName("target_days")]
    public int TargetDays { get; set; } = 50;

    [JsonPropertyName("heatmap_weeks")]
    public int HeatmapWeeks { get; set; } = 12;

    [JsonPropertyName("heatmap_start_date")]
    public string HeatmapStartDate { get; set; } = "";
}
