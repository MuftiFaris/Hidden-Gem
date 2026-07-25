# Hidden Gem - Setup Guide

## Quick Start

### For End Users

1. **Download Installer**
   - Get `HiddenGem-Setup-v1.0.0.exe`
   - Run installer
   - Follow on-screen instructions
   - Launch from Start Menu

2. **Configure API Key**
   - Open Settings
   - Enter Gemini API key (get free: https://ai.google.dev/)
   - Click "Save & Test"
   - Return to Chat

3. **Start Chatting**
   - Type message
   - Press Send or Enter
   - AI responds in real-time

---

## For Developers

### Prerequisites

1. **Install .NET 8.0 SDK**
   ```cmd
   # Download from:
   https://dotnet.microsoft.com/download/dotnet/8.0
   
   # Verify installation:
   dotnet --version
   ```

2. **Install Visual Studio 2022** (optional but recommended)
   - Workloads: .NET Desktop Development
   - Or use Visual Studio Code with C# extension

3. **Get Gemini API Key**
   - Visit: https://ai.google.dev/
   - Sign in with Google account
   - Create API key
   - Save securely

### Clone & Build

```cmd
# Clone repository
git clone <repository-url>
cd GeminiAssistant

# Restore packages
dotnet restore

# Build Debug
dotnet build

# Build Release
dotnet build -c Release

# Run
dotnet run
```

### Project Structure Explained

```
GeminiAssistant/
├── Models/                    # Data models
│   ├── ChatMessage.cs         # Chat message with INPC
│   ├── AppSettings.cs         # User preferences
│   └── GeminiModels.cs        # API request/response DTOs
│
├── Services/                  # Business logic layer
│   ├── GeminiService.cs       # Gemini API client
│   ├── ScreenCaptureService.cs # GDI+ screen capture
│   ├── SpeechService.cs       # Windows Speech Recognition
│   ├── CredentialManagerService.cs # Windows Credential Manager
│   └── SettingsService.cs     # JSON settings persistence
│
├── ViewModels/                # MVVM view models
│   ├── MainViewModel.cs       # Root VM (navigation)
│   ├── ChatViewModel.cs       # Chat functionality
│   └── SettingsViewModel.cs   # Settings page
│
├── Views/                     # XAML views
│   ├── ChatView.xaml          # Chat interface
│   └── SettingsView.xaml      # Settings page
│
├── Helpers/                   # Utilities
│   ├── RelayCommand.cs        # ICommand implementation
│   ├── Converters.cs          # Value converters
│   ├── SystemTrayManager.cs   # Tray icon
│   └── WindowPrivacyHelper.cs # Privacy mode P/Invoke
│
├── MainWindow.xaml            # Main app window
├── OverlayWindow.xaml         # Floating overlay
├── App.xaml.cs                # DI + logging setup
└── GeminiAssistant.csproj     # Project file
```

### Key Technologies

- **Framework:** WPF on .NET 8.0
- **Architecture:** MVVM
- **DI:** Microsoft.Extensions.DependencyInjection
- **Logging:** Serilog (file-based)
- **HTTP:** System.Net.Http.HttpClient
- **Graphics:** System.Drawing (GDI+)
- **Speech:** System.Speech

### Environment Setup

#### 1. API Key (Development)

**Option A: Use Settings UI (Recommended)**
1. Run application
2. Go to Settings
3. Enter API key
4. Saves to Windows Credential Manager

**Option B: Manual Credential Manager**
1. Open Control Panel
2. Credential Manager → Windows Credentials
3. Add Generic Credential:
   - Internet/network address: `HiddenGem_ApiKey`
   - Password: Your API key

#### 2. Logging Configuration

Default location: `%LOCALAPPDATA%\HiddenGem\logs\`

To change log settings, edit `App.xaml.cs`:

```csharp
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()  // Change to .Information() for less verbose
    .WriteTo.File(
        path: Path.Combine(logDir, "app-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7)  // Keep 7 days
    .CreateLogger();
```

#### 3. Privacy Mode

Windows 10 1903+ required for SetWindowDisplayAffinity API.

Test in code:
```csharp
var handle = new WindowInteropHelper(this).Handle;
WindowPrivacyHelper.SetPrivacyMode(handle, true, logger);
```

### Development Workflow

#### 1. Run in Debug Mode
```cmd
# Visual Studio: F5
# VS Code: Run task "Debug"
# CLI:
dotnet run
```

#### 2. Hot Reload (XAML only)
- Visual Studio: Hot Reload enabled by default
- Edit XAML → Save → See changes immediately

#### 3. Debugging
- Set breakpoints in `.cs` files
- Use Debug Console for output
- Check logs: `%LOCALAPPDATA%\HiddenGem\logs\`

#### 4. Testing API Integration

Manually test Gemini service:
```csharp
var gemini = serviceProvider.GetRequiredService<IGeminiService>();
var apiKey = "YOUR_API_KEY";
var settings = new AppSettings();
var history = new List<ChatMessage>
{
    new() { Role = MessageRole.User, Content = "Hello" }
};

var response = await gemini.SendMessageAsync(history, apiKey, settings);
Console.WriteLine(response);
```

### Build Configurations

#### Debug Build
- Full symbols
- No optimization
- Logging verbose
```cmd
dotnet build -c Debug
```

#### Release Build
- Optimized
- Minimal symbols
- Production-ready
```cmd
dotnet build -c Release
```

#### Publish (Self-Contained)
```cmd
dotnet publish -c Release -r win-x64 --self-contained true
# Output: bin\Release\net8.0-windows\win-x64\publish\
# Size: ~150 MB (includes .NET runtime)
```

#### Publish (Framework-Dependent)
```cmd
dotnet publish -c Release -r win-x64 --self-contained false
# Output: bin\Release\net8.0-windows\win-x64\publish\
# Size: ~5 MB (requires .NET 8.0 installed)
```

### Creating Installer

#### Prerequisites
- **Inno Setup 6.x**: https://jrsoftware.org/isinfo.php
- Application built in Release mode

#### Steps

1. **Build Application**
   ```cmd
   dotnet publish -c Release -r win-x64 --self-contained false
   ```

2. **Run Installer Script**
   ```cmd
   build-installer.cmd
   ```
   Or manually:
   ```cmd
   "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer.iss
   ```

3. **Output**
   - Location: `installer-output\HiddenGem-Setup-v1.0.0.exe`
   - Size: ~6 MB (framework-dependent)

#### Customize Installer

Edit `installer.iss`:

```ini
#define AppVersion "1.0.0"  ; Change version

[Setup]
DefaultDirName={autopf}\{#AppName}  ; Install location
OutputBaseFilename=CustomName       ; Installer filename

[Tasks]
Name: "startup"; Description: "Launch at startup"; Flags: checked  ; Enable by default
```

### Troubleshooting Development

#### Build Errors

**"SDK not found"**
```cmd
# Install .NET 8.0 SDK
# Verify:
dotnet --list-sdks
```

**"System.Speech not found"**
- Ensure project targets `net8.0-windows` (not `net8.0`)
- Check `<PackageReference Include="System.Speech" Version="8.0.0" />`

**"XAML parse error"**
- Check for missing resource references
- Verify namespace declarations
- Rebuild solution

#### Runtime Errors

**"API key not found"**
- Check Windows Credential Manager
- Re-enter key in Settings UI
- Verify credential target name matches

**"Speech recognition failed"**
- Enable Windows Speech Recognition
- Windows Settings → Speech
- Select language pack

**"Screen capture black screen"**
- Run without admin rights (admin windows have protection)
- Check display scaling settings
- Try region capture instead of full screen

#### Performance Issues

**High CPU during streaming**
- Normal behavior (SSE processing)
- Disable streaming in Settings
- Reduce max tokens

**Memory leak**
- Check for undisposed Bitmap objects
- Review async/await patterns
- Use memory profiler

### Advanced Configuration

#### Custom Models

Add to `SettingsViewModel.cs`:
```csharp
public List<string> AvailableModels => new()
{
    "gemini-1.5-flash",
    "gemini-1.5-pro",
    "gemini-2.0-flash-exp",
    "your-custom-model"  // Add here
};
```

#### Custom System Prompt

Settings UI or `AppSettings.cs`:
```csharp
public string SystemPrompt { get; set; } =
    "You are a specialized assistant for...";
```

#### Custom Overlay Position

Edit `OverlayWindow.xaml.cs`:
```csharp
public OverlayWindow()
{
    InitializeComponent();
    
    // Position at bottom-right
    this.Left = SystemParameters.WorkArea.Right - this.Width - 20;
    this.Top = SystemParameters.WorkArea.Bottom - this.Height - 20;
}
```

### Deployment Checklist

- [ ] Update version in `.csproj`
- [ ] Update version in `installer.iss`
- [ ] Build Release configuration
- [ ] Test on clean Windows install
- [ ] Verify .NET 8.0 runtime requirement
- [ ] Test API key flow
- [ ] Test all features (voice, screen, overlay)
- [ ] Check installer creates shortcuts
- [ ] Verify uninstall removes all files
- [ ] Update README.md with changes
- [ ] Tag release in git

### Resources

- **.NET Documentation**: https://docs.microsoft.com/dotnet/
- **WPF Guide**: https://docs.microsoft.com/dotnet/desktop/wpf/
- **Gemini API**: https://ai.google.dev/api/
- **Inno Setup**: https://jrsoftware.org/ishelp/
- **Windows API**: https://docs.microsoft.com/windows/win32/

### Support

Check logs first:
```cmd
notepad %LOCALAPPDATA%\HiddenGem\logs\app-*.log
```

Common log locations:
- Application: `%LOCALAPPDATA%\HiddenGem\logs\`
- Settings: `%LOCALAPPDATA%\HiddenGem\settings.json`
- Credentials: Windows Credential Manager

### Next Steps

1. Explore code in `Services/` folder
2. Modify `ChatView.xaml` for custom UI
3. Add new features in ViewModels
4. Extend Gemini API capabilities
5. Customize branding/styling

Happy coding!
