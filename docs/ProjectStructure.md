# M-Aria2 Universal Download Manager - Project Structure

## Solution Structure

```
MAria2.sln
├── MAria2.Core                     // Domain Layer - Core business entities and rules
├── MAria2.Infrastructure           // Infrastructure Layer - Download engine implementations
├── MAria2.Application              // Application Layer - Coordination and application logic
└── MAria2.Presentation.WinUI       // Presentation Layer - WinUI 3 interface components
```

## Layer Breakdown

### Domain Layer (MAria2.Core)
- Entities
  - Download
  - DownloadProgress
  - DownloadSegment
  - EngineCapability
- Interfaces
  - IDownloadEngine
  - IDownloadTask
  - IProgressTracker
  - IEngineCapabilityProvider
  - IDownloadRepository
- Enums
  - DownloadStatus
  - DownloadPriority
  - EngineType
  - ProtocolType
  - ContentType
- Exceptions
  - DownloadException
  - EngineException

### Infrastructure Layer (MAria2.Infrastructure)
- Engine Implementations
  - Aria2
    - Aria2Engine
    - Aria2ConnectionManager
    - Aria2CapabilityProvider
    - Aria2Configuration
  - YtDlp
    - YtDlpEngine
    - YtDlpProcessManager
    - YtDlpCapabilityProvider
    - YtDlpConfiguration
  - WinInet
    - WinInetEngine
    - WinInetAdapter
    - WinInetCapabilityProvider
    - WinInetConfiguration
  - Wget
    - WgetEngine
    - WgetProcessManager
    - WgetCapabilityProvider
    - WgetConfiguration
  - LibCurl
    - LibCurlEngine
    - LibCurlNativeInterop
    - LibCurlCapabilityProvider
    - LibCurlConfiguration
- Repositories
  - SqliteDownloadRepository
- Services
  - EngineLoaderService
  - DependencyVerificationService
  - ChecksumService
  - FileSystemService

### Application Layer (MAria2.Application)
- Services
  - DownloadService
  - EngineManagerService
  - ProtocolHandlerService
  - DownloadQueueService
  - ConfigurationService
  - EngineSelectionService
  - SplitDownloadManager
  - EngineHealthMonitorService
  - EnginePerformanceAnalyticsService
- Commands
  - StartDownloadCommand
  - PauseDownloadCommand
  - ResumeDownloadCommand
  - CancelDownloadCommand
  - SwitchEngineCommand
  - SetPriorityCommand
- Events
  - DownloadProgressEvent
  - DownloadCompletedEvent
  - DownloadFailedEvent
  - EngineHealthEvent
- ViewModels
  - DownloadViewModel
  - EngineSettingsViewModel
  - DownloadQueueViewModel
  - EnginePerformanceViewModel
  - EngineRulesViewModel

### Presentation Layer (MAria2.Presentation.WinUI)
- Windows
  - MainWindow
  - DownloadDetailsWindow
  - SettingsWindow
  - EngineRulesWindow
  - PerformanceAnalyticsWindow
- Pages
  - DownloadsPage
  - CompletedDownloadsPage
  - EngineSettingsPage
  - GeneralSettingsPage
  - AdvancedSettingsPage
- Controls
  - DownloadListItem
  - DownloadProgressBar
  - EngineSelector
  - EngineSettingsPanel
  - EnginePerformanceGraph
  - ProtocolSelector
  - DownloadPrioritySelector
- Resources
  - Styles
  - Icons
  - Templates

## Support Folders

```
/lib                      // Third-party libraries and engines
  /aria2                  // Aria2 binaries and libraries
  /yt-dlp                 // yt-dlp executable and dependencies
  /wget                   // wget binaries
  /curl                   // libcurl DLLs and dependencies
  /runtime                // .NET runtime components if needed

/assets                   // Application assets (icons, images)

/docs                     // Documentation
```

## Core Implementation Details

The system will use a plugin architecture with the Adapter pattern to normalize different download APIs into a consistent interface. The EngineManager service will act as the central coordinator and will use the Services Locator pattern for dependency management.

Each engine will register its capabilities through an IEngineCapabilityProvider, allowing the system to make intelligent decisions about which engine to use for specific download tasks.

The system will implement a publisher/subscriber model for status updates and notifications, allowing components to respond to download events without tight coupling.