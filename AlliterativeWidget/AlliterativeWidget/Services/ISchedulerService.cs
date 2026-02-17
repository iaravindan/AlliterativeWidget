namespace AlliterativeWidget.Services;

public interface ISchedulerService
{
    event EventHandler? RefreshRequired;
    event EventHandler? PeriodicRefresh;

    void Start();
    void Stop();
    string GetCurrentDailyKey();
}
