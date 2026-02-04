namespace AlliterativeWidget.Services;

public interface ISchedulerService
{
    event EventHandler? RefreshRequired;
    event EventHandler<bool>? VisibilityChanged;

    void Start();
    void Stop();
    bool ShouldBeVisible();
    string GetCurrentDailyKey();
}
