using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MAria2.Presentation.WinUI.ViewModels;
using MAria2.Core.Interfaces;

namespace MAria2.Presentation.WinUI.Views;

public sealed partial class DependencyUpdateManagementPage : Window
{
    public DependencyUpdateManagementViewModel ViewModel { get; }

    public DependencyUpdateManagementPage(
        IDependencyUpdateService updateService,
        IDialogService dialogService,
        ILoggingService loggingService)
    {
        this.InitializeComponent();
        
        ViewModel = new DependencyUpdateManagementViewModel(
            updateService, 
            dialogService, 
            loggingService
        );

        DataContext = ViewModel;
    }

    // Optional: Add any additional page-specific logic here
    protected override async void OnActivated(WindowActivatedEventArgs args)
    {
        base.OnActivated(args);

        // Automatically check for updates when the page is activated
        await ViewModel.CheckForUpdatesCommand.ExecuteAsync(null);
    }
}
