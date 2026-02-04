using System.Text.Json.Serialization;

namespace AlliterativeWidget.Models;

public class WidgetHistory
{
    [JsonPropertyName("last_daily_key")]
    public string LastDailyKey { get; set; } = "";

    [JsonPropertyName("headline_history")]
    public HeadlineHistory HeadlineHistory { get; set; } = new();

    [JsonPropertyName("punchline_history")]
    public List<PunchlineEntry> PunchlineHistory { get; set; } = [];
}

public class HeadlineHistory
{
    [JsonPropertyName("monday")]
    public Queue<string> Monday { get; set; } = new();

    [JsonPropertyName("tuesday")]
    public Queue<string> Tuesday { get; set; } = new();

    [JsonPropertyName("wednesday")]
    public Queue<string> Wednesday { get; set; } = new();

    [JsonPropertyName("thursday")]
    public Queue<string> Thursday { get; set; } = new();

    [JsonPropertyName("friday")]
    public Queue<string> Friday { get; set; } = new();

    public Queue<string> GetQueueForDay(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => Monday,
        DayOfWeek.Tuesday => Tuesday,
        DayOfWeek.Wednesday => Wednesday,
        DayOfWeek.Thursday => Thursday,
        DayOfWeek.Friday => Friday,
        _ => new Queue<string>()
    };

    public void AddToDay(DayOfWeek day, string prefix, int maxSize)
    {
        var queue = GetQueueForDay(day);
        queue.Enqueue(prefix);
        while (queue.Count > maxSize)
        {
            queue.Dequeue();
        }
    }
}

public class PunchlineEntry
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = "";

    [JsonPropertyName("used_date")]
    public DateTime UsedDate { get; set; }
}
