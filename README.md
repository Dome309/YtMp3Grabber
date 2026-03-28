# yt-mp3-grabber

A simple .NET backend + browser extension to download MP3 audio from YouTube with `yt-dlp` + `ffmpeg`.

## Features

- REST API on `http://localhost:5000`
- Windows folder picker UI
- Audio download and MP3 conversion via `yt-dlp`
- Chrome/Edge extension integration with CORS restricted to `chrome-extension://`
- Auto-detect `yt-dlp` and `ffmpeg` from PATH
- Detailed console logging and error handling

## Requirements

- Windows 10/11
- .NET 8 SDK
- `yt-dlp` (install with `winget install yt-dlp.yt-dlp`)
- `ffmpeg` (install with `winget install ffmpeg`)

## Installation

1. Clone the repository
2. Open a terminal in the project folder
3. `dotnet restore`
4. `dotnet build`

## Run

1. Start the app with `dotnet run` or via Visual Studio
2. Keep the console window open while using the extension (API is active until the process stops)
3. Extension communicates with `http://localhost:5000`

## Endpoints

- `GET /select-folder`: shows the Windows folder picker
- `POST /download`:
  - JSON body: `{ "url": "<YouTube URL>", "folder": "C:\\path\\to\\output" }`
  - returns 200 on success, 400 for invalid URL/folder, 500 for yt-dlp errors

## Using with the extension

1. Load the extension in Developer mode in Chrome/Edge from the folder
2. Ensure the API listens on `http://localhost:5000`
3. Enter a YouTube URL and click `Download`
4. Select a folder when prompted (`/select-folder` call)

## Debug

- yt-dlp logs are prefixed with `[yt-dlp] ...`
- If `ffmpeg`/`ffprobe` is missing:
  - `winget install ffmpeg`
  - restart the API
- Command exit codes are accepted with `.WithValidation(CommandResultValidation.None)` in yt-dlp invocation; check logs for details.

## Main files

- `Program.cs`: API server, yt-dlp process handling
- `background.js`: extension <-> local API calls
- `content_script.js`: YouTube page integration
- `manifest.json`: extension permissions and settings

## Committed files

The main files included in the commit:
- `README.md`
- `Program.cs`
- `background.js`
- `content_script.js`
- `manifest.json`
- `YoutubeMp3Extension.sln`
- `YtMp3Api.csproj`

