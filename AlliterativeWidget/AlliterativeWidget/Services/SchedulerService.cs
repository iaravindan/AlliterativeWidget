using AlliterativeWidget.Helpers;
using Microsoft.UI.Dispatching;

namespace AlliterativeWidget.Services;

public class SchedulerService : ISchedulerService
{
    public event EventHandler? RefreshRequired;
    public event EventHandler? PeriodicRefresh;

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

        PeriodicRefresh?.Invoke(this, EventArgs.Empty);
    }

    private void HandleDayChange()
    {
        _lastKnownDailyKey = GetCurrentDailyKey();

        // Always refresh content on day change (including weekends)
        RefreshRequired?.Invoke(this, EventArgs.Empty);
    }
}
