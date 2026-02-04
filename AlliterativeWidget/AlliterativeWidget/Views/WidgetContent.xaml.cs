using AlliterativeWidget.Models;
using AlliterativeWidget.Services;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace AlliterativeWidget.Views;

public sealed partial class WidgetContent : UserControl
{
    private readonly TypewriterService _typewriter = new();

    public event EventHandler? WidgetLeftClicked;
    public event EventHandler? WidgetRightClicked;

    public WidgetContent()
    {
        InitializeComponent();
        PointerPressed += OnPointerPressed;
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var properties = e.GetCurrentPoint(this).Properties;

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
}
