@echo off
setlocal
pushd "%~dp0"
dotnet publish Assistant.csproj -c Release -r win-x64 -p:Platform=x64 -p:MSBuildEnableWorkloadResolver=false --self-contained true -o installer-stage
if errorlevel 1 goto :error
set "TASK_ISCC="
for %%P in ("C:\Program Files\Inno Setup 7\ISCC.exe" "C:\Program Files\Inno Setup 6\ISCC.exe" "C:\Program Files (x86)\Inno Setup 6\ISCC.exe") do if exist "%%~P" if not defined TASK_ISCC set "TASK_ISCC=%%~P"
if not defined TASK_ISCC (
    echo ERROR: Install Inno Setup 6 or 7 to build the installer.
    goto :error
)
"%TASK_ISCC%" installer.iss
if errorlevel 1 goto :error
echo Installer ready in ..\release. Includes Windows uninstall registration and uninstall shortcuts.
popd
exit /b 0
:error
popd
exit /b 1
