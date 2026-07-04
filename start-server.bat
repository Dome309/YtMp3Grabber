@echo off
setlocal
cd /d "%~dp0"

where dotnet >nul 2>nul
if errorlevel 1 (
    echo .NET SDK not found.
    echo Install .NET 8 SDK and run this launcher again.
    echo.
    pause
    exit /b 1
)

echo Starting YT MP3 Grabber API...
echo.
dotnet run --project "%~dp0YtMp3Grabber.csproj"

if errorlevel 1 (
    echo.
    echo The server did not start correctly.
    echo Check the output above and press any key to exit.
    pause
)
