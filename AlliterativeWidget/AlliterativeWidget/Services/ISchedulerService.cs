namespace AlliterativeWidget.Services;

public interface ISchedulerService
{
    event EventHandler? RefreshRequired;

    void Start();
    void Stop();
    string GetCurrentDailyKey();
}
