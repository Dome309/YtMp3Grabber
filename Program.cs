using CliWrap;
using Microsoft.AspNetCore.Mvc;

// ──────────────────────────────────────────────
//  CONFIGURAZIONE
// ──────────────────────────────────────────────
const string API_URL        = "http://localhost:5000";
const int    MAX_URL_LENGTH = 200;

// Cerca yt-dlp nel PATH di sistema + utente.
// Necessario perché i processi avviati da IDE/servizi non ereditano
// sempre il PATH della sessione utente corrente.
var ytDlpExe = FindExecutable("yt-dlp");
if (ytDlpExe is null)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("ERRORE: yt-dlp non trovato!");
    Console.WriteLine("Installa con: winget install yt-dlp.yt-dlp");
    Console.WriteLine("Poi riavvia questa finestra.");
    Console.ResetColor();
    return;
}
Console.WriteLine($"yt-dlp  → {ytDlpExe}");

var ffmpegExe = FindExecutable("ffmpeg");
if (ffmpegExe is null)
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine("AVVISO: ffmpeg non trovato!");
    Console.WriteLine("Installa con: winget install ffmpeg");
    Console.ResetColor();
}
else
{
    Console.WriteLine($"ffmpeg → {ffmpegExe}");
}

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// CORS: accetta richieste SOLO da estensioni browser (chrome-extension://)
builder.Services.AddCors(options =>
{
    options.AddPolicy("ExtensionOnly", policy =>
    {
        policy
            .SetIsOriginAllowed(origin => origin.StartsWith("chrome-extension://"))
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

var app = builder.Build();
app.UseCors("ExtensionOnly");

// ──────────────────────────────────────────────
//  ENDPOINT: GET /select-folder
// ──────────────────────────────────────────────
app.MapGet("/select-folder", () =>
{
    string? selectedPath = null;

    var thread = new Thread(() =>
    {
        using var dialog = new FolderBrowserDialog
        {
            Description            = "Seleziona la cartella dove salvare i tuoi MP3",
            UseDescriptionForTitle = true,
            ShowNewFolderButton    = true
        };

        var owner = new NativeWindow();
        owner.AssignHandle(GetForegroundWindowHandle());

        if (dialog.ShowDialog(owner) == DialogResult.OK)
            selectedPath = dialog.SelectedPath;
    });

    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    thread.Join();

    return selectedPath is not null
        ? Results.Ok(new { path = selectedPath })
        : Results.BadRequest(new { error = "Nessuna cartella selezionata." });
});

// ──────────────────────────────────────────────
//  ENDPOINT: POST /download
// ──────────────────────────────────────────────
app.MapPost("/download", async ([FromBody] DownloadRequest request, ILogger<Program> logger) =>
{
    if (!IsValidYouTubeUrl(request.Url))
    {
        logger.LogWarning("URL non valido: {Url}", request.Url);
        return Results.BadRequest(new { error = "URL non valido. Solo URL YouTube sono accettati." });
    }

    if (string.IsNullOrWhiteSpace(request.Folder) || !Directory.Exists(request.Folder))
    {
        logger.LogWarning("Cartella non valida: {Folder}", request.Folder);
        return Results.BadRequest(new { error = "Cartella di destinazione non valida o non esistente." });
    }

    var outputTemplate = Path.Combine(request.Folder, "%(title)s.%(ext)s");
    logger.LogInformation("Avvio download: {Url} → {Folder}", request.Url, request.Folder);

    try
    {
        var args = new List<string>
        {
            "--extract-audio",
            "--audio-format",  "mp3",
            "--audio-quality", "0",
            "--no-playlist",
            "--no-mtime"
        };

        // Aggiungi ffmpeg path se trovato
        if (ffmpegExe is not null)
        {
            args.Add("--ffmpeg-location");
            args.Add(ffmpegExe);
        }

        args.Add("-o");
        args.Add(outputTemplate);
        args.Add(request.Url);

        await Cli.Wrap(ytDlpExe)           // path completo, nessuna ambiguità
            .WithArguments(args)
            .WithStandardOutputPipe(PipeTarget.ToDelegate(line =>
                logger.LogInformation("[yt-dlp] {Line}", line)))
            .WithStandardErrorPipe(PipeTarget.ToDelegate(line =>
                logger.LogWarning("[yt-dlp] {Line}", line)))
            .WithValidation(CommandResultValidation.None)
            .ExecuteAsync();

        logger.LogInformation("Download completato: {Url}", request.Url);
        return Results.Ok(new { message = "Download completato! File salvato in: " + request.Folder });
    }
    catch (CliWrap.Exceptions.CommandExecutionException ex)
    {
        logger.LogError(ex, "yt-dlp ha restituito un errore");
        return Results.Problem(
            detail: "yt-dlp ha restituito un errore. Controlla che l'URL sia valido e che yt-dlp sia aggiornato (yt-dlp -U).",
            statusCode: 500);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Errore imprevisto");
        return Results.Problem(detail: ex.Message, statusCode: 500);
    }
});

Console.WriteLine("╔════════════════════════════════════╗");
Console.WriteLine("║     YT MP3 Downloader API          ║");
Console.WriteLine("║     In ascolto su localhost:5000   ║");
Console.WriteLine("╚════════════════════════════════════╝");
Console.WriteLine("Tieni questa finestra aperta mentre usi l'estensione.");

app.Run(API_URL);

// ──────────────────────────────────────────────
//  HELPERS
// ──────────────────────────────────────────────

// Cerca un eseguibile nel PATH di sistema e utente.
// Legge entrambe le variabili d'ambiente così funziona anche
// quando il processo non eredita il PATH della sessione corrente.
static string? FindExecutable(string name)
{
    var systemPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine) ?? "";
    var userPath   = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User)   ?? "";
    var allPaths   = $"{systemPath};{userPath}";

    foreach (var dir in allPaths.Split(';', StringSplitOptions.RemoveEmptyEntries))
    {
        var full = Path.Combine(dir.Trim(), name + ".exe");
        if (File.Exists(full)) return full;
    }
    return null;
}

static bool IsValidYouTubeUrl(string? url)
{
    if (string.IsNullOrWhiteSpace(url)) return false;
    if (url.Length > MAX_URL_LENGTH)    return false;
    if (url.Contains('\n') || url.Contains('\r') || url.Contains('"')) return false;

    return url.StartsWith("https://www.youtube.com/watch?v=") ||
           url.StartsWith("https://youtube.com/watch?v=")     ||
           url.StartsWith("https://youtu.be/")                ||
           url.StartsWith("https://music.youtube.com/watch?v=");
}

static nint GetForegroundWindowHandle()
{
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern nint GetForegroundWindow();
    return GetForegroundWindow();
}

record DownloadRequest(string Url, string Folder);