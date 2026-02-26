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
            // Animation was cancelled - do not set text here.
            // The next animation call already cleared target.Text = "" before this
            // catch block runs, so setting it here would corrupt the new animation.
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
