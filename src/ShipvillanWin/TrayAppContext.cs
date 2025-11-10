using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace ShipvillanWin;

/// <summary>
/// Application context for the system tray application.
/// Manages the tray icon, context menu, and application lifecycle.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class TrayAppContext : ApplicationContext
{
    private const string AppTitle = "ShipvillanWin";

    private readonly NotifyIcon _trayIcon;
    private readonly ContextMenuStrip _contextMenu;

    public TrayAppContext()
    {
        // Ensure we auto-start at login (HKCU)
        InitializeAutoStart();

        // Build context menu
        _contextMenu = CreateContextMenu();

        // Initialize system tray icon
        _trayIcon = CreateTrayIcon(_contextMenu);
        _trayIcon.MouseUp += OnTrayIconMouseUp;
    }

    /// <summary>
    /// Attempts to enable auto-start functionality.
    /// Logs any errors to debug output without failing the application startup.
    /// </summary>
    private static void InitializeAutoStart()
    {
        try
        {
            AutoStart.EnsureEnabled();
        }
        catch (UnauthorizedAccessException ex)
        {
            Debug.WriteLine($"Auto-start initialization failed due to insufficient permissions: {ex.Message}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Auto-start initialization failed: {ex}");
        }
    }

    /// <summary>
    /// Creates the context menu for the tray icon.
    /// </summary>
    private ContextMenuStrip CreateContextMenu()
    {
        var helloItem = new ToolStripMenuItem("Hello World", null, OnHelloClick);
        var exitItem = new ToolStripMenuItem("Exit", null, OnExitClick);

        var menu = new ContextMenuStrip();
        menu.Items.Add(helloItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);

        return menu;
    }

    /// <summary>
    /// Creates and configures the system tray icon.
    /// </summary>
    private static NotifyIcon CreateTrayIcon(ContextMenuStrip contextMenu)
    {
        return new NotifyIcon
        {
            Text = AppTitle,
            Icon = SystemIcons.Application,
            ContextMenuStrip = contextMenu,
            Visible = true
        };
    }

    /// <summary>
    /// Handles mouse clicks on the tray icon.
    /// Left-click displays the context menu at the cursor position.
    /// </summary>
    private void OnTrayIconMouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            // Show context menu at cursor position
            _contextMenu.Show(Cursor.Position);
        }
    }

    /// <summary>
    /// Handles the "Hello World" menu item click.
    /// </summary>
    private void OnHelloClick(object? sender, EventArgs e)
    {
        MessageBox.Show(
            "hello world!",
            AppTitle,
            MessageBoxButtons.OK,
            MessageBoxIcon.Information
        );
    }

    /// <summary>
    /// Handles the "Exit" menu item click.
    /// Initiates application shutdown.
    /// </summary>
    private void OnExitClick(object? sender, EventArgs e)
    {
        ExitThread();
    }

    /// <summary>
    /// Performs cleanup when the application exits.
    /// Ensures the tray icon is properly disposed and hidden.
    /// </summary>
    protected override void ExitThreadCore()
    {
        if (_trayIcon != null)
        {
            _trayIcon.Visible = false;
            _trayIcon.MouseUp -= OnTrayIconMouseUp;
            _trayIcon.Dispose();
        }

        _contextMenu?.Dispose();

        base.ExitThreadCore();
    }

    /// <summary>
    /// Disposes of resources used by the context.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _trayIcon?.Dispose();
            _contextMenu?.Dispose();
        }

        base.Dispose(disposing);
    }
}
