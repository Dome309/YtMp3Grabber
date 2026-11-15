using CliWrap;
using Microsoft.AspNetCore.Mvc;

// ──────────────────────────────────────────────
//  CONFIGURATION
// ──────────────────────────────────────────────
const string API_URL        = "http://localhost:5000";
const int    MAX_URL_LENGTH = 200;

// Search yt-dlp in both machine and user PATH.
// This is needed because processes launched from IDEs/services may not inherit
// the current user session PATH.
var ytDlpExe = FindExecutable("yt-dlp");
if (ytDlpExe is null)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("ERROR: yt-dlp not found!");
    Console.WriteLine("Install with: winget install yt-dlp.yt-dlp");
    Console.WriteLine("Then restart this terminal.");
    Console.ResetColor();
    return;
}
Console.WriteLine($"yt-dlp  -> {ytDlpExe}");

var ffmpegExe = FindExecutable("ffmpeg");
if (ffmpegExe is null)
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine("WARNING: ffmpeg not found!");
    Console.WriteLine("Install with: winget install ffmpeg");
    Console.ResetColor();
}
else
{
    Console.WriteLine($"ffmpeg → {ffmpegExe}");
}

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// CORS: only allow requests from browser extensions (chrome-extension://)
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
        Description            = "Select the folder where mp3 files should be saved",
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
        : Results.BadRequest(new { error = "No folder selected." });
});

// ──────────────────────────────────────────────
//  ENDPOINT: POST /download
// ──────────────────────────────────────────────
app.MapPost("/download", async ([FromBody] DownloadRequest request, ILogger<Program> logger) =>
{
    if (!IsValidYouTubeUrl(request.Url))
    {
        logger.LogWarning("Invalid URL: {Url}", request.Url);
        return Results.BadRequest(new { error = "Invalid URL. Only YouTube URLs are accepted." });
    }

    if (string.IsNullOrWhiteSpace(request.Folder) || !Directory.Exists(request.Folder))
    {
        logger.LogWarning("Invalid folder path: {Folder}", request.Folder);
        return Results.BadRequest(new { error = "Invalid or non-existent destination folder." });
    }

    var outputTemplate = Path.Combine(request.Folder, "%(title)s.%(ext)s");
    logger.LogInformation("Starting download: {Url} → {Folder}", request.Url, request.Folder);

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

        // Add ffmpeg path if available
        if (ffmpegExe is not null)
        {
            args.Add("--ffmpeg-location");
            args.Add(ffmpegExe);
        }

        args.Add("-o");
        args.Add(outputTemplate);
        args.Add(request.Url);

        var result = await Cli.Wrap(ytDlpExe)           // full path, no ambiguity
            .WithArguments(args)
            .WithStandardOutputPipe(PipeTarget.ToDelegate(line =>
                logger.LogInformation("[yt-dlp] {Line}", line)))
            .WithStandardErrorPipe(PipeTarget.ToDelegate(line =>
                logger.LogWarning("[yt-dlp] {Line}", line)))
            .WithValidation(CommandResultValidation.None)
            .ExecuteAsync();

        if (result.ExitCode != 0)
        {
            logger.LogError("yt-dlp exited with code {Code} for {Url}", result.ExitCode, request.Url);
            return Results.Problem(
                detail: "yt-dlp returned an error. Check that the URL is valid and yt-dlp is up to date (winget upgrade yt-dlp.yt-dlp).",
                statusCode: 500);
        }

        logger.LogInformation("Download completed: {Url}", request.Url);
        return Results.Ok(new { message = "Download completed! File saved in: " + request.Folder });
    }
    catch (CliWrap.Exceptions.CommandExecutionException ex)
    {
        logger.LogError(ex, "yt-dlp returned an error");
        return Results.Problem(
            detail: "yt-dlp returned an error. Check that the URL is valid and yt-dlp is up to date (yt-dlp -U).",
            statusCode: 500);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Unexpected error");
        return Results.Problem(detail: ex.Message, statusCode: 500);
    }
});

Console.WriteLine("╔════════════════════════════════════╗");
Console.WriteLine("║     YT MP3 Downloader API          ║");
Console.WriteLine("║     Listening on localhost:5000    ║");
Console.WriteLine("╚════════════════════════════════════╝");
Console.WriteLine("Keep this window open while using the extension.");

app.Run(API_URL);

// ──────────────────────────────────────────────
//  HELPERS
// ──────────────────────────────────────────────

// Find an executable in machine and user PATH.
// Reads both environment variables so it works even when the process
// does not inherit the current session PATH.
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