using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MAria2.Presentation.WinUI.ViewModels;

namespace MAria2.Presentation.WinUI.Views;

public sealed partial class UpdateSourcePluginManagementPage : Window
{
    public UpdateSourcePluginManagementViewModel ViewModel { get; }

    public UpdateSourcePluginManagementPage(
        UpdateSourcePluginManagementViewModel viewModel)
    {
        this.InitializeComponent();
        ViewModel = viewModel;
        DataContext = ViewModel;
    }

    private void OnCloseButtonClicked(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
