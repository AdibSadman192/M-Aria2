using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MAria2.Presentation.WinUI.ViewModels;
using MAria2.Core.Interfaces;

namespace MAria2.Presentation.WinUI.Views;

public sealed partial class DependencyManagementPage : Window
{
    public DependencyManagementViewModel ViewModel { get; }

    public DependencyManagementPage(
        IDependencyVerificationService dependencyService,
        IDialogService dialogService,
        ILoggingService loggingService)
    {
        this.InitializeComponent();
        
        ViewModel = new DependencyManagementViewModel(
            dependencyService, 
            dialogService, 
            loggingService
        );

        DataContext = ViewModel;
    }
}
