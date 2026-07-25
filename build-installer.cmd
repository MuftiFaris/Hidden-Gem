@echo off
echo ========================================
echo Building Installer
echo ========================================
echo.

echo Step 1: Building application...
call build.cmd
if %errorlevel% neq 0 goto :error

echo.
echo Step 2: Creating installer with Inno Setup...
echo (Make sure Inno Setup is installed: https://jrsoftware.org/isinfo.php)
echo.

if not exist "C:\Program Files\Inno Setup 7\ISCC.exe" (
    echo ERROR: Inno Setup not found!
    echo Please install from: https://jrsoftware.org/isinfo.php
    goto :error
)

"C:\Program Files\Inno Setup 7\ISCC.exe" installer.iss
if %errorlevel% neq 0 goto :error

echo.
echo ========================================
echo Installer created successfully!
echo Output: installer-output\HiddenGem-Setup-v1.0.0.exe
echo ========================================
goto :end

:error
echo.
echo ========================================
echo Build FAILED!
echo ========================================
exit /b 1

:end
