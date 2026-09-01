# Hidden Gem - Complete Feature Summary

## Overview

Hidden Gem is a comprehensive AI-powered Windows desktop assistant that combines:
- **Chat interface** for conversations with Gemini AI
- **Screen understanding** for visual analysis
- **Voice input** for hands-free operation
- **Interview assistance** for real-time question answering
- **Floating overlay** for quick access
- **Privacy mode** for secure screen sharing

## Core Features

### 1. Smart Chat Interface ✅
- **Real-time streaming responses** - See answers as they're generated
- **Conversation history** - Full chat context for follow-ups
- **Customizable system prompt** - Define AI personality
- **Message formatting** - Clean, readable message display
- **Copy to clipboard** - Easy sharing of responses

**Use Case**: General AI conversations, brainstorming, writing help

### 2. Screen Understanding ✅
- **Full screen capture** - Analyze entire display
- **Active window capture** - Focus on current app
- **Region selection** - Crop specific areas
- **Vision API integration** - Gemini analyzes visual content
- **Base64 encoding** - Efficient image transmission

**Use Case**: "What's on my screen?", "Read this text", "Explain this error"

### 3. Voice Input ✅
- **Windows Speech Recognition** - Native, no extra setup
- **Continuous dictation mode** - Natural speech input
- **Confidence filtering** - Rejects low-quality recognition
- **Real-time feedback** - Shows recording status
- **Language support** - Auto-detects system language

**Use Case**: Hands-free input, accessibility, quick queries

### 4. Floating Overlay Window ✅
- **Always-on-top** - Stays visible over any app
- **Draggable interface** - Position anywhere
- **Opacity control** - Adjust transparency (0-100%)
- **Quick actions** - Voice button, Screen button
- **Pin toggle** - Lock/unlock always-on-top
- **Minimal footprint** - Compact design

**Use Case**: Quick reference, multitasking, minimal distraction

### 5. Privacy Mode ✅
- **Windows Display Affinity** - Hides from screen capture APIs
- **Tested with Zoom, Discord, OBS** - Works with major apps
- **Toggle on/off** - Easy enable/disable
- **Persistence** - Remembers preference
- **No performance impact** - Lightweight implementation

**Use Case**: Confidential content, recording sessions, sensitive work

### 6. Secure Credential Storage ✅
- **Windows Credential Manager** - OS-level encryption
- **User-scoped** - No admin rights needed
- **Secure deletion** - Easy removal
- **Never logged** - API keys not stored in files
- **Auto-retrieval** - Seamless on launch

**Use Case**: Safe API key management, multi-user systems

### 7. Real-time Streaming ✅
- **Server-Sent Events (SSE)** - Gemini streaming protocol
- **Progressive display** - Text appears incrementally
- **Chunk processing** - Efficient parsing
- **Cancel support** - Stop generation mid-stream
- **Error resilience** - Handles connection drops

**Use Case**: Long responses, real-time feedback, perceived speed

### 8. Modern UI ✅
- **Dark theme** - Eye-friendly GitHub-style design
- **Color-coded sections** - Blue (API), Purple (Model), Green (Privacy), Indigo (Window)
- **Professional layout** - Proper spacing and alignment
- **Responsive design** - Scales to window size
- **Accessibility** - Readable fonts, high contrast

**Use Case**: Pleasant user experience, extended use, reduced eye strain

### 9. Interview Assistant (NEW!) ✅
- **Audio capture from speakers** - WASAPI loopback technology
- **Automatic question detection** - Pattern matching + AI analysis
- **Response generation** - Combines screen context + question
- **Real-time transcription** - Audio → text via Gemini
- **Exchange history** - Track all Q&A pairs
- **One-click copy** - Paste responses into chat

**Use Case**: Video interviews (Zoom, Discord, GMeet, Teams), real-time assistance

#### Interview Audio System
- **WASAPI Loopback Capture**: Captures all system audio
- **NAudio Integration**: Professional audio processing
- **Device Enumeration**: Multiple speaker support
- **Audio Analysis**: Silence detection, noise filtering
- **Transcription Pipeline**: Audio → WAV → Base64 → Gemini → Text

#### Interview Question Detection
- **Pattern Matching**: Regex for common question phrases
- **Customizable Rules**: Add your own patterns
- **Confidence Scoring**: Filter false positives
- **Context Awareness**: Combines audio + screen analysis

#### Interview Response Generation
- **Context Combination**: Screen capture + transcribed question
- **System Prompt**: Tuned for interview scenarios
- **Temperature Setting**: 0.5 for balanced responses
- **Token Management**: Appropriate response length

---

## Technical Architecture

### Services Layer
```
IGeminiService              → Google Gemini REST API (text + vision)
IScreenCaptureService       → GDI+ BitBlt (screen capture)
ISpeechService              → Windows Speech Recognition (voice)
IAudioCaptureService        → NAudio + WASAPI Loopback (interview audio)
IAudioTranscriptionService  → Audio → text conversion
IAutoResponseService        → Interview Q&A automation
ISettingsService            → JSON persistence
ICredentialService          → Windows Credential Manager
```

### ViewModels (MVVM)
```
ChatViewModel          → Conversation state + streaming
SettingsViewModel      → Preferences, API key, model config
InterviewViewModel     → Audio capture, auto-response state
MainViewModel          → Navigation, privacy mode, overlay
```

### Views (XAML)
```
MainWindow.xaml         → Main application window
ChatView.xaml           → Chat interface
SettingsView.xaml       → Configuration panel
InterviewView.xaml      → Interview assistant UI
OverlayWindow.xaml      → Floating assistant window
```

### Models
```
ChatMessage             → Single message in conversation
AppSettings             → User preferences (JSON)
GeminiModels            → API request/response structures
AudioDevice             → Speaker descriptor
AutoResponseRule        → Question detection pattern
InterviewExchange       → Q&A history entry
```

### Helpers
```
RelayCommand            → ICommand implementation
Converters              → Data binding converters
WindowPrivacyHelper     → Display Affinity API
CredentialManager P/Invoke → Secure credential storage
```

---

## Dependencies

### NuGet Packages
- **NAudio** (2.2.1) - Audio capture and processing
- **Hardcodet.NotifyIcon.Wpf** (2.0.1) - System tray support
- **Microsoft.Extensions.DependencyInjection** (8.0.1) - IoC container
- **Microsoft.Extensions.Logging** (8.0.1) - Logging abstractions
- **Serilog** (3.1.1) - Structured file logging
- **System.Drawing.Common** (8.0.0) - GDI+ screen capture
- **System.Speech** (8.0.0) - Windows Speech Recognition

### Built-In APIs
- **WASAPI** (Windows Audio Session API) - Loopback audio capture
- **P/Invoke** (advapi32.dll) - Credential Manager access
- **GDI+** - Screen capture via BitBlt
- **WPF** - UI framework

---

## File Storage

All user data stored locally in `%LOCALAPPDATA%\HiddenGem\`:

```
%LOCALAPPDATA%\HiddenGem\
├── settings.json                    # User preferences (non-sensitive)
└── logs/
    ├── HiddenGem-yyyyMMdd.log      # Daily rotating logs
    └── (7-day retention)
```

### What's Stored
- ✅ Settings: model, temperature, token limits, UI preferences
- ✅ Logs: errors, API calls (no conversation content)
- ❌ API keys: NOT stored (Credential Manager only)
- ❌ Conversations: NOT stored (unless explicitly enabled)

---

## API Integration

### Google Gemini API
- **Endpoint**: `https://generativelanguage.googleapis.com/v1beta/`
- **Models Used**:
  - `gemini-3.5-flash` - Default (fast, capable)
  - `gemini-3.5-pro` - More powerful (slower)
  - `gemini-3.6-flash` - Newer variant
  - `gemini-pro-latest` - Experimental

### Rate Limiting
- **Free Tier**: 15 requests per minute
- **Batching**: Combine questions when possible
- **Queue**: Failed requests retry with backoff

### Error Handling
- **401 Unauthorized**: Invalid API key
- **400 Bad Request**: Malformed request
- **503 Service Unavailable**: API overloaded
- **Network Errors**: Connection timeouts

---

## Performance Characteristics

### Response Time
- **Chat (non-streaming)**: 2-5 seconds (depends on response length)
- **Chat (streaming)**: First token in <1 second
- **Screen capture**: <200ms
- **Voice recognition**: 1-5 seconds (depends on speech length)
- **Audio transcription**: 3-10 seconds (depends on audio length)

### Memory Usage
- **Baseline**: ~150 MB (app + dependencies)
- **Chat history**: ~1 MB per 1000 messages
- **Audio buffer**: ~50 MB per 5 minutes
- **Screenshots**: ~5-10 MB each (compressed JPEG)

### Network
- **Chat streaming**: ~1 KB per token
- **Screen capture**: ~100-500 KB (depends on resolution)
- **Audio**: ~100 KB per minute (16-bit WAV)

---

## Security Considerations

### API Key Protection
- Stored in Windows Credential Manager (encrypted at OS level)
- Never written to disk
- Not logged or transmitted except to Gemini
- User can delete via Settings UI

### Screen Capture
- Only captured on-demand by user action
- Not transmitted unless sent to AI
- Privacy mode blocks capture tools
- User has full control

### Conversation Data
- Transmitted to Gemini API only
- Not stored locally by default
- Opt-in conversation logging (disabled by default)
- Logs stored locally, not cloud

### Audio Data
- Captured from local speakers only
- Not recorded to disk
- Transcribed by Gemini, then discarded
- No permanent storage of audio

---

## Customization Options

### Visual Customization
- 🎨 App name/branding
- 🎨 Icon/emoji
- 🎨 Color scheme (via Styles.xaml)
- 🎨 Window layout
- 🎨 Overlay styling

### Functional Customization
- 🔧 System prompt (AI personality)
- 🔧 Model selection
- 🔧 Temperature (creativity)
- 🔧 Token limits
- 🔧 Streaming on/off
- 🔧 Privacy mode
- 🔧 Storage paths
- 🔧 Logging levels

### Interview Customization
- 🔧 Question detection patterns
- 🔧 Response generation styles
- 🔧 Auto-response rules
- 🔧 Audio capture device

---

## Troubleshooting Matrix

| Problem | Cause | Solution |
|---------|-------|----------|
| "No API key found" | Not configured | Go to Settings → Enter key → Save |
| Response is generic | No system prompt | Customize in Settings |
| Streaming too slow | Low internet | Check connection, disable streaming |
| Privacy mode not working | Old Windows | Need 10.2004+, check logs |
| Audio not captured | Device incompatible | Try different speaker, check drivers |
| Question not detected | Pattern mismatch | Use Chat tab instead |
| Crash on startup | Corrupted settings | Delete `settings.json`, restart |
| "Network error" | API unreachable | Check internet, check API status |

---

## Future Roadmap

### Potential Features
- 🚀 Custom question patterns for interviews
- 🚀 Voice cloning for response reading
- 🚀 Multi-language interview support
- 🚀 Interview analytics/performance tracking
- 🚀 Keyboard shortcuts and hotkeys
- 🚀 Plugin system for extensibility
- 🚀 Cloud sync (optional)
- 🚀 Alternative LLM backends (OpenAI, Claude, etc.)

### Known Limitations
- ⚠️ Audio capture requires WASAPI support
- ⚠️ Speech recognition quality depends on audio
- ⚠️ Interview responses may lack nuance
- ⚠️ No offline mode (requires internet)

---

## Version History

### v1.0.0 (Current)
- ✅ Initial release
- ✅ Chat interface
- ✅ Screen understanding
- ✅ Voice input
- ✅ Overlay window
- ✅ Privacy mode
- ✅ Interview Assistant (NEW!)

### v0.9.0 (Pre-release)
- Initial development
- Core features (chat, screen, voice)

---

## License

MIT License - See LICENSE file

---

## Support & Resources

- **GitHub**: https://github.com/MuftiFaris/Hidden-Gem
- **Google Gemini**: https://ai.google.dev/
- **NAudio Docs**: https://github.com/naudio/NAudio
- **WPF Docs**: https://docs.microsoft.com/en-us/dotnet/desktop/wpf/
- **Serilog Docs**: https://serilog.net/

---

**Last Updated**: September 2026
**Current Version**: 1.0.0
**Platform**: Windows 10/11 (x64)
**Framework**: .NET 8.0
