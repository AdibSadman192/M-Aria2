using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MAria2.Core.Interfaces;
using MAria2.Core.Exceptions;

namespace MAria2.Presentation.WinUI.ViewModels;

public partial class DependencyUpdateManagementViewModel : ObservableObject
{
    private readonly IDependencyUpdateService _updateService;
    private readonly IDialogService _dialogService;
    private readonly ILoggingService _loggingService;

    [ObservableProperty]
    private ObservableCollection<DependencyUpdateViewModel> _availableUpdates;

    [ObservableProperty]
    private bool _isCheckingForUpdates;

    [ObservableProperty]
    private bool _hasUpdates;

    public DependencyUpdateManagementViewModel(
        IDependencyUpdateService updateService,
        IDialogService dialogService,
        ILoggingService loggingService)
    {
        _updateService = updateService;
        _dialogService = dialogService;
        _loggingService = loggingService;

        AvailableUpdates = new ObservableCollection<DependencyUpdateViewModel>();
    }

    [RelayCommand]
    private async Task CheckForUpdatesAsync()
    {
        try 
        {
            IsCheckingForUpdates = true;
            AvailableUpdates.Clear();

            var updates = await _updateService.CheckForUpdatesAsync();

            foreach (var update in updates)
            {
                var updateViewModel = new DependencyUpdateViewModel(
                    update, 
                    _updateService, 
                    _dialogService, 
                    _loggingService
                );
                AvailableUpdates.Add(updateViewModel);
            }

            HasUpdates = AvailableUpdates.Any();

            if (!HasUpdates)
            {
                await _dialogService.ShowInfoAsync(
                    "Update Check", 
                    "No updates are currently available."
                );
            }
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Update check failed: {ex.Message}");
            await _dialogService.ShowErrorAsync(
                "Update Check Failed", 
                "Could not check for updates. Please try again later."
            );
        }
        finally 
        {
            IsCheckingForUpdates = false;
        }
    }

    [RelayCommand]
    private async Task UpdateAllDependenciesAsync()
    {
        var updatesToApply = AvailableUpdates.ToList();
        var successfulUpdates = new List<string>();
        var failedUpdates = new List<(string Name, string Error)>();

        foreach (var updateVm in updatesToApply)
        {
            try 
            {
                var updateResult = await updateVm.UpdateDependencyAsync();
                
                if (updateResult)
                {
                    successfulUpdates.Add(updateVm.Name);
                }
                else 
                {
                    failedUpdates.Add((updateVm.Name, "Update failed"));
                }
            }
            catch (DependencyUpdateException ex)
            {
                failedUpdates.Add((updateVm.Name, ex.GetUserFriendlyMessage()));
            }
        }

        // Provide summary of updates
        await ShowUpdateSummaryAsync(successfulUpdates, failedUpdates);

        // Refresh available updates
        await CheckForUpdatesAsync();
    }

    private async Task ShowUpdateSummaryAsync(
        List<string> successfulUpdates, 
        List<(string Name, string Error)> failedUpdates)
    {
        var summaryMessage = new StringBuilder();

        if (successfulUpdates.Any())
        {
            summaryMessage.AppendLine("Successfully Updated:");
            foreach (var update in successfulUpdates)
            {
                summaryMessage.AppendLine($"- {update}");
            }
        }

        if (failedUpdates.Any())
        {
            summaryMessage.AppendLine("\nFailed Updates:");
            foreach (var (name, error) in failedUpdates)
            {
                summaryMessage.AppendLine($"- {name}: {error}");
            }
        }

        await _dialogService.ShowInfoAsync(
            "Update Summary", 
            summaryMessage.ToString()
        );
    }
}

public class DependencyUpdateViewModel : ObservableObject
{
    private readonly IDependencyUpdateService _updateService;
    private readonly IDialogService _dialogService;
    private readonly ILoggingService _loggingService;

    public string Name { get; }
    public string CurrentVersion { get; }
    public string NewVersion { get; }
    public string DownloadUrl { get; }

    [ObservableProperty]
    private bool _isUpdating;

    [ObservableProperty]
    private double _updateProgress;

    public DependencyUpdateViewModel(
        DependencyUpdateInfo updateInfo,
        IDependencyUpdateService updateService,
        IDialogService dialogService,
        ILoggingService loggingService)
    {
        Name = updateInfo.Name;
        CurrentVersion = updateInfo.Version;
        NewVersion = updateInfo.Version;
        DownloadUrl = updateInfo.DownloadUrl;

        _updateService = updateService;
        _dialogService = dialogService;
        _loggingService = loggingService;
    }

    [RelayCommand]
    public async Task<bool> UpdateDependencyAsync()
    {
        try 
        {
            IsUpdating = true;
            UpdateProgress = 0;

            var updateInfo = new DependencyUpdateInfo
            {
                Name = Name,
                Version = NewVersion,
                DownloadUrl = DownloadUrl
            };

            var result = await _updateService.UpdateDependencyAsync(updateInfo);

            UpdateProgress = 100;
            return result;
        }
        catch (DependencyUpdateException ex)
        {
            _loggingService.LogError($"Update failed for {Name}: {ex.Message}");
            
            await _dialogService.ShowErrorAsync(
                "Update Failed", 
                ex.GetUserFriendlyMessage()
            );

            return false;
        }
        finally 
        {
            IsUpdating = false;
        }
    }
}
