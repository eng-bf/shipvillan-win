using Squirrel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Net.Http;
using System.Text;

namespace ShipvillanWin;

public class UpdateService : IDisposable
{
    private readonly string _githubRepoUrl;
    private readonly System.Windows.Forms.Timer _dailyCheckTimer;
    private UpdateManager? _updateManager;
    private bool _isCheckingForUpdates = false;
    private readonly string _architecture;

    public event EventHandler<string>? UpdateStatusChanged;
    public event EventHandler<Exception>? UpdateError;

    public UpdateService(string githubRepoUrl)
    {
        _githubRepoUrl = githubRepoUrl;

        // Detect runtime architecture
        _architecture = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.X86 => "x86",
            _ => "x64" // Default to x64 for other architectures
        };

        Debug.WriteLine($"UpdateService initialized for {_architecture} architecture");

        // Set up daily update check timer (check every hour, but only update after 3pm PST)
        _dailyCheckTimer = new System.Windows.Forms.Timer();
        _dailyCheckTimer.Interval = 60 * 60 * 1000; // 1 hour
        _dailyCheckTimer.Tick += DailyCheckTimer_Tick;
        _dailyCheckTimer.Start();
    }

    /// <summary>
    /// Starts the update service and performs initial update check
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            // Handle Squirrel events (install, update, uninstall)
            SquirrelAwareApp.HandleEvents(
                onInitialInstall: OnAppInstall,
                onAppUpdate: OnAppUpdate,
                onAppUninstall: OnAppUninstall
            );

            // Perform initial update check after a short delay
            await Task.Delay(5000); // Wait 5 seconds after startup
            await CheckForUpdatesAsync(silent: true);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"UpdateService initialization error: {ex.Message}");
            UpdateError?.Invoke(this, ex);
        }
    }

    /// <summary>
    /// Checks if current time is after 3pm PST
    /// </summary>
    private bool IsWithinUpdateWindow()
    {
        var pstZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
        var pstNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, pstZone);

        // Only update after 3pm PST (15:00)
        return pstNow.Hour >= 15;
    }

    /// <summary>
    /// Timer callback for daily automatic update checks
    /// </summary>
    private async void DailyCheckTimer_Tick(object? sender, EventArgs e)
    {
        if (IsWithinUpdateWindow())
        {
            await CheckForUpdatesAsync(silent: true);
        }
    }

    /// <summary>
    /// Manually check for updates (called from tray menu)
    /// </summary>
    public async Task CheckForUpdatesManuallyAsync()
    {
        UpdateStatusChanged?.Invoke(this, "Checking for updates...");
        await CheckForUpdatesAsync(silent: false);
    }

    /// <summary>
    /// Creates an architecture-aware UpdateManager
    /// </summary>
    private Task<UpdateManager> CreateUpdateManagerAsync()
    {
        // For now, use the standard GitHub URL
        // The architecture-specific handling is done through different package IDs:
        // - ShipvillanWin-x86 for 32-bit
        // - ShipvillanWin-x64 for 64-bit
        // Note: This requires uploading architecture-specific RELEASES files to GitHub
        // TODO: Implement proper architecture-specific RELEASES file handling
        return Task.FromResult(new UpdateManager(_githubRepoUrl));
    }


    /// <summary>
    /// Main update check logic
    /// </summary>
    private async Task CheckForUpdatesAsync(bool silent)
    {
        if (_isCheckingForUpdates)
        {
            if (!silent)
                UpdateStatusChanged?.Invoke(this, "Update check already in progress...");
            return;
        }

        try
        {
            _isCheckingForUpdates = true;

            // Create update manager if not already created
            if (_updateManager == null)
            {
                _updateManager = await CreateUpdateManagerAsync();
            }

            if (!silent)
                UpdateStatusChanged?.Invoke(this, "Checking for updates...");

            // Check for updates
            var updateInfo = await _updateManager.CheckForUpdate();

            if (updateInfo == null || !updateInfo.ReleasesToApply.Any())
            {
                if (!silent)
                    UpdateStatusChanged?.Invoke(this, "You are running the latest version.");
                return;
            }

            // If silent mode, check if we're in update window
            if (silent && !IsWithinUpdateWindow())
            {
                Debug.WriteLine($"Updates available but outside update window. Will check again later.");
                return;
            }

            // Download and apply updates
            if (!silent)
                UpdateStatusChanged?.Invoke(this, "Downloading updates...");

            await _updateManager.UpdateApp();

            if (!silent)
                UpdateStatusChanged?.Invoke(this, "Update downloaded. Restarting application...");

            // Wait a moment before restarting
            await Task.Delay(2000);

            // Restart the application
            UpdateManager.RestartApp();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Update check error: {ex.Message}");
            if (!silent)
            {
                UpdateStatusChanged?.Invoke(this, $"Update check failed: {ex.Message}");
                UpdateError?.Invoke(this, ex);
            }
        }
        finally
        {
            _isCheckingForUpdates = false;
        }
    }

    /// <summary>
    /// Rollback to previous version
    /// </summary>
    public async Task RollbackToPreviousVersionAsync()
    {
        try
        {
            UpdateStatusChanged?.Invoke(this, "Rolling back to previous version...");

            if (_updateManager == null)
            {
                _updateManager = await CreateUpdateManagerAsync();
            }

            // Get all versions
            var updateInfo = await _updateManager.CheckForUpdate();

            // Squirrel keeps previous versions in the packages folder
            // We need to manually handle rollback by reinstalling the previous version
            var packagesPath = Path.Combine(
                Path.GetDirectoryName(Application.ExecutablePath) ?? "",
                "..",
                "packages"
            );

            if (Directory.Exists(packagesPath))
            {
                var packages = Directory.GetFiles(packagesPath, "*.nupkg")
                    .OrderByDescending(f => new FileInfo(f).LastWriteTime)
                    .ToList();

                if (packages.Count > 1)
                {
                    // The second package is the previous version
                    var previousVersion = packages[1];
                    UpdateStatusChanged?.Invoke(this, $"Found previous version. Reinstalling...");

                    // Note: Squirrel doesn't have built-in rollback, so we'll need to
                    // implement this by downloading and installing the specific version
                    UpdateStatusChanged?.Invoke(this, "Rollback requires manual intervention. Please contact IT.");
                }
                else
                {
                    UpdateStatusChanged?.Invoke(this, "No previous version found for rollback.");
                }
            }
            else
            {
                UpdateStatusChanged?.Invoke(this, "Cannot find packages folder for rollback.");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Rollback error: {ex.Message}");
            UpdateStatusChanged?.Invoke(this, $"Rollback failed: {ex.Message}");
            UpdateError?.Invoke(this, ex);
        }
    }

    #region Squirrel Event Handlers

    private static void OnAppInstall(SemanticVersion version, IAppTools tools)
    {
        // Create desktop and start menu shortcuts
        tools.CreateShortcutForThisExe(ShortcutLocation.StartMenu | ShortcutLocation.Desktop);

        // Enable auto-start on installation
        try
        {
            AutoStart.EnsureEnabled();
            Debug.WriteLine("Auto-start enabled during installation");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to enable auto-start during installation: {ex.Message}");
        }
    }

    private static void OnAppUpdate(SemanticVersion version, IAppTools tools)
    {
        // Update shortcuts
        tools.CreateShortcutForThisExe(ShortcutLocation.StartMenu | ShortcutLocation.Desktop);

        // Ensure auto-start remains enabled after update
        try
        {
            AutoStart.EnsureEnabled();
            Debug.WriteLine("Auto-start re-enabled during update");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to re-enable auto-start during update: {ex.Message}");
        }
    }

    private static void OnAppUninstall(SemanticVersion version, IAppTools tools)
    {
        // Remove shortcuts
        tools.RemoveShortcutForThisExe(ShortcutLocation.StartMenu | ShortcutLocation.Desktop);

        // Disable auto-start on uninstall
        try
        {
            AutoStart.Disable();
            Debug.WriteLine("Auto-start disabled during uninstall");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to disable auto-start during uninstall: {ex.Message}");
        }
    }

    #endregion

    public void Dispose()
    {
        _dailyCheckTimer?.Stop();
        _dailyCheckTimer?.Dispose();
        _updateManager?.Dispose();
    }
}

