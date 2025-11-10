using System;
using System.Drawing;
using System.IO;
using System.Reflection;

namespace ShipvillanWin;

/// <summary>
/// Helper class to load embedded icon resources.
/// </summary>
public static class IconHelper
{
    /// <summary>
    /// Loads an icon from embedded resources.
    /// Returns null if the icon cannot be loaded (falls back to SystemIcons).
    /// </summary>
    /// <param name="resourceName">Name of the embedded resource (e.g., "ShipvillanWin.icon.ico")</param>
    public static Icon? LoadEmbeddedIcon(string resourceName)
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream(resourceName);

            if (stream == null)
            {
                System.Diagnostics.Debug.WriteLine($"IconHelper: Embedded icon '{resourceName}' not found");
                return null;
            }

            return new Icon(stream);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"IconHelper: Failed to load icon '{resourceName}': {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Gets the application tray icon.
    /// Attempts to load custom icon, falls back to system icon if unavailable.
    /// </summary>
    public static Icon GetTrayIcon()
    {
        // Try to load custom icon
        var customIcon = LoadEmbeddedIcon("ShipvillanWin.Resources.tray-icon.ico");

        if (customIcon != null)
            return customIcon;

        // Fallback to system icon
        return SystemIcons.Application;
    }
}
