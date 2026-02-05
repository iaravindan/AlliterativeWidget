using AlliterativeWidget.Models;
using AlliterativeWidget.Services;
using AlliterativeWidget.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace AlliterativeWidget.Views;

public sealed partial class WidgetContent : UserControl
{
    private readonly TypewriterService _typewriter = new();
    private GymViewModel? _gymViewModel;

    public event EventHandler? WidgetLeftClicked;
    public event EventHandler? WidgetRightClicked;
    public event EventHandler? GymRefreshRequested;

    public WidgetContent()
    {
        InitializeComponent();
        AlliterativeTile.PointerPressed += OnAlliterativeTilePointerPressed;
        GymTile.RefreshRequested += OnGymRefreshRequested;
    }

    private void OnAlliterativeTilePointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var properties = e.GetCurrentPoint(AlliterativeTile).Properties;

        if (properties.IsRightButtonPressed)
        {
            WidgetRightClicked?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
        else if (properties.IsLeftButtonPressed)
        {
            WidgetLeftClicked?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
    }

    private void OnGymRefreshRequested(object? sender, EventArgs e)
    {
        GymRefreshRequested?.Invoke(this, EventArgs.Empty);
    }

    public async Task UpdateContentAsync(DailyContent content)
    {
        // Animate headline (30ms per character)
        await _typewriter.AnimateTextAsync(HeadlineText, content.Headline, 30);

        // Animate punchline (20ms per character - faster for longer text)
        await _typewriter.AnimateTextAsync(PunchlineText, content.Punchline, 20);
    }

    public void SetContentImmediate(string headline, string punchline)
    {
        HeadlineText.Text = headline;
        PunchlineText.Text = punchline;
    }

    public void SetGymViewModel(GymViewModel viewModel)
    {
        _gymViewModel = viewModel;
        GymTile.SetViewModel(viewModel);

        // Show/hide gym tile and divider based on enabled state
        var visible = viewModel.IsEnabled ? Visibility.Visible : Visibility.Collapsed;
        GymTile.Visibility = visible;
        Divider.Visibility = visible;
    }

    public void UpdateGymVisibility(bool isEnabled)
    {
        var visible = isEnabled ? Visibility.Visible : Visibility.Collapsed;
        GymTile.Visibility = visible;
        Divider.Visibility = visible;
    }

    public void Cleanup()
    {
        GymTile.Cleanup();
    }
}
