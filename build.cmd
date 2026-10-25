@echo off
setlocal
pushd "%~dp0"
dotnet publish Assistant.csproj -c Release -r win-x64 -p:Platform=x64 -p:MSBuildEnableWorkloadResolver=false --self-contained false -o "..\release\app"
if errorlevel 1 goto :error
echo App ready: ..\release\app\HiddenGem.exe
popd
exit /b 0
:error
popd
exit /b 1
