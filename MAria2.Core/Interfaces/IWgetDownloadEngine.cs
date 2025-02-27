using System;
using System.Threading;
using System.Threading.Tasks;

namespace MAria2.Core.Interfaces
{
    public interface IWgetDownloadEngine : IDownloadEngine
    {
        /// <summary>
        /// Wget-specific download configuration options
        /// </summary>
        WgetDownloadOptions WgetOptions { get; set; }

        /// <summary>
        /// Retrieve wget version information
        /// </summary>
        Task<string> GetWgetVersionAsync();

        /// <summary>
        /// Check wget installation and availability
        /// </summary>
        Task<bool> IsWgetInstalledAsync();
    }

    public record WgetDownloadOptions
    {
        // Basic Download Options
        public bool Recursive { get; init; } = false
        public int MaxDepth { get; init; } = 5
        public bool MirrorSite { get; init; } = false
        public bool ConvertLinks { get; init; } = false

        // Performance Options
        public int Timeout { get; init; } = 60
        public int Tries { get; init; } = 3
        public bool ContinueDownload { get; init; } = true

        // Authentication Options
        public string Username { get; init; }
        public string Password { get; init; }

        // Bandwidth and Rate Limiting
        public long MaxDownloadSpeed { get; init; } = 0 // Unlimited
        public bool Throttle { get; init; } = false

        // Advanced Options
        public bool IgnoreRobotsTxt { get; init; } = false
        public bool AcceptCookies { get; init; } = true
        public string UserAgent { get; init; } = "MAria2-WgetEngine/1.0"

        // Proxy Configuration
        public string ProxyServer { get; init; }
        public int ProxyPort { get; init; }
        public string ProxyUsername { get; init; }
        public string ProxyPassword { get; init; }
    }
}
