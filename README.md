# yt-mp3-grabber

A lightweight tool to download and convert YouTube audio to MP3 directly from your browser. It runs a local .NET 8 REST API that handles the heavy lifting — `yt-dlp` fetches the audio stream, `ffmpeg` converts it, and a Chrome/Edge extension wires everything together so you never have to leave the page.

---

## How it works

Open any YouTube video, click the extension popup, pick an output folder and hit **MP3**. That's it. The file lands in your folder as a properly named `.mp3`.

Under the hood:

- The browser extension sends the page URL to a local API running on `http://localhost:5000`
- The API spawns a `yt-dlp` process, downloads the best available audio stream, and pipes it through `ffmpeg` for conversion
- CORS is locked to `chrome-extension://` origins — nothing is exposed to the open web
- Files are named automatically using the video title (`%(title)s.%(ext)s`)

---

## Requirements

- Windows 10 or 11
- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- `yt-dlp` — `winget install yt-dlp.yt-dlp`
- `ffmpeg` — `winget install ffmpeg`
- Chrome or Edge (Manifest V3)

Both `yt-dlp` and `ffmpeg` must be available in your system `PATH`. The API checks for them at startup and will log a warning if either is missing.

---

## Installation

```bash
git clone https://github.com/Dome309/yt-mp3-grabber.git
cd yt-mp3-grabber
dotnet restore
dotnet build
```

---

## Running the API

### Easier option on Windows

Double-click [start-server.bat](start-server.bat) in the project folder. This will open a terminal window and start the server automatically.

### Manual option

```bash
dotnet run
```

Keep the console window open while you use the extension. The API listens on `http://localhost:5000` and needs to stay running in the background.

---

## Loading the extension

1. Open `chrome://extensions` (Chrome) or `edge://extensions` (Edge)
2. Enable **Developer mode** (toggle in the top-right corner)
3. Click **Load unpacked** and select the project root folder
4. The **YT MP3** button should now appear in your toolbar

If the popup buttons don't show up, check that the content script is active on the current tab and that the background service worker is connected — the extension console will show any connection errors.

---

## Usage

1. Navigate to a YouTube video (`youtube.com` or `music.youtube.com`)
2. Open the extension popup
3. On first use, click the **folder** button to pick an output directory — the path is saved for future sessions
4. Paste the video URL if it isn't pre-filled, then press **MP3**
5. Wait for the confirmation message; the file will appear in your chosen folder

---

## API reference

### `GET /select-folder`

Opens a native Windows folder picker dialog.

**Response (success)**
```json
{ "path": "C:\\Users\\you\\Music" }
```

**Response (cancelled or error)**
```json
{ "error": "No folder selected" }
```

---

### `POST /download`

Triggers a download and conversion job.

**Request body**
```json
{
  "url": "https://www.youtube.com/watch?v=...",
  "folder": "C:\\Users\\you\\Music"
}
```

**Responses**

| Status | Meaning |
|--------|---------|
| `200` | Download completed successfully |
| `400` | Missing or invalid `url` / `folder` |
| `500` | `yt-dlp` or `ffmpeg` encountered an error |

Errors from `yt-dlp` are forwarded as-is in the response body to make debugging straightforward.

---

## Project structure

```
yt-mp3-grabber/
├── Program.cs            # API server, process management, folder picker
├── manifest.json         # Extension manifest (Manifest V3)
├── YtMp3Grabber.background.js         # Service worker — bridges extension and local API
└── YtMp3Grabber.content_script.js     # Injected into YouTube pages
```

---

## Supported URLs

- `https://www.youtube.com/*`
- `https://music.youtube.com/*`

Playlists and individual videos are both handled by `yt-dlp` natively, though the extension UI is designed around single-video downloads. Batch support depends on what you pass to the `/download` endpoint directly.

---

## Troubleshooting

**The API starts but yt-dlp isn't found**
Make sure `yt-dlp` is in your PATH. Run `yt-dlp --version` in a new terminal to verify. If you installed via `winget`, you may need to restart your terminal session.

**The extension popup is blank**
Reload the extension from the extensions page after making any changes to the source files. Cached service workers can sometimes cause stale state.

**Download completes but no file appears**
Check the console output of the API — `ffmpeg` errors (missing codec, permission denied on the output folder) are logged there.

---

## Legal notice

This project is provided for educational and personal use only. Downloading content from YouTube may violate their [Terms of Service](https://www.youtube.com/t/terms). You are responsible for ensuring that any content you download is either your own, in the public domain, or that you have explicit permission from the rights holder. The author assumes no liability for misuse.