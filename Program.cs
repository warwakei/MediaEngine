using Windows.Media.Control;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Drawing;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Check for command line arguments
bool isInstalled = args.Length > 0 && (args[0] == "-i" || args[0] == "--installed");

if (isInstalled)
{
    // Create installed marker file
    string markerFile = Path.Combine(AppContext.BaseDirectory, "installed.iww");
    File.WriteAllText(markerFile, "");
    
    // Run in tray mode
    RunInTray();
}
else
{
    // Check if already installed
    string markerFile = Path.Combine(AppContext.BaseDirectory, "installed.iww");
    if (File.Exists(markerFile))
    {
        // Already installed, run in tray mode
        RunInTray();
    }
    else
    {
        // First run - show installation prompt
        ShowInstallationPrompt();
    }
}

void ShowInstallationPrompt()
{
    Console.WriteLine("Do you want install warwakei projects Media Engine?");
    Console.WriteLine("1. Yes");
    Console.WriteLine("2. No");
    Console.Write("> ");
    
    string? choice = Console.ReadLine();
    
    if (choice == "1")
    {
        InstallEngine();
    }
    else
    {
        Console.WriteLine("Installation cancelled.");
        Environment.Exit(0);
    }
}

void InstallEngine()
{
    try
    {
        string username = Environment.UserName;
        string installPath = Path.Combine("C:\\Users", username, "AppData\\Roaming\\warwakei\\utility");
        string exePath = Path.Combine(installPath, "MediaEngine.exe");
        
        // Create directory if it doesn't exist
        Directory.CreateDirectory(installPath);
        
        // Copy current executable to installation path
        string currentExe = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "";
        if (File.Exists(currentExe))
        {
            File.Copy(currentExe, exePath, true);
            
            // Create startup shortcut
            CreateStartupShortcut(exePath);
            
            Console.WriteLine("Installed engine. Opened in tray");
            
            // Start the installed engine with -i flag
            System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = exePath,
                Arguments = "-i",
                UseShellExecute = true,
                CreateNoWindow = true
            };
            System.Diagnostics.Process.Start(psi);
            
            Console.WriteLine("Press Enter to exit");
            Console.ReadLine();
        }
        else
        {
            Console.WriteLine("Error: Could not find current executable");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Installation error: {ex.Message}");
    }
}

void CreateStartupShortcut(string exePath)
{
    try
    {
        string username = Environment.UserName;
        string startupFolder = Path.Combine("C:\\Users", username, "AppData\\Roaming\\Microsoft\\Windows\\Start Menu\\Programs\\Startup");
        string shortcutPath = Path.Combine(startupFolder, "MediaEngine.lnk");
        
        // Create WshShell COM object
        dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
        dynamic shortcut = shell.CreateShortcut(shortcutPath);
        
        shortcut.TargetPath = exePath;
        shortcut.Arguments = "-i";
        shortcut.WorkingDirectory = Path.GetDirectoryName(exePath);
        shortcut.WindowStyle = 7; // Minimized
        shortcut.Save();
        
        System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shortcut);
        System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Warning: Could not create startup shortcut: {ex.Message}");
    }
}

void RunInTray()
{
    // Hide console window
    var handle = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
    ShowWindow(handle, 0); // SW_HIDE
    
    // Start web service in background
    Task.Run(() => StartWebService());
    
    // Keep application running
    System.Threading.Thread.Sleep(System.Threading.Timeout.Infinite);
}

void StartWebService()
{
    try
    {
        var builder = WebApplication.CreateBuilder(new string[] { });
        
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton<MediaEngineService>();
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("AllowAll", policy =>
            {
                policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
            });
        });
        
        var app = builder.Build();
        app.UseCors("AllowAll");
        
        var service = app.Services.GetRequiredService<MediaEngineService>();
        _ = service.StartListeningAsync();
        
        app.MapGet("/api/track", async () =>
        {
            var track = await service.GetCurrentTrackAsync();
            return Results.Json(track);
        });
        
        app.MapPost("/api/delay", (HttpContext context) =>
        {
            var query = context.Request.Query;
            if (!int.TryParse(query["delayMs"], out int delayMs))
                return Results.Json(new { error = "Invalid delayMs parameter" });
            
            if (delayMs < 100 || delayMs > 60000)
                return Results.Json(new { error = "Delay must be between 100 and 60000 ms" });
            
            service.SetDelay(delayMs);
            return Results.Json(new { message = $"Delay set to {delayMs}ms" });
        });
        
        app.MapGet("/api/status", () =>
        {
            return Results.Json(new { version = "0.01", status = "running" });
        });
        
        app.Run("http://localhost:5000");
    }
    catch
    {
        // Silently fail
    }
}

[DllImport("user32.dll")]
static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

public class MediaEngineService
{
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private int _delayMs = 3000;

    public void SetDelay(int delayMs)
    {
        _delayMs = delayMs;
    }

    public async Task StartListeningAsync()
    {
        try
        {
            _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        }
        catch
        {
        }
    }

    public async Task<TrackInfo> GetCurrentTrackAsync()
    {
        if (_manager == null)
            return new TrackInfo { Title = "none" };

        try
        {
            var session = _manager.GetCurrentSession();
            
            if (session == null)
                return new TrackInfo { Title = "none" };

            var props = await session.TryGetMediaPropertiesAsync();
            var playbackInfo = session.GetPlaybackInfo();

            string title = props.Title;
            string artist = props.Artist;

            if (string.IsNullOrWhiteSpace(title))
                return new TrackInfo { Title = "none" };

            string display = $"{artist} - {title}".Trim();
            if (display.StartsWith("-"))
                display = title;

            bool isPaused = playbackInfo?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused;

            return new TrackInfo
            {
                Title = title,
                Artist = artist,
                Display = display,
                IsPaused = isPaused,
                Delay = _delayMs
            };
        }
        catch
        {
            return new TrackInfo { Title = "none" };
        }
    }
}

public class TrackInfo
{
    public string Title { get; set; } = "none";
    public string Artist { get; set; } = "";
    public string Display { get; set; } = "none";
    public bool IsPaused { get; set; } = false;
    public int Delay { get; set; } = 3000;
}
