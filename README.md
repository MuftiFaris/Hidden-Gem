# Hidden Gem

Windows desktop assistant built with WPF and .NET 8.

- Gemini or 9Router / OpenAI-compatible APIs.
- Streaming chat, selectable Markdown, copy actions, DOCX/Markdown/TXT export.
- Selected-window analysis and speaker transcription.
- API keys stored separately by provider in Windows Credential Manager.

## Install

Run the latest `HiddenGem-Setup-v*.exe` from `../release/`.
Exit the running app from its tray menu before upgrading.
The installer includes the .NET runtime and an uninstaller.
Uninstall through Windows **Installed apps → Hidden Gem** or the Start Menu shortcut.

## Build

Requires the .NET 8+ SDK; installer builds also need Inno Setup 6 or 7.

```powershell
.\build.cmd
.\build-installer.cmd
```

Outputs, relative to this repository:

- `../release/HiddenGem-Setup-v<version>.exe`: installer.
- `../release/app/HiddenGem.exe`: app with supporting files.
- `installer-stage/`: temporary packaging output.

Build output, local design metadata, and working notes are excluded from Git.
Keep code, assets, build scripts, README, and LICENSE in the repository.
Installers belong in GitHub Releases when publishing, rather than source commits.

## Connect

In Settings, select the provider and save its API key.
For local 9Router, keep its server running and use its dashboard URL/key;
the default API URL is `http://localhost:20128/v1`.
Refresh models, choose the exact model ID, save, and test the connection.
Models are not necessarily free; provider pricing and quotas apply.
Screen analysis requires vision support; 9Router voice requires an STT route/model.
Keep the selected window visible. Capture privacy is a Windows request, not a guarantee.
