# Microsoft Edge - AI Desktop Companion

A sleek Windows desktop application powered by Google's Gemini API, featuring screen understanding, voice input, and an always-on-top overlay window.

![Version](https://img.shields.io/badge/version-1.0.0-blue)
![.NET](https://img.shields.io/badge/.NET-8.0-purple)
![Platform](https://img.shields.io/badge/platform-Windows%2010/11-lightgrey)
![License](https://img.shields.io/badge/license-MIT-green)

## ✨ Features

- **💬 Smart Chat Interface** - Natural conversations with streaming responses
- **📷 Screen Understanding** - Capture and analyze screen content with AI vision
- **🎤 Voice Input** - Speech-to-text using Windows Speech Recognition
- **🪟 Floating Overlay** - Customizable always-on-top assistant window
- **🔒 Privacy Mode** - Block screen capture with Windows Display Affinity
- **🔐 Secure Storage** - API keys stored in Windows Credential Manager
- **📊 Real-time Streaming** - See AI responses as they're generated
- **🎨 Modern UI** - Clean WPF interface with transparency effects

## 🚀 Quick Start

### Prerequisites

- Windows 10 (1809+) or Windows 11 (64-bit)
- [.NET 8.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Google Gemini API Key](https://ai.google.dev/) (free)

### Installation

#### Option 1: Download Installer (Recommended)
1. Go to [Releases](../../releases/latest)
2. Download `MicrosoftEdge-Setup-v1.0.0.exe`
3. Run installer
4. Launch from Start Menu

#### Option 2: Build from Source
```bash
git clone https://github.com/yourusername/microsoft-edge-ai
cd microsoft-edge-ai
dotnet restore
dotnet build -c Release
```

## 📖 Usage

### First-Time Setup
1. Launch the application
2. Go to **Settings**
3. Enter your Gemini API key
4. Click **Save & Test**
5. Start chatting!

### Voice Input
1. Click **🎤 Voice** button
2. Speak when prompted
3. Your speech is converted to text and sent to AI

### Screen Analysis
1. Click **📷 Screen** button
2. Screen is captured automatically
3. AI describes what it sees

### Overlay Window
- Click **Overlay** in sidebar
- Drag to reposition
- Adjust opacity with slider
- Pin to keep always-on-top

## ⚙️ Configuration

### Available AI Models
- `gemini-1.5-flash` - Fastest (recommended)
- `gemini-1.5-pro` - Most capable
- `gemini-2.0-flash-exp` - Experimental

### Settings Location
- **User Settings**: `%LOCALAPPDATA%\MicrosoftEdge\settings.json`
- **API Key**: Windows Credential Manager (encrypted)
- **Logs**: `%LOCALAPPDATA%\MicrosoftEdge\logs\`

## 🎨 Customization

### Changing App Name and Branding

Want to use your own branding? Here's where to make changes:

#### 1. Application Name
**File**: `Assistant.csproj`
```xml
<AssemblyName>YourAppName</AssemblyName>
<Product>Your App Name</Product>
<Company>Your Company</Company>
```

#### 2. Window Title
**File**: `MainWindow.xaml`
```xml
Title="Your App Name"
```

#### 3. App Icon/Emoji
**File**: `MainWindow.xaml` (line ~62)
```xml
<TextBlock Text="🌊" FontSize="15" .../>  <!-- Change emoji here -->
<TextBlock Text="Your App Name" .../>
```

#### 4. Storage Paths
**File**: `App.xaml.cs` (line ~67)
```csharp
var logDir = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "YourAppName", "logs");
```

**File**: `Services/SettingsService.cs` (line ~21)
```csharp
"YourAppName");  // Change folder name
```

#### 5. Credential Manager Key
**File**: `Services/CredentialManagerService.cs` (line ~26)
```csharp
private const string CredentialTarget = "YourAppName_ApiKey";
```

**File**: `Services/CredentialManagerService.cs` (line ~56)
```csharp
Comment = "Your App Name — API Key",
```

#### 6. Installer Branding
**File**: `installer.iss`
```ini
#define AppName "Your App Name"
#define AppPublisher "Your Company"
```

### Adding Your Logo

Replace the wave emoji (🌊) with:
- Custom icon file (`.ico`)
- Different emoji
- Image resource

For custom icon:
1. Add icon to `Assets/` folder
2. Update `MainWindow.xaml`:
```xml
<Image Source="/Assets/yourlogo.png" Width="16" Height="16" />
```

## 🏗️ Project Structure

```
├── Models/           # Data models
├── Services/         # Business logic
│   ├── GeminiService.cs
│   ├── ScreenCaptureService.cs
│   └── SpeechService.cs
├── ViewModels/       # MVVM view models
├── Views/            # XAML views
├── Helpers/          # Utilities
├── Resources/        # Styles, themes
└── Assets/           # Icons, images
```

## 🔧 Development

### Requirements
- Visual Studio 2022 or VS Code
- .NET 8.0 SDK
- Windows 10 SDK

### Building
```bash
# Debug build
dotnet build

# Release build
dotnet build -c Release

# Run
dotnet run

# Create installer (requires Inno Setup)
build-installer.cmd
```

### Tech Stack
- **Framework**: WPF (.NET 8.0)
- **Architecture**: MVVM
- **DI**: Microsoft.Extensions.DependencyInjection
- **Logging**: Serilog
- **API**: Google Gemini REST API
- **Graphics**: System.Drawing (GDI+)
- **Speech**: System.Speech

## 🔒 Privacy & Security

- **API Keys**: Stored encrypted in Windows Credential Manager
- **Conversations**: NOT logged by default
- **Screen Capture**: On-demand only, user-initiated
- **Privacy Mode**: Blocks most screen recording tools
- **Logs**: Only errors and metadata (no content)

## 📝 License

MIT License - See [LICENSE](LICENSE) for details

## 🤝 Contributing

Contributions welcome! Please:
1. Fork the repository
2. Create a feature branch
3. Commit your changes
4. Push and create a Pull Request

## ⚠️ Troubleshooting

### "No API key found"
- Go to Settings → Enter API key → Save & Test

### "Speech not available"
- Enable Windows Speech Recognition
- Windows Settings → Speech

### ".NET not found"
- Install [.NET 8.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)

### Build errors
```bash
# Clean build
dotnet clean
dotnet restore
dotnet build
```

## 📚 Documentation

- [Setup Guide](SETUP_GUIDE.md) - Developer setup
- [Dependencies](DEPENDENCIES.md) - All dependencies
- [Quick Start](QUICKSTART.txt) - User guide

## 🌟 Credits

- **Google Gemini API** - https://ai.google.dev/
- **Serilog** - Structured logging
- **.NET Team** - Application framework

## 📬 Support

- **Issues**: [GitHub Issues](../../issues)
- **Logs**: `%LOCALAPPDATA%\MicrosoftEdge\logs\`
- **API Status**: [Google Cloud Status](https://status.cloud.google.com/)

---

**Note**: This is an unofficial project and is not affiliated with Microsoft or Google.
