using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MAria2.Core.Interfaces;
using MAria2.Core;

namespace MAria2.Presentation.WinUI.ViewModels;

public partial class DependencyManagementViewModel : ObservableObject
{
    private readonly IDependencyVerificationService _dependencyService;
    private readonly IDialogService _dialogService;
    private readonly ILoggingService _loggingService;

    [ObservableProperty]
    private ObservableCollection<DependencyConfigViewModel> _dependencies;

    public DependencyManagementViewModel(
        IDependencyVerificationService dependencyService,
        IDialogService dialogService,
        ILoggingService loggingService)
    {
        _dependencyService = dependencyService;
        _dialogService = dialogService;
        _loggingService = loggingService;

        LoadDependencies();
    }

    private async void LoadDependencies()
    {
        try 
        {
            var verificationResult = await _dependencyService.VerifyDependenciesAsync();
            
            Dependencies = new ObservableCollection<DependencyConfigViewModel>(
                verificationResult.DependencyResults.Select(d => 
                    new DependencyConfigViewModel(
                        d.Key, 
                        d.Value, 
                        _dependencyService, 
                        _dialogService, 
                        _loggingService
                    )
                )
            );
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Failed to load dependencies: {ex.Message}");
            await _dialogService.ShowErrorAsync(
                "Dependency Loading Error", 
                "Could not load dependency configuration."
            );
        }
    }

    [RelayCommand]
    private async Task AddDependency()
    {
        var newDependency = new DependencyConfigViewModel(
            "New Dependency", 
            new DependencyVerificationInfo { IsValid = false },
            _dependencyService, 
            _dialogService, 
            _loggingService
        );

        Dependencies.Add(newDependency);
    }

    [RelayCommand]
    private async Task SaveConfiguration()
    {
        try 
        {
            var dependencyConfigs = Dependencies.Select(d => new DependencyConfig
            {
                Name = d.Name,
                RelativePath = d.RelativePath,
                ExpectedHash = d.ExpectedHash,
                SupportedArchitectures = d.SupportedArchitectures,
                RequiredRuntimeVersion = d.RequiredRuntimeVersion
            }).ToList();

            await _dependencyService.SaveDependencyConfigAsync(dependencyConfigs);

            await _dialogService.ShowSuccessAsync(
                "Configuration Saved", 
                "Dependency configuration has been successfully updated."
            );
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Failed to save dependency configuration: {ex.Message}");
            await _dialogService.ShowErrorAsync(
                "Save Error", 
                "Could not save dependency configuration."
            );
        }
    }
}

public class DependencyConfigViewModel : ObservableObject
{
    private readonly IDependencyVerificationService _dependencyService;
    private readonly IDialogService _dialogService;
    private readonly ILoggingService _loggingService;

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private string _relativePath;

    [ObservableProperty]
    private string _expectedHash;

    [ObservableProperty]
    private string[] _supportedArchitectures;

    [ObservableProperty]
    private string _requiredRuntimeVersion;

    [ObservableProperty]
    private bool _isValid;

    [ObservableProperty]
    private string _errorMessage;

    public DependencyConfigViewModel(
        string name, 
        DependencyVerificationInfo verificationInfo,
        IDependencyVerificationService dependencyService,
        IDialogService dialogService,
        ILoggingService loggingService)
    {
        Name = name;
        IsValid = verificationInfo.IsValid;
        ErrorMessage = verificationInfo.ErrorMessage;

        _dependencyService = dependencyService;
        _dialogService = dialogService;
        _loggingService = loggingService;
    }

    [RelayCommand]
    private async Task Verify()
    {
        try 
        {
            var dependencyConfig = new DependencyConfig
            {
                Name = Name,
                RelativePath = RelativePath,
                ExpectedHash = ExpectedHash,
                SupportedArchitectures = SupportedArchitectures,
                RequiredRuntimeVersion = RequiredRuntimeVersion
            };

            var verificationResult = await _dependencyService.VerifyDependenciesAsync();
            var dependencyInfo = verificationResult.DependencyResults[Name];

            IsValid = dependencyInfo.IsValid;
            ErrorMessage = dependencyInfo.ErrorMessage;

            await _dialogService.ShowInfoAsync(
                "Verification Result", 
                IsValid 
                    ? "Dependency verified successfully." 
                    : $"Verification failed: {ErrorMessage}"
            );
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Dependency verification failed: {ex.Message}");
            await _dialogService.ShowErrorAsync(
                "Verification Error", 
                "Could not verify dependency."
            );
        }
    }

    [RelayCommand]
    private async Task Update()
    {
        // Placeholder for dependency update logic
        await _dialogService.ShowInfoAsync(
            "Update Dependency", 
            "Dependency update functionality will be implemented in a future version."
        );
    }
}
