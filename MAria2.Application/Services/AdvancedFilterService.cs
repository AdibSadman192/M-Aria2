using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MAria2.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System.Dynamic;
using System.Text.Json;

namespace MAria2.Application.Services
{
    public class AdvancedFilterService : IAdvancedFilterService
    {
        private readonly ILogger<AdvancedFilterService> _logger;
        private readonly ConcurrentDictionary<string, FilterConfiguration> _savedConfigurations;

        public AdvancedFilterService(ILogger<AdvancedFilterService> logger)
        {
            _logger = logger;
            _savedConfigurations = new ConcurrentDictionary<string, FilterConfiguration>();
        }

        public async Task<IEnumerable<DownloadCandidate>> ApplyFiltersAsync(
            IEnumerable<DownloadCandidate> candidates, 
            FilterConfiguration filterConfig)
        {
            if (filterConfig == null || !filterConfig.IsEnabled)
                return candidates;

            var results = new ConcurrentBag<DownloadCandidate>();
            var criteria = filterConfig.Criteria;

            await Task.WhenAll(candidates.Select(async candidate =>
            {
                var filterResult = await EvaluateCandidateAsync(candidate, criteria);
                if (filterResult.Passed)
                {
                    results.Add(candidate);
                }
                else
                {
                    _logger.LogInformation($"Candidate {candidate.Url} filtered out. Failed rules: {string.Join(", ", filterResult.FailedRules)}");
                }
            }));

            return results;
        }

        private async Task<FilterResult> EvaluateCandidateAsync(
            DownloadCandidate candidate, 
            FilterCriteria criteria)
        {
            var result = new FilterResult 
            { 
                Candidate = candidate, 
                Passed = true, 
                FailedRules = new List<string>() 
            };

            // Domain Filtering
            if (criteria.AllowedDomains?.Any() == true)
            {
                var candidateDomain = new Uri(candidate.Url).Host;
                if (!criteria.AllowedDomains.Any(d => candidateDomain.Contains(d)))
                {
                    result.Passed = false;
                    result.FailedRules.Add("Domain not allowed");
                }
            }

            if (criteria.BlockedDomains?.Any() == true)
            {
                var candidateDomain = new Uri(candidate.Url).Host;
                if (criteria.BlockedDomains.Any(d => candidateDomain.Contains(d)))
                {
                    result.Passed = false;
                    result.FailedRules.Add("Domain is blocked");
                }
            }

            // File Type Filtering
            if (criteria.AllowedFileTypes?.Any() == true)
            {
                if (!criteria.AllowedFileTypes.Contains(candidate.FileType))
                {
                    result.Passed = false;
                    result.FailedRules.Add("File type not allowed");
                }
            }

            if (criteria.BlockedFileTypes?.Any() == true)
            {
                if (criteria.BlockedFileTypes.Contains(candidate.FileType))
                {
                    result.Passed = false;
                    result.FailedRules.Add("File type is blocked");
                }
            }

            // Size Filtering
            if (criteria.MinFileSize.HasValue && candidate.FileSize < criteria.MinFileSize.Value)
            {
                result.Passed = false;
                result.FailedRules.Add("File size too small");
            }

            if (criteria.MaxFileSize.HasValue && candidate.FileSize > criteria.MaxFileSize.Value)
            {
                result.Passed = false;
                result.FailedRules.Add("File size too large");
            }

            // Date Filtering
            if (criteria.MinDiscoveryDate.HasValue && candidate.DiscoveredAt < criteria.MinDiscoveryDate.Value)
            {
                result.Passed = false;
                result.FailedRules.Add("Discovery date too early");
            }

            if (criteria.MaxDiscoveryDate.HasValue && candidate.DiscoveredAt > criteria.MaxDiscoveryDate.Value)
            {
                result.Passed = false;
                result.FailedRules.Add("Discovery date too late");
            }

            // Metadata Filtering
            if (criteria.MetadataFilters?.Any() == true)
            {
                foreach (var filter in criteria.MetadataFilters)
                {
                    if (candidate.Metadata.TryGetValue(filter.Key, out var value))
                    {
                        if (!value.Equals(filter.Value))
                        {
                            result.Passed = false;
                            result.FailedRules.Add($"Metadata filter failed for {filter.Key}");
                        }
                    }
                }
            }

            // Custom Expression Filtering
            if (!string.IsNullOrEmpty(criteria.CustomFilterExpression))
            {
                var customFilter = CreateCustomFilter(criteria.CustomFilterExpression);
                if (!customFilter(candidate))
                {
                    result.Passed = false;
                    result.FailedRules.Add("Custom filter expression failed");
                }
            }

            return result;
        }

        public Func<DownloadCandidate, bool> CreateCustomFilter(string filterExpression)
        {
            try
            {
                // Basic safety check
                if (filterExpression.Length > 1000)
                    throw new ArgumentException("Filter expression too long");

                // Compile a simple lambda expression
                var parameter = Expression.Parameter(typeof(DownloadCandidate), "x");
                var body = System.Linq.Dynamic.Core.DynamicExpressionParser.ParseLambda(
                    new[] { parameter }, 
                    typeof(bool), 
                    filterExpression
                );

                return (Func<DownloadCandidate, bool>)body.Compile();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to create custom filter: {ex.Message}");
                return _ => true; // Default to allowing all
            }
        }

        public bool ValidateFilterExpression(string filterExpression)
        {
            try
            {
                CreateCustomFilter(filterExpression);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<FilterConfiguration> SaveFilterConfigurationAsync(FilterConfiguration config)
        {
            if (string.IsNullOrEmpty(config.Id))
                config.Id = Guid.NewGuid().ToString();

            _savedConfigurations[config.Id] = config;
            
            // Simulate async persistence
            await Task.Delay(50);
            
            return config;
        }

        public async Task<IEnumerable<FilterConfiguration>> GetSavedFilterConfigurationsAsync()
        {
            // Simulate async retrieval
            await Task.Delay(50);
            
            return _savedConfigurations.Values.ToList();
        }
    }
}
