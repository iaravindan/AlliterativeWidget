using AlliterativeWidget.Helpers;
using Microsoft.UI.Dispatching;

namespace AlliterativeWidget.Services;

public class SchedulerService : ISchedulerService
{
    public event EventHandler? RefreshRequired;
    public event EventHandler<bool>? VisibilityChanged;

    private readonly DispatcherQueue _dispatcherQueue;
    private DispatcherQueueTimer? _midnightTimer;
    private DispatcherQueueTimer? _watchdogTimer;
    private string _lastKnownDailyKey = "";

    public SchedulerService(DispatcherQueue dispatcherQueue)
    {
        _dispatcherQueue = dispatcherQueue;
    }

    public void Start()
    {
        _lastKnownDailyKey = GetCurrentDailyKey();

        // Set up midnight timer
        ScheduleMidnightTimer();

        // Set up watchdog timer (every 5 minutes)
        _watchdogTimer = _dispatcherQueue.CreateTimer();
        _watchdogTimer.Interval = TimeSpan.FromMinutes(5);
        _watchdogTimer.Tick += OnWatchdogTick;
        _watchdogTimer.Start();
    }

    public void Stop()
    {
        _midnightTimer?.Stop();
        _watchdogTimer?.Stop();
    }

    public bool ShouldBeVisible() => DateTimeHelper.IsWorkday();

    public string GetCurrentDailyKey() => DateTimeHelper.GetTodayKey();

    private void ScheduleMidnightTimer()
    {
        _midnightTimer?.Stop();
        _midnightTimer = _dispatcherQueue.CreateTimer();

        var timeUntilMidnight = DateTimeHelper.TimeUntilMidnight();
        // Add a small buffer to ensure we're past midnight
        _midnightTimer.Interval = timeUntilMidnight.Add(TimeSpan.FromSeconds(1));
        _midnightTimer.IsRepeating = false;
        _midnightTimer.Tick += OnMidnightTick;
        _midnightTimer.Start();
    }

    private void OnMidnightTick(DispatcherQueueTimer sender, object args)
    {
        HandleDayChange();
        ScheduleMidnightTimer(); // Reschedule for next midnight
    }

    private void OnWatchdogTick(DispatcherQueueTimer sender, object args)
    {
        var currentKey = GetCurrentDailyKey();
        if (currentKey != _lastKnownDailyKey)
        {
            HandleDayChange();
        }
    }

    private void HandleDayChange()
    {
        var wasWorkday = IsWorkday(_lastKnownDailyKey);
        _lastKnownDailyKey = GetCurrentDailyKey();
        var isWorkday = ShouldBeVisible();

        if (wasWorkday != isWorkday)
        {
            VisibilityChanged?.Invoke(this, isWorkday);
        }

        if (isWorkday)
        {
            RefreshRequired?.Invoke(this, EventArgs.Empty);
        }
    }

    private static bool IsWorkday(string dailyKey)
    {
        if (DateTime.TryParse(dailyKey, out var date))
        {
            return DateTimeHelper.IsWorkday(date);
        }
        return false;
    }
}
