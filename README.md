# MAria2 Universal Download Manager

## Overview
MAria2 is an advanced, multi-engine download management application designed with flexibility, performance, and intelligent routing as core principles.

## System Requirements
- .NET 8 Runtime
- Minimum 4GB RAM
- 500MB Disk Space
- Supported Platforms: Windows 10/11, macOS 10.15+, Linux (Ubuntu 20.04+)

## Installation Options

### Automatic Installation (Recommended)
1. Go to [Releases](https://github.com/Adibsadman192/MAria2/releases)
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

## Contributing
1. Fork the repository
2. Create your feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## Troubleshooting
- Ensure you have the latest .NET 8 runtime
- Check system compatibility using the built-in compatibility checker
- Refer to our [Wiki](https://github.com/Adibsadman192/MAria2/wiki) for detailed guides

## License
Distributed under the MIT License. See `LICENSE` for more information.

## Contact
Project Link: [https://github.com/Adibsadman192/MAria2](https://github.com/Adibsadman192/MAria2)
