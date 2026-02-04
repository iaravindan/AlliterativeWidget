using Microsoft.UI.Xaml.Controls;

namespace AlliterativeWidget.Services;

public class TypewriterService
{
    private CancellationTokenSource? _cts;

    public async Task AnimateTextAsync(TextBlock target, string text, int delayMs = 30)
    {
        // Cancel any ongoing animation
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        target.Text = "";

        try
        {
            foreach (char c in text)
            {
                if (token.IsCancellationRequested)
                    break;

                target.Text += c;
                await Task.Delay(delayMs, token);
            }
        }
        catch (TaskCanceledException)
        {
            // Animation was cancelled, set final text
            target.Text = text;
        }
    }

    public void SetTextImmediate(TextBlock target, string text)
    {
        _cts?.Cancel();
        target.Text = text;
    }

    public void Cancel()
    {
        _cts?.Cancel();
    }
}
