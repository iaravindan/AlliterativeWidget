using AlliterativeWidget.Helpers;
using AlliterativeWidget.Models;

namespace AlliterativeWidget.Services;

public class ContentEngine : IContentEngine
{
    private readonly IPersistenceService _persistence;
    private readonly Random _random = new();

    public ContentEngine(IPersistenceService persistence)
    {
        _persistence = persistence;
    }

    public DailyContent GenerateContent(DateTime date)
    {
        var config = _persistence.LoadConfig();
        var history = _persistence.LoadHistory();
        var dailyKey = DateTimeHelper.GetDailyKey(date);

        // If we already generated content for today, return cached version
        if (history.LastDailyKey == dailyKey)
        {
            // Content already generated, but we don't cache the actual content
            // So regenerate with same seed for consistency
        }

        var isWeekend = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
        var headline = GenerateHeadline(date, config, history);
        var punchline = isWeekend
            ? SelectWeekendPunchline(config)
            : SelectPunchline(config, history);

        // Update history
        history.LastDailyKey = dailyKey;
        _persistence.SaveHistory(history);

        return new DailyContent(headline, punchline, dailyKey);
    }

    private string GenerateHeadline(DateTime date, WidgetConfig config, WidgetHistory history)
    {
        var dayOfWeek = date.DayOfWeek;
        var prefixes = config.Content.Prefixes.GetPrefixesForDay(dayOfWeek);
        var dayName = config.Content.Prefixes.GetDayName(dayOfWeek);

        if (prefixes.Count == 0)
        {
            return $"Happy {dayName}";
        }

        var recentlyUsed = history.HeadlineHistory.GetQueueForDay(dayOfWeek);
        var available = prefixes.Where(p => !recentlyUsed.Contains(p)).ToList();

        // Relax constraint if all prefixes have been used recently
        if (available.Count == 0)
        {
            available = prefixes;
        }

        var selectedPrefix = available[_random.Next(available.Count)];

        // Update history
        history.HeadlineHistory.AddToDay(dayOfWeek, selectedPrefix, config.Rules.HeadlineNoRepeatWindow);

        return $"{selectedPrefix} {dayName}";
    }

    private string SelectWeekendPunchline(WidgetConfig config)
    {
        var punchlines = config.Content.WeekendPunchlines;
        if (punchlines.Count == 0)
        {
            return "No synergy required today.";
        }
        return punchlines[_random.Next(punchlines.Count)];
    }

    private string SelectPunchline(WidgetConfig config, WidgetHistory history)
    {
        var punchlines = config.Content.Punchlines;
        if (punchlines.Count == 0)
        {
            return "Have a productive day!";
        }

        var cutoffDate = DateTime.Today.AddDays(-config.Rules.PunchlineNoRepeatDays);
        var recentlyUsed = history.PunchlineHistory
            .Where(p => p.UsedDate > cutoffDate)
            .Select(p => p.Text)
            .ToHashSet();

        var available = punchlines.Where(p => !recentlyUsed.Contains(p)).ToList();

        // Relax constraint if all punchlines have been used recently
        if (available.Count == 0)
        {
            available = punchlines;
            // Clear old history to start fresh
            history.PunchlineHistory.Clear();
        }

        var selectedPunchline = available[_random.Next(available.Count)];

        // Update history
        history.PunchlineHistory.Add(new PunchlineEntry
        {
            Text = selectedPunchline,
            UsedDate = DateTime.Today
        });

        // Prune old entries
        history.PunchlineHistory.RemoveAll(p => p.UsedDate < cutoffDate);

        return selectedPunchline;
    }
}
