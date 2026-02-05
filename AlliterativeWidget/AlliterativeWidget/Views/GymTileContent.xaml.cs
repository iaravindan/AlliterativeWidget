using AlliterativeWidget.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace AlliterativeWidget.Views;

public sealed partial class GymTileContent : UserControl
{
    private GymViewModel? _viewModel;

    public event EventHandler? RefreshRequested;

    public GymTileContent()
    {
        this.InitializeComponent();
        this.PointerPressed += OnPointerPressed;
    }

    public void SetViewModel(GymViewModel viewModel)
    {
        _viewModel = viewModel;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _viewModel.DataRefreshed += OnDataRefreshed;
        UpdateUI();
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() => UpdateUI());
    }

    private void OnDataRefreshed(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(() => UpdateUI());
    }

    private void UpdateUI()
    {
        if (_viewModel == null) return;

        // Update title
        TitleText.Text = _viewModel.TitleText;

        // Update percentage label
        PercentLabel.Text = $"{_viewModel.ProgressPercent}%";
        PercentLabel.Foreground = _viewModel.ProgressBarColor;

        // Update progress bar — use the parent grid's column width
        UpdateProgressBar();

        // Update heatmap
        HeatmapControl.ItemsSource = _viewModel.HeatmapRows;

        // Update month labels
        UpdateMonthLabels();

        // Update loading state
        LoadingOverlay.Visibility = _viewModel.IsLoading ? Visibility.Visible : Visibility.Collapsed;

        // Update error state
        if (!string.IsNullOrEmpty(_viewModel.ErrorMessage))
        {
            ErrorText.Text = _viewModel.ErrorMessage;
            ErrorText.Visibility = Visibility.Visible;
        }
        else
        {
            ErrorText.Visibility = Visibility.Collapsed;
        }
    }

    private void UpdateProgressBar()
    {
        if (_viewModel == null) return;

        var progressPercent = Math.Clamp(_viewModel.ProgressPercent, 0, 100);
        ProgressFill.Background = _viewModel.ProgressBarColor;

        // Calculate available width: total control width minus padding and percentage label space
        var availableWidth = ActualWidth - 60; // Reserve space for "100%" label + gap
        if (availableWidth <= 0) availableWidth = 300; // Fallback before layout

        var progressWidth = availableWidth * progressPercent / 100.0;
        ProgressFill.Width = Math.Max(0, progressWidth);
    }

    private void UpdateMonthLabels()
    {
        if (_viewModel == null) return;

        const double cellWidth = 6;
        const double cellSpacing = 2;
        const double cellTotalWidth = cellWidth + cellSpacing;

        var labels = new List<MonthLabelDisplay>();
        foreach (var label in _viewModel.MonthLabels)
        {
            var width = label.WeekSpan * cellTotalWidth;

            labels.Add(new MonthLabelDisplay
            {
                Month = label.Month,
                Width = width,
            });
        }

        MonthLabelsControl.ItemsSource = labels;
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);

        if (point.Properties.IsLeftButtonPressed)
        {
            RefreshRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Cleanup()
    {
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel.DataRefreshed -= OnDataRefreshed;
        }
    }
}

internal class MonthLabelDisplay
{
    public string Month { get; set; } = "";
    public double Width { get; set; }
}
