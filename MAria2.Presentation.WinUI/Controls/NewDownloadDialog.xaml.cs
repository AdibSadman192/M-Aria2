using Microsoft.UI.Xaml.Controls;
using Microsoft.Win32;
using MAria2.Core.Enums;
using MAria2.Application.Services;

namespace MAria2.Presentation.WinUI.Controls;

public sealed partial class NewDownloadDialog : UserControl
{
    private readonly EngineManagerService _engineManagerService;

    public string DownloadUrl => UrlTextBox.Text;
    public string DestinationPath => DestinationPathTextBox.Text;
    public EngineType? SelectedEngine => 
        EngineSelector.SelectedItem is EngineType engine ? engine : null;

    public NewDownloadDialog(EngineManagerService engineManagerService)
    {
        InitializeComponent();
        _engineManagerService = engineManagerService;

        // Populate engine selector
        PopulateEngineSelector();
    }

    private void PopulateEngineSelector()
    {
        var supportedEngines = _engineManagerService.GetSupportedEngines();
        EngineSelector.ItemsSource = supportedEngines;
        
        // Set default selection if possible
        if (supportedEngines.Any())
        {
            EngineSelector.SelectedIndex = 0;
        }
    }

    private void BrowseButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        // TODO: Implement file save dialog for WinUI 3
        // This is a placeholder and will need to be replaced with WinUI 3 specific implementation
        var dialog = new SaveFileDialog
        {
            Title = "Select Download Destination",
            Filter = "All Files (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            DestinationPathTextBox.Text = dialog.FileName;
        }
    }

    public bool Validate()
    {
        // Validate URL
        if (string.IsNullOrWhiteSpace(DownloadUrl))
        {
            // Show error
            return false;
        }

        // Validate destination path
        if (string.IsNullOrWhiteSpace(DestinationPath))
        {
            // Show error
            return false;
        }

        return true;
    }
}
