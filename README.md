# yt-mp3-grabber

Un semplice backend .NET + estensione browser per scaricare tracce MP3 da YouTube usando `yt-dlp` + `ffmpeg`.

## Funzionalità

- API REST localmente su `http://localhost:5000`
- Selezione cartella con finestra GUI
- Download e conversione audio in MP3 con `yt-dlp`
- Compatibile con estensione Chrome/Edge tramite CORS limitato a `chrome-extension://`
- Auto-detect di `yt-dlp` e `ffmpeg` in PATH
- Logging console dettagliato e gestione errori

## Requisiti

- Windows 10/11
- .NET 8 SDK
- `yt-dlp` (installabile con `winget install yt-dlp.yt-dlp`)
- `ffmpeg` (installabile con `winget install ffmpeg`)

## Installazione

1. Clona il repository
2. Apri il terminale nella cartella del progetto
3. `dotnet restore`
4. `dotnet build`

## Esecuzione

1. Avvia l'app con `dotnet run` o lancia la soluzione da Visual Studio
2. Assicurati che la finestra rimanga aperta (il servizio API gira finché il processo è attivo)
3. L'estensione browser comunica a `http://localhost:5000`

## Endpoints

- `GET /select-folder`: mostra il selettore cartelle di Windows
- `POST /download`:
  - body JSON: `{ "url": "<YouTube URL>", "folder": "C:\\path\\to\\output" }`
  - restituisce 200 su successo, 400 su validazione URL/cartella, 500 su errore yt-dlp

## Come usare con l'estensione

1. Installa l'estensione dalla cartella (in Developer mode in Chrome/Edge)
2. Assicurati che l'API sia in ascolto `http://localhost:5000`
3. Nel popup, inserisci l'URL YouTube e premi `Download`
4. Seleziona cartella (prima chiamata a `/select-folder`)

## Debug

- Messaggi yt-dlp in log: `[yt-dlp] ...`
- Se c'è errore di `ffmpeg` o `ffprobe`:
  - `winget install ffmpeg`
  - riavvia API
- Per bypassare l'errore di exit code `yt-dlp`, il comando viene eseguito con `.WithValidation(CommandResultValidation.None)` e i dettagli restano nel log.

## File principali

- `Program.cs`: server API e invocazione yt-dlp
- `background.js`: comunicazione con l'API locale
- `content_script.js`: integrazione pagina YouTube
- `manifest.json`: permessi, CORS e URL

## Commit minimali

Il commit successivo include i file necessari:
- `README.md`
- `Program.cs`
- `background.js`
- `content_script.js`
- `manifest.json`

