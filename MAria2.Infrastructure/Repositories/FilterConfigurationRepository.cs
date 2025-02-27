using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using MAria2.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace MAria2.Infrastructure.Repositories
{
    public class FilterConfigurationRepository
    {
        private readonly string _configPath;
        private readonly ILogger<FilterConfigurationRepository> _logger;
        private const string CONFIG_FILENAME = "filter_configurations.json";

        public FilterConfigurationRepository(
            IPlatformAbstractionService platformService, 
            ILogger<FilterConfigurationRepository> logger)
        {
            _logger = logger;
            var downloadDir = platformService.GetDefaultDownloadDirectory();
            _configPath = Path.Combine(
                downloadDir, 
                ".maria2", 
                CONFIG_FILENAME
            );
        }

        public async Task<IEnumerable<FilterConfiguration>> GetAllConfigurationsAsync()
        {
            try 
            {
                if (!File.Exists(_configPath))
                    return Enumerable.Empty<FilterConfiguration>();

                var jsonContent = await File.ReadAllTextAsync(_configPath);
                return JsonSerializer.Deserialize<List<FilterConfiguration>>(jsonContent) 
                    ?? Enumerable.Empty<FilterConfiguration>();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error reading filter configurations: {ex.Message}");
                return Enumerable.Empty<FilterConfiguration>();
            }
        }

        public async Task SaveConfigurationsAsync(IEnumerable<FilterConfiguration> configurations)
        {
            try 
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_configPath));

                var jsonContent = JsonSerializer.Serialize(configurations, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });

                await File.WriteAllTextAsync(_configPath, jsonContent);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error saving filter configurations: {ex.Message}");
            }
        }

        public async Task<FilterConfiguration> AddConfigurationAsync(FilterConfiguration configuration)
        {
            var existingConfigs = await GetAllConfigurationsAsync().ToListAsync();
            
            // Ensure unique ID
            configuration.Id = Guid.NewGuid().ToString();
            
            existingConfigs.Add(configuration);
            await SaveConfigurationsAsync(existingConfigs);
            
            return configuration;
        }

        public async Task<bool> DeleteConfigurationAsync(string configId)
        {
            var existingConfigs = await GetAllConfigurationsAsync().ToListAsync();
            var configToRemove = existingConfigs.FirstOrDefault(c => c.Id == configId);
            
            if (configToRemove == null)
                return false;

            existingConfigs.Remove(configToRemove);
            await SaveConfigurationsAsync(existingConfigs);
            
            return true;
        }

        public async Task<FilterConfiguration> UpdateConfigurationAsync(FilterConfiguration configuration)
        {
            var existingConfigs = await GetAllConfigurationsAsync().ToListAsync();
            var existingConfig = existingConfigs.FirstOrDefault(c => c.Id == configuration.Id);
            
            if (existingConfig == null)
                throw new InvalidOperationException("Configuration not found");

            existingConfigs.Remove(existingConfig);
            existingConfigs.Add(configuration);
            
            await SaveConfigurationsAsync(existingConfigs);
            
            return configuration;
        }
    }
}
