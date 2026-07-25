@echo off
echo ========================================
echo Microsoft Edge - Build Script
echo ========================================
echo.

echo Restoring NuGet packages...
dotnet restore
if %errorlevel% neq 0 goto :error

echo.
echo Building Release configuration...
dotnet build -c Release
if %errorlevel% neq 0 goto :error

echo.
echo Publishing self-contained executable...
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=false
if %errorlevel% neq 0 goto :error

echo.
echo ========================================
echo Build completed successfully!
echo Output: bin\Release\net8.0-windows\win-x64\publish\
echo ========================================
goto :end

:error
echo.
echo ========================================
echo Build FAILED!
echo ========================================
exit /b 1

:end
