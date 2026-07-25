# Create Installer

## Option 1: Portable (No Installer) - READY NOW ✓

Sudah bisa langsung dijalankan:

```
bin\Release\net8.0-windows\win-x64\publish\HiddenGem.exe
```

Copy folder `publish` ke mana aja, double-click `HiddenGem.exe`

## Option 2: Create Professional Installer

### Requirements
1. Download **Inno Setup 6**: https://jrsoftware.org/isinfo.php
2. Install ke default location
3. Run build script

### Build Installer
```cmd
build-installer.cmd
```

Output: `installer-output\HiddenGem-Setup-v1.0.0.exe`

### Manual Build
```cmd
"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer.iss
```

## Files Included in Installer
- HiddenGem.exe
- All DLLs
- Resources
- Config files

## Installer Features
- Desktop shortcut (optional)
- Start menu shortcut
- Startup launch (optional)
- Clean uninstall
- Checks for .NET 8.0
