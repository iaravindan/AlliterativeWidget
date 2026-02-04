namespace AlliterativeWidget.Helpers;

public static class DateTimeHelper
{
    public static string GetDailyKey(DateTime date) => date.ToString("yyyy-MM-dd");

    public static string GetTodayKey() => GetDailyKey(DateTime.Today);

    public static bool IsWorkday(DateTime date) =>
        date.DayOfWeek is >= DayOfWeek.Monday and <= DayOfWeek.Friday;

    public static bool IsWorkday() => IsWorkday(DateTime.Today);

    public static TimeSpan TimeUntilMidnight()
    {
        var now = DateTime.Now;
        var midnight = now.Date.AddDays(1);
        return midnight - now;
    }

    public static DateTime NextMidnight() => DateTime.Today.AddDays(1);
}
