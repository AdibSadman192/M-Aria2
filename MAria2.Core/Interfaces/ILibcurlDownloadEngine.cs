using System;
using System.Threading;
using System.Threading.Tasks;

namespace MAria2.Core.Interfaces
{
    public interface ILibcurlDownloadEngine : IDownloadEngine
    {
        /// <summary>
        /// LibCurl-specific download configuration options
        /// </summary>
        LibcurlDownloadOptions LibcurlOptions { get; set; }

        /// <summary>
        /// Retrieve libcurl version information
        /// </summary>
        Task<string> GetLibcurlVersionAsync();

        /// <summary>
        /// Check libcurl installation and availability
        /// </summary>
        Task<bool> IsLibcurlInstalledAsync();
    }

    public record LibcurlDownloadOptions
    {
        // Connection Options
        public int ConnectTimeout { get; init; } = 30
        public bool FollowRedirects { get; init; } = true
        public int MaxRedirects { get; init; } = 5

        // Performance Options
        public bool EnableMultiDownload { get; init; } = true
        public int MaxConcurrentDownloads { get; init; } = 5
        public long MaxDownloadSpeed { get; init; } = 0 // Unlimited

        // Authentication Options
        public string Username { get; init; }
        public string Password { get; init; }
        public bool UseDigestAuth { get; init; } = false

        // SSL/TLS Options
        public bool VerifySSL { get; init; } = true
        public string ClientCertPath { get; init; }
        public string ClientCertPassword { get; init; }

        // Proxy Configuration
        public string ProxyServer { get; init; }
        public int ProxyPort { get; init; }
        public string ProxyUsername { get; init; }
        public string ProxyPassword { get; init; }
        public ProxyType ProxyType { get; init; } = ProxyType.HTTP

        // Advanced Options
        public string UserAgent { get; init; } = "MAria2-LibcurlEngine/1.0"
        public bool AcceptCompression { get; init; } = true
        public string[] AcceptedEncodings { get; init; } = new[] { "gzip", "deflate" }

        // Download Resuming
        public bool EnableResumeSupport { get; init; } = true
    }

    public enum ProxyType
    {
        HTTP,
        HTTPS,
        SOCKS4,
        SOCKS5
    }
}
