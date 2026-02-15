using AlliterativeWidget.Models;
using AlliterativeWidget.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AlliterativeWidget.ViewModels;

public partial class WidgetViewModel : ObservableObject
{
    private readonly IContentEngine _contentEngine;
    private readonly ISchedulerService _scheduler;

    [ObservableProperty]
    private string _headline = "";

    [ObservableProperty]
    private string _punchline = "";

    [ObservableProperty]
    private bool _isVisible = true;

    public event EventHandler<DailyContent>? ContentRefreshed;

    public WidgetViewModel(IContentEngine contentEngine, ISchedulerService scheduler)
    {
        _contentEngine = contentEngine;
        _scheduler = scheduler;

        _scheduler.RefreshRequired += OnRefreshRequired;
    }

    public void Initialize()
    {
        RefreshContent();
        _scheduler.Start();
    }

    public void RefreshContent()
    {
        var content = _contentEngine.GenerateContent(DateTime.Today);
        Headline = content.Headline;
        Punchline = content.Punchline;
        ContentRefreshed?.Invoke(this, content);
    }

    private void OnRefreshRequired(object? sender, EventArgs e)
    {
        RefreshContent();
    }

    public void Cleanup()
    {
        _scheduler.RefreshRequired -= OnRefreshRequired;
        _scheduler.Stop();
    }
}
