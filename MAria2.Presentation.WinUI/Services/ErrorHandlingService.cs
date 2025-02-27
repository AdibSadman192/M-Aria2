using Microsoft.UI.Xaml.Controls;
using System.Diagnostics;

namespace MAria2.Presentation.WinUI.Services;

public class ErrorHandlingService
{
    public async Task ShowErrorDialogAsync(string title, string message, Exception exception = null)
    {
        // Log the full exception details
        LogException(exception);

        // Create and show error dialog
        var dialog = new ContentDialog
        {
            Title = title,
            Content = new StackPanel
            {
                Children =
                {
                    new TextBlock { Text = message, TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap },
                    new TextBlock 
                    { 
                        Text = exception?.Message, 
                        Foreground = Microsoft.UI.Xaml.Media.SolidColorBrush.Parse("#FF0000"),
                        Margin = new Microsoft.UI.Xaml.Thickness(0, 10, 0, 0),
                        TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap
                    }
                }
            },
            PrimaryButtonText = "OK",
            CloseButtonText = "Copy Error Details"
        };

        var result = await dialog.ShowAsync();

        // If user chooses to copy error details
        if (result == ContentDialogResult.Secondary && exception != null)
        {
            CopyErrorDetailsToClipboard(exception);
        }
    }

    private void LogException(Exception exception)
    {
        if (exception == null) return;

        // Log to system debug output
        Debug.WriteLine($"Error: {exception.Message}");
        Debug.WriteLine($"Stack Trace: {exception.StackTrace}");

        // TODO: Implement more robust logging (file, telemetry, etc.)
    }

    private void CopyErrorDetailsToClipboard(Exception exception)
    {
        var errorDetails = $"Error: {exception.Message}\n\nStack Trace:\n{exception.StackTrace}";
        
        // TODO: Implement cross-platform clipboard copy for WinUI 3
    }

    public void TrackEvent(string eventName, Dictionary<string, string> properties = null)
    {
        // TODO: Implement telemetry tracking
        // This could integrate with Application Insights or other telemetry services
        Debug.WriteLine($"Event Tracked: {eventName}");
        if (properties != null)
        {
            foreach (var prop in properties)
            {
                Debug.WriteLine($"  {prop.Key}: {prop.Value}");
            }
        }
    }
}
