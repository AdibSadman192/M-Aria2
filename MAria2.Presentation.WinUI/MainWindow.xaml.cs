using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MAria2.Presentation.WinUI.Pages;

namespace MAria2.Presentation.WinUI;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        
        // Set initial page
        MainContentFrame.Navigate(typeof(DownloadsPage));
    }

    private void MainNavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem selectedItem)
        {
            // Navigate to the selected page based on the Tag
            var pageType = selectedItem.Tag switch
            {
                "DownloadsPage" => typeof(DownloadsPage),
                "CompletedDownloadsPage" => typeof(CompletedDownloadsPage),
                "SettingsPage" => typeof(SettingsPage),
                _ => typeof(DownloadsPage)
            };

            MainContentFrame.Navigate(pageType);
        }
    }
}
