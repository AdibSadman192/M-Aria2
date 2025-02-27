using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace MAria2.Core.Interfaces
{
    public interface IAdvancedFilterService
    {
        /// <summary>
        /// Applies complex filtering to download candidates
        /// </summary>
        Task<IEnumerable<DownloadCandidate>> ApplyFiltersAsync(
            IEnumerable<DownloadCandidate> candidates, 
            FilterConfiguration filterConfig);

        /// <summary>
        /// Creates a custom filter based on dynamic expression
        /// </summary>
        Func<DownloadCandidate, bool> CreateCustomFilter(string filterExpression);

        /// <summary>
        /// Validates and compiles a filter expression
        /// </summary>
        bool ValidateFilterExpression(string filterExpression);

        /// <summary>
        /// Saves a custom filter configuration
        /// </summary>
        Task<FilterConfiguration> SaveFilterConfigurationAsync(FilterConfiguration config);

        /// <summary>
        /// Retrieves saved filter configurations
        /// </summary>
        Task<IEnumerable<FilterConfiguration>> GetSavedFilterConfigurationsAsync();
    }

    public class DownloadCandidate
    {
        public string Url { get; set; }
        public string FileName { get; set; }
        public long FileSize { get; set; }
        public string FileType { get; set; }
        public DateTime DiscoveredAt { get; set; }
        public string Source { get; set; }
        public Dictionary<string, object> Metadata { get; set; }
    }

    public class FilterConfiguration
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public FilterCriteria Criteria { get; set; }
        public bool IsEnabled { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class FilterCriteria
    {
        // URL Filtering
        public List<string> AllowedDomains { get; set; }
        public List<string> BlockedDomains { get; set; }

        // File Type Filtering
        public List<string> AllowedFileTypes { get; set; }
        public List<string> BlockedFileTypes { get; set; }

        // Size Filtering
        public long? MinFileSize { get; set; }
        public long? MaxFileSize { get; set; }

        // Date Filtering
        public DateTime? MinDiscoveryDate { get; set; }
        public DateTime? MaxDiscoveryDate { get; set; }

        // Advanced Metadata Filtering
        public Dictionary<string, object> MetadataFilters { get; set; }

        // Custom Expression Filter
        public string CustomFilterExpression { get; set; }
    }

    public enum FilterPriority
    {
        Low,
        Medium,
        High,
        Critical
    }

    public class FilterResult
    {
        public DownloadCandidate Candidate { get; set; }
        public bool Passed { get; set; }
        public List<string> FailedRules { get; set; }
    }
}
