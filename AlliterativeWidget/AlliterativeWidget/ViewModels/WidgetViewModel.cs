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
        _scheduler.VisibilityChanged += OnVisibilityChanged;
    }

    public void Initialize()
    {
        IsVisible = _scheduler.ShouldBeVisible();
        if (IsVisible)
        {
            RefreshContent();
        }
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

    private void OnVisibilityChanged(object? sender, bool isVisible)
    {
        IsVisible = isVisible;
        if (isVisible)
        {
            RefreshContent();
        }
    }

    public void Cleanup()
    {
        _scheduler.RefreshRequired -= OnRefreshRequired;
        _scheduler.VisibilityChanged -= OnVisibilityChanged;
        _scheduler.Stop();
    }
}
