# Dependencies

## Runtime Dependencies

### .NET Runtime
- **.NET 8.0 Desktop Runtime** (x64)
- Download: https://dotnet.microsoft.com/download/dotnet/8.0
- Required components:
  - Windows Desktop Runtime
  - ASP.NET Core Runtime (included)

### Windows Components
- **Windows 10 version 1809** or newer
- **Windows 11** (all versions)
- Windows Speech Recognition (optional, for voice features)

### NuGet Packages

#### Core Packages
- **Hardcodet.NotifyIcon.Wpf** v2.0.1
  - System tray icon support
  - License: Code Project Open License (CPOL)
  - https://github.com/hardcodet/wpf-notifyicon

- **Microsoft.Extensions.DependencyInjection** v8.0.1
  - Dependency injection container
  - License: MIT
  - https://github.com/dotnet/runtime

- **Microsoft.Extensions.Logging** v8.0.1
  - Logging abstractions
  - License: MIT
  - https://github.com/dotnet/runtime

#### Logging
- **Serilog** v3.1.1
  - Structured logging framework
  - License: Apache 2.0
  - https://github.com/serilog/serilog

- **Serilog.Extensions.Logging** v8.0.0
  - Microsoft.Extensions.Logging integration
  - License: Apache 2.0

- **Serilog.Sinks.File** v5.0.0
  - File output sink
  - License: Apache 2.0

#### System Integration
- **System.Drawing.Common** v8.0.0
  - GDI+ graphics (screen capture)
  - License: MIT
  - https://github.com/dotnet/runtime

- **System.Speech** (Framework reference)
  - Windows Speech Recognition API
  - Built into Windows
  - No separate download required

- **System.Windows.Forms** (Framework reference)
  - Windows Forms components (Screen class)
  - Built into .NET

## Development Dependencies

### Required Tools
- **Visual Studio 2022** (v17.8 or newer) OR **Visual Studio Code**
- **.NET 8.0 SDK**
  - Download: https://dotnet.microsoft.com/download/dotnet/8.0
- **Windows 10 SDK** (included with Visual Studio)

### Optional Tools
- **Inno Setup 6.x** (for installer creation)
  - Download: https://jrsoftware.org/isinfo.php
  - License: Custom (free for commercial use)

### Build Tools
- MSBuild (included with .NET SDK)
- NuGet CLI (included with .NET SDK)

## External Services

### Google Gemini API
- **API Endpoint:** `https://generativelanguage.googleapis.com`
- **Authentication:** API Key
- **Free Tier:** Available with rate limits
- **Get API Key:** https://ai.google.dev/

#### Supported Models
- `gemini-1.5-flash` - Fast, multimodal
- `gemini-1.5-pro` - Advanced reasoning
- `gemini-2.0-flash-exp` - Experimental

#### Rate Limits (Free Tier)
- 15 requests per minute
- 1 million tokens per minute
- 1,500 requests per day

## System Requirements

### Minimum
- **OS:** Windows 10 x64 (version 1809 or newer)
- **RAM:** 4 GB
- **Storage:** 500 MB free space
- **Internet:** Broadband connection

### Recommended
- **OS:** Windows 11 x64
- **RAM:** 8 GB or more
- **Storage:** 1 GB free space
- **Internet:** High-speed broadband
- **Microphone:** For voice input features

## Installation Steps

### 1. Install .NET Runtime
```cmd
# Check if already installed
dotnet --list-runtimes

# If not found, download from:
https://dotnet.microsoft.com/download/dotnet/8.0
# Install: "Windows Desktop Runtime x64"
```

### 2. Verify Installation
```cmd
dotnet --version
# Should show: 8.0.x
```

### 3. For Development
```cmd
# Install .NET SDK (not just runtime)
# Download from: https://dotnet.microsoft.com/download/dotnet/8.0

# Verify
dotnet --list-sdks
# Should show: 8.0.xxx
```

### 4. Build Project
```cmd
cd GeminiAssistant
dotnet restore
dotnet build -c Release
```

## Package Restore

### Automatic (Recommended)
```cmd
dotnet restore
```

### Manual NuGet Package Restoration
```cmd
dotnet nuget locals all --clear
dotnet restore --force
```

## Troubleshooting

### "Could not find SDK version"
- Install .NET 8.0 SDK from official Microsoft site
- Restart terminal/IDE after installation

### "Assembly not found: System.Speech"
- Update to latest .NET 8.0 runtime
- Ensure targeting `net8.0-windows` (not `net8.0`)

### "System.Drawing.Common requires Windows"
- This package only works on Windows
- Application cannot run on macOS/Linux

### NuGet Package Errors
```cmd
# Clear NuGet cache
dotnet nuget locals all --clear

# Restore with diagnostics
dotnet restore --verbosity detailed

# Force reinstall
dotnet restore --force-evaluate
```

## Security Considerations

### Package Verification
All packages sourced from:
- nuget.org (official Microsoft repository)
- Verified publishers only

### Vulnerability Scanning
```cmd
# Check for known vulnerabilities
dotnet list package --vulnerable
```

### Update Packages
```cmd
# Check for updates
dotnet list package --outdated

# Update to latest stable
dotnet add package <PackageName>
```

## License Compliance

### Microsoft Packages (MIT License)
- Microsoft.Extensions.*
- System.* packages
Permits: Commercial use, modification, distribution

### Third-Party Packages
- **Serilog** (Apache 2.0) - Permissive
- **Hardcodet.NotifyIcon.Wpf** (CPOL) - Permissive
- **System.Drawing.Common** (MIT) - Permissive

All dependencies are compatible with commercial use.

## Version Compatibility

### .NET Version Pinning
Project targets: `net8.0-windows`
- Ensures Windows-specific APIs available
- Not compatible with `net8.0` (cross-platform)

### Breaking Changes
When upgrading .NET major versions:
1. Check API deprecations
2. Test speech recognition (API changes)
3. Verify P/Invoke signatures (IntPtr changes)
4. Test credential manager integration

## Future Dependencies

### Planned (Optional)
- **NAudio** - Advanced audio input device selection
- **Windows App SDK** - Modern Windows 11 UI components
- **Microsoft.ML** - Local AI models (offline mode)
