using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MAria2.Core.Models;
using MAria2.Core.Interfaces;

namespace MAria2.Presentation.WinUI.ViewModels;

public partial class UpdateSourcePluginManagementViewModel : ObservableObject
{
    private readonly IUpdateSourcePluginConfigurationService _configService;
    private readonly IDialogService _dialogService;
    private readonly ILoggingService _loggingService;

    [ObservableProperty]
    private ObservableCollection<UpdateSourcePluginConfigurationViewModel> _pluginConfigurations;

    [ObservableProperty]
    private UpdateSourcePluginConfigurationViewModel _selectedPlugin;

    [ObservableProperty]
    private bool _isLoading;

    public UpdateSourcePluginManagementViewModel(
        IUpdateSourcePluginConfigurationService configService,
        IDialogService dialogService,
        ILoggingService loggingService)
    {
        _configService = configService;
        _dialogService = dialogService;
        _loggingService = loggingService;

        PluginConfigurations = new ObservableCollection<UpdateSourcePluginConfigurationViewModel>();
        LoadPluginConfigurations();
    }

    private async void LoadPluginConfigurations()
    {
        try 
        {
            IsLoading = true;
            PluginConfigurations.Clear();

            var configurations = await _configService.GetConfiguredPluginsAsync();
            
            foreach (var config in configurations)
            {
                PluginConfigurations.Add(
                    new UpdateSourcePluginConfigurationViewModel(
                        config, 
                        _configService, 
                        _dialogService, 
                        _loggingService
                    )
                );
            }
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Failed to load plugin configurations: {ex.Message}");
            await _dialogService.ShowErrorAsync(
                "Load Error", 
                "Could not load update source plugin configurations."
            );
        }
        finally 
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task AddNewPluginAsync()
    {
        try 
        {
            var newConfig = new UpdateSourcePluginConfiguration
            {
                Id = Guid.NewGuid().ToString(),
                Name = "New Update Source",
                IsEnabled = true
            };

            var newPluginVm = new UpdateSourcePluginConfigurationViewModel(
                newConfig, 
                _configService, 
                _dialogService, 
                _loggingService
            );

            PluginConfigurations.Add(newPluginVm);
            SelectedPlugin = newPluginVm;
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Failed to add new plugin: {ex.Message}");
            await _dialogService.ShowErrorAsync(
                "Add Plugin Error", 
                "Could not create a new update source plugin."
            );
        }
    }

    [RelayCommand]
    private async Task SavePluginConfigurationsAsync()
    {
        try 
        {
            IsLoading = true;
            var configsToSave = PluginConfigurations
                .Select(vm => vm.Configuration)
                .ToList();

            foreach (var config in configsToSave)
            {
                await _configService.UpdatePluginConfigurationAsync(config);
            }

            await _dialogService.ShowSuccessAsync(
                "Save Successful", 
                "Update source plugin configurations saved."
            );
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Failed to save plugin configurations: {ex.Message}");
            await _dialogService.ShowErrorAsync(
                "Save Error", 
                "Could not save update source plugin configurations."
            );
        }
        finally 
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RemoveSelectedPluginAsync()
    {
        if (SelectedPlugin == null) return;

        try 
        {
            var confirmResult = await _dialogService.ShowConfirmationAsync(
                "Remove Plugin", 
                $"Are you sure you want to remove the update source '{SelectedPlugin.Name}'?"
            );

            if (confirmResult)
            {
                await _configService.RemovePluginConfigurationAsync(
                    SelectedPlugin.Configuration.Id
                );

                PluginConfigurations.Remove(SelectedPlugin);
                SelectedPlugin = null;
            }
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Failed to remove plugin: {ex.Message}");
            await _dialogService.ShowErrorAsync(
                "Remove Error", 
                "Could not remove the update source plugin."
            );
        }
    }

    [RelayCommand]
    private async Task TestSelectedPluginConnectionAsync()
    {
        if (SelectedPlugin == null) return;

        try 
        {
            var testResult = await _configService.TestUpdateSourceConnectionAsync(
                SelectedPlugin.Configuration
            );

            if (testResult)
            {
                await _dialogService.ShowSuccessAsync(
                    "Connection Test", 
                    $"Successfully connected to {SelectedPlugin.Name}."
                );
            }
            else 
            {
                await _dialogService.ShowErrorAsync(
                    "Connection Test", 
                    $"Failed to connect to {SelectedPlugin.Name}."
                );
            }
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Plugin connection test failed: {ex.Message}");
            await _dialogService.ShowErrorAsync(
                "Connection Error", 
                "Could not test the update source plugin connection."
            );
        }
    }
}

public class UpdateSourcePluginConfigurationViewModel : ObservableObject
{
    private readonly IUpdateSourcePluginConfigurationService _configService;
    private readonly IDialogService _dialogService;
    private readonly ILoggingService _loggingService;

    public UpdateSourcePluginConfiguration Configuration { get; }

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private string _pluginType;

    [ObservableProperty]
    private string _baseUrl;

    [ObservableProperty]
    private bool _isEnabled;

    [ObservableProperty]
    private int _priority;

    [ObservableProperty]
    private string _username;

    [ObservableProperty]
    private string _secret;

    public UpdateSourcePluginConfigurationViewModel(
        UpdateSourcePluginConfiguration configuration,
        IUpdateSourcePluginConfigurationService configService,
        IDialogService dialogService,
        ILoggingService loggingService)
    {
        Configuration = configuration;
        _configService = configService;
        _dialogService = dialogService;
        _loggingService = loggingService;

        // Map configuration to properties
        Name = configuration.Name;
        PluginType = configuration.PluginType;
        BaseUrl = configuration.BaseUrl;
        IsEnabled = configuration.IsEnabled;
        Priority = configuration.Priority;

        // Only set username if authentication exists
        if (configuration.Authentication != null)
        {
            Username = configuration.Authentication.Username;
        }
    }

    [RelayCommand]
    private async Task SaveConfigurationAsync()
    {
        try 
        {
            // Update configuration
            Configuration.Name = Name;
            Configuration.PluginType = PluginType;
            Configuration.BaseUrl = BaseUrl;
            Configuration.IsEnabled = IsEnabled;
            Configuration.Priority = Priority;

            // Update authentication if credentials provided
            if (!string.IsNullOrWhiteSpace(Username) || 
                !string.IsNullOrWhiteSpace(Secret))
            {
                Configuration.Authentication ??= new UpdateSourceAuthConfig();
                Configuration.Authentication.Username = Username;
                Configuration.Authentication.Secret = Secret;
            }

            // Validate and save
            await _configService.UpdatePluginConfigurationAsync(Configuration);

            await _dialogService.ShowSuccessAsync(
                "Save Successful", 
                $"Configuration for {Name} saved successfully."
            );
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Failed to save plugin configuration: {ex.Message}");
            await _dialogService.ShowErrorAsync(
                "Save Error", 
                "Could not save update source plugin configuration."
            );
        }
    }
}
