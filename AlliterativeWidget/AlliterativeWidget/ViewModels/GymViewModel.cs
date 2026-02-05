using AlliterativeWidget.Models;
using AlliterativeWidget.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace AlliterativeWidget.ViewModels;

public partial class GymViewModel : ObservableObject
{
    private readonly IGymService _gymService;
    private readonly GymConfig _config;

    // Colors matching the dark theme
    private static readonly Color VisitedColor = Color.FromArgb(255, 76, 175, 80);   // #4CAF50 Material Green
    private static readonly Color MissedColor = Color.FromArgb(255, 244, 67, 54);    // #F44336 Material Red
    private static readonly Color FutureColor = Color.FromArgb(255, 60, 60, 60);     // #3C3C3C Dark gray
    private static readonly Color ExcludedColor = Color.FromArgb(255, 40, 40, 40);   // #282828 Darker gray

    private static readonly Color OnTrackColor = Color.FromArgb(255, 76, 175, 80);   // #4CAF50 Green
    private static readonly Color WarningColor = Color.FromArgb(255, 224, 124, 75);  // #E07C4B Theme orange
    private static readonly Color BehindColor = Color.FromArgb(255, 244, 67, 54);    // #F44336 Red

    [ObservableProperty]
    private bool _isEnabled;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string _titleText = "Consistency Index";

    [ObservableProperty]
    private int _progressPercent;

    [ObservableProperty]
    private SolidColorBrush _progressBarColor = new(OnTrackColor);

    [ObservableProperty]
    private List<HeatmapRowViewModel> _heatmapRows = [];

    [ObservableProperty]
    private List<MonthLabelViewModel> _monthLabels = [];

    public event EventHandler? DataRefreshed;

    public GymViewModel(IGymService gymService, GymConfig config)
    {
        _gymService = gymService;
        _config = config;
        _isEnabled = config.Enabled;
    }

    public async Task InitializeAsync()
    {
        if (!_config.Enabled)
        {
            IsEnabled = false;
            return;
        }

        IsEnabled = true;
        await RefreshDataAsync();
    }

    public async Task RefreshDataAsync()
    {
        if (!IsEnabled) return;

        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var summary = await _gymService.GetSummaryAsync();
            if (summary != null)
            {
                UpdateFromSummary(summary);
            }
            else
            {
                ErrorMessage = _gymService.LastError ?? "Unable to load gym data";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            DataRefreshed?.Invoke(this, EventArgs.Empty);
        }
    }

    public async Task ForceRefreshAsync()
    {
        if (!IsEnabled) return;

        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var summary = await _gymService.RefreshSummaryAsync();
            if (summary != null)
            {
                UpdateFromSummary(summary);
            }
            else
            {
                ErrorMessage = _gymService.LastError ?? "Unable to refresh gym data";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            DataRefreshed?.Invoke(this, EventArgs.Empty);
        }
    }

    private void UpdateFromSummary(GymSummaryResponse summary)
    {
        // Compute progress against the overall target_days goal
        var totalVisitDays = summary.Stats.TotalVisits;
        var targetDays = _config.TargetDays;
        ProgressPercent = targetDays > 0
            ? Math.Clamp((int)Math.Round((double)totalVisitDays / targetDays * 100), 0, 100)
            : 0;

        TitleText = "Consistency Index";

        // Update progress bar color based on progress
        ProgressBarColor = new SolidColorBrush(GetProgressColor(ProgressPercent));

        // Filter heatmap to only include weeks from heatmap_start_date onward
        var startFilter = _config.HeatmapStartDate;
        var filteredGrid = summary.Heatmap.Grid;
        if (!string.IsNullOrEmpty(startFilter))
        {
            filteredGrid = filteredGrid
                .Where(w => string.Compare(w.WeekStart, startFilter, StringComparison.Ordinal) >= 0)
                .ToList();
        }

        // Build heatmap rows (Mon-Fri)
        var rows = new List<HeatmapRowViewModel>();
        for (int dayIndex = 0; dayIndex < 5; dayIndex++)
        {
            var dayOfWeek = dayIndex + 1; // 1=Monday, 5=Friday
            var cells = new List<HeatmapCellViewModel>();

            foreach (var week in filteredGrid)
            {
                var dayData = week.Days.FirstOrDefault(d => d.DayOfWeek == dayOfWeek);
                var color = GetStatusColor(dayData?.Status ?? "future");
                cells.Add(new HeatmapCellViewModel
                {
                    Date = dayData?.Date ?? "",
                    Status = dayData?.Status ?? "future",
                    Color = new SolidColorBrush(color)
                });
            }

            rows.Add(new HeatmapRowViewModel
            {
                DayOfWeek = dayOfWeek,
                DayName = GetDayName(dayOfWeek),
                Days = cells
            });
        }
        HeatmapRows = rows;

        // Build month labels from filtered grid
        var monthLabels = new List<MonthLabelViewModel>();
        string currentMonth = "";
        int currentMonthStartWeek = 0;
        int currentMonthWeeks = 0;

        for (int w = 0; w < filteredGrid.Count; w++)
        {
            var weekStart = filteredGrid[w].WeekStart;
            // Parse month from weekStart date string (YYYY-MM-DD)
            var monthStr = "";
            if (DateTime.TryParse(weekStart, out var date))
            {
                monthStr = date.ToString("MMM");
            }

            if (monthStr != currentMonth)
            {
                if (!string.IsNullOrEmpty(currentMonth) && currentMonthWeeks > 0)
                {
                    monthLabels.Add(new MonthLabelViewModel
                    {
                        Month = currentMonth,
                        WeekIndex = currentMonthStartWeek,
                        WeekSpan = currentMonthWeeks
                    });
                }
                currentMonth = monthStr;
                currentMonthStartWeek = w;
                currentMonthWeeks = 1;
            }
            else
            {
                currentMonthWeeks++;
            }
        }
        // Add the last month
        if (!string.IsNullOrEmpty(currentMonth) && currentMonthWeeks > 0)
        {
            monthLabels.Add(new MonthLabelViewModel
            {
                Month = currentMonth,
                WeekIndex = currentMonthStartWeek,
                WeekSpan = currentMonthWeeks
            });
        }

        MonthLabels = monthLabels;
        ErrorMessage = null;
    }

    private static Color GetStatusColor(string status) => status switch
    {
        "visit" => VisitedColor,
        "miss" => MissedColor,
        "future" => FutureColor,
        "excluded" => ExcludedColor,
        _ => FutureColor
    };

    private static Color GetProgressColor(int percent)
    {
        if (percent >= 50) return OnTrackColor;   // Green at 50%+
        return WarningColor;                       // Orange below 50%
    }

    private static string GetDayName(int dayOfWeek) => dayOfWeek switch
    {
        1 => "Mon",
        2 => "Tue",
        3 => "Wed",
        4 => "Thu",
        5 => "Fri",
        _ => ""
    };
}

public class HeatmapRowViewModel
{
    public int DayOfWeek { get; set; }
    public string DayName { get; set; } = "";
    public List<HeatmapCellViewModel> Days { get; set; } = [];
}

public class HeatmapCellViewModel
{
    public string Date { get; set; } = "";
    public string Status { get; set; } = "future";
    public SolidColorBrush Color { get; set; } = new(Colors.Gray);
}

public class MonthLabelViewModel
{
    public string Month { get; set; } = "";
    public int WeekIndex { get; set; }
    public int WeekSpan { get; set; }
}
