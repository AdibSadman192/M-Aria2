# MAria2 Universal Download Manager

## Overview
MAria2 is an advanced, multi-engine download management application designed with flexibility, performance, and intelligent routing as core principles.

## Key Features

### 🌐 Cross-Platform System Integration
- Advanced platform-specific security management
- Intelligent network monitoring and optimization
- Adaptive system resource handling
- Consistent performance across Windows, macOS, and Linux

### 🔒 Security Capabilities
- Platform-aware administrator privilege detection
- Secure password and token management
- Granular file permission controls
- Cross-platform security abstraction

### 🌈 Network Intelligence
- Multi-platform network information retrieval
- Real-time connection monitoring
- Adaptive network speed tracking
- Intelligent connection fallback mechanisms

### 🚀 Performance Optimization
- Lightweight cross-platform managers
- Minimal runtime overhead
- Efficient resource utilization
- Machine learning-driven optimization strategies

## System Integration Architecture

MAria2 uses a sophisticated, factory-based approach to system integration:

```csharp
// Automatic platform detection and manager instantiation
var securityManager = CrossPlatformSecurityManagerFactory.Create(logger);
var networkManager = CrossPlatformNetworkManagerFactory.Create(logger);
```

## Comprehensive Documentation

MAria2 provides extensive documentation to help you understand, use, and contribute to the project:

### 🏗️ Project Architecture
- [System Architecture](/docs/ARCHITECTURE.md)
- [Project Structure](/docs/ProjectStructure.md)

### 🚀 Performance
- [Performance Optimization Guide](/docs/PERFORMANCE.md)

### 🔒 Security
- [Security Architecture](/docs/security.md)

### 🌐 System Integration
- [Cross-Platform Integration Guide](/docs/system-integration.md)

### 🛠️ Development
- [Continuous Integration and Deployment](/docs/CICD_README.md)
- [Contributing Guidelines](/docs/CONTRIBUTING.md)

## Contributing
1. Fork the repository
2. Review our [Contributing Guidelines](/docs/CONTRIBUTING.md)
3. Submit pull requests with clear descriptions

## Detailed Documentation

For more in-depth information about MAria2, explore our comprehensive documentation. Each guide provides insights into different aspects of the project, from architecture and performance to security and system integration.

## System Requirements
- .NET 8 Runtime
- Minimum 4GB RAM
- 500MB Disk Space
- Supported Platforms: Windows 10/11, macOS 10.15+, Linux (Ubuntu 20.04+)

## Installation Options

### Automatic Installation (Recommended)
1. Go to [Releases](https://github.com/Adibsadman192/M-Aria2/releases)
2. Download the appropriate installer for your platform:
   - Windows: `MAria2-windows.exe`
   - macOS: `MAria2-macos.dmg`
   - Linux: `MAria2.tar.gz` or `.deb`/`.rpm`

### Manual Build from Source

#### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Git
- (Optional) Cross-platform build tools

#### Clone the Repository
```bash
git clone https://github.com/Adibsadman192/MAria2.git
cd MAria2
```

#### Build for Your Platform

##### Windows
```powershell
dotnet restore
dotnet build -c Release
dotnet publish -c Release -r win-x64 --self-contained true
```

##### macOS
```bash
dotnet restore
dotnet build -c Release
dotnet publish -c Release -r osx-x64 --self-contained true
```

##### Linux
```bash
dotnet restore
dotnet build -c Release
dotnet publish -c Release -r linux-x64 --self-contained true
```

## Development Dependencies
- FFMpegCore
- YoutubeDL.Net
- Prometheus
- Grafana
- ML.NET

## Troubleshooting
- Ensure you have the latest .NET 8 runtime
- Check system compatibility using the built-in compatibility checker
- Refer to our [Wiki](https://github.com/Adibsadman192/M-Aria2/wiki) for detailed guides

## License
Distributed under the MIT License. See `LICENSE` for more information.

## Contact
Project Link: [https://github.com/Adibsadman192/MAria2](https://github.com/Adibsadman192/MAria2)
