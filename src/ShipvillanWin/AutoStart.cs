using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;
using System.Security;

namespace ShipvillanWin;

/// <summary>
/// Manages Windows auto-start functionality via the registry Run key.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class AutoStart
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "ShipvillanWin";

    /// <summary>
    /// Ensures the application is configured to start automatically at user login.
    /// If auto-start is not enabled, this method will attempt to enable it.
    /// Exceptions are caught and logged to debug output.
    /// </summary>
    public static void EnsureEnabled()
    {
        try
        {
            if (!IsEnabled())
            {
                Enable();
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            Debug.WriteLine($"AutoStart: Access denied when trying to modify registry: {ex.Message}");
            throw;
        }
        catch (SecurityException ex)
        {
            Debug.WriteLine($"AutoStart: Security exception when accessing registry: {ex.Message}");
            throw;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"AutoStart: Unexpected error: {ex}");
            throw;
        }
    }

    /// <summary>
    /// Checks if the application is currently configured to start automatically at user login.
    /// </summary>
    /// <returns>True if auto-start is enabled and points to the current executable; otherwise, false.</returns>
    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            if (key == null)
            {
                return false;
            }

            var existing = key.GetValue(AppName) as string;
            if (string.IsNullOrWhiteSpace(existing))
            {
                return false;
            }

            var exePath = GetExecutablePath();
            var existingPath = existing.Trim('"');

            return string.Equals(existingPath, exePath, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"AutoStart.IsEnabled: Error checking registry: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Enables auto-start by adding the application to the Windows Run registry key.
    /// </summary>
    /// <exception cref="UnauthorizedAccessException">Thrown when the user lacks permission to modify the registry.</exception>
    /// <exception cref="SecurityException">Thrown when a security error occurs accessing the registry.</exception>
    public static void Enable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
                      ?? Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);

        if (key == null)
        {
            throw new InvalidOperationException("Failed to open or create registry key for auto-start.");
        }

        var exePath = GetExecutablePath();
        key.SetValue(AppName, $"\"{exePath}\"", RegistryValueKind.String);

        Debug.WriteLine($"AutoStart enabled: {exePath}");
    }

    /// <summary>
    /// Disables auto-start by removing the application from the Windows Run registry key.
    /// </summary>
    public static void Disable()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            key?.DeleteValue(AppName, throwOnMissingValue: false);

            Debug.WriteLine("AutoStart disabled");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"AutoStart.Disable: Error removing registry value: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Gets the full path to the current executable.
    /// Works with both framework-dependent and self-contained deployments.
    /// </summary>
    /// <returns>The full path to the executable file.</returns>
    private static string GetExecutablePath()
    {
        // Try to get the path from the current process's main module
        var processPath = Process.GetCurrentProcess().MainModule?.FileName;

        if (!string.IsNullOrWhiteSpace(processPath) && File.Exists(processPath))
        {
            return processPath;
        }

        // Fallback: construct path from base directory
        var fallbackPath = Path.Combine(AppContext.BaseDirectory, $"{AppName}.exe");

        if (!File.Exists(fallbackPath))
        {
            Debug.WriteLine($"Warning: Executable not found at expected location: {fallbackPath}");
        }

        return fallbackPath;
    }
}
