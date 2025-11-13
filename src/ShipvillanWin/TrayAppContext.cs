using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace ShipvillanWin;

/// <summary>
/// Application context for the system tray application.
/// Manages the tray icon, context menu, barcode processing, and application lifecycle.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class TrayAppContext : ApplicationContext
{
    private const string AppTitle = "ShipvillanWin";

    private readonly NotifyIcon _trayIcon;
    private readonly ContextMenuStrip _contextMenu;
    private readonly AppConfiguration _config;
    private readonly ComPortManager _comPortManager;
    private readonly BarcodeProcessor? _barcodeProcessor;
    private readonly OrderAssignmentProcessor? _orderAssignmentProcessor;
    private readonly OrderAssignmentService? _orderAssignmentService;
    private readonly UpdateService _updateService;

    // Menu items that need to be updated
    private ToolStripMenuItem? _statusItem;
    private ToolStripMenuItem? _modeMenuOrderAssignment;
    private ToolStripMenuItem? _modeMenuInterception;
    private ToolStripMenuItem? _modeMenuPassthrough;
    private ToolStripMenuItem? _comPortMenu;

    public TrayAppContext()
    {
        // Load configuration
        _config = AppConfiguration.Load();

        // Ensure we auto-start at login (HKCU)
        InitializeAutoStart();

        // Initialize COM port manager
        _comPortManager = new ComPortManager();
        _comPortManager.BarcodeReceived += OnBarcodeReceived;
        _comPortManager.ConnectionStatusChanged += OnConnectionStatusChanged;
        _comPortManager.ErrorOccurred += OnComPortError;

        // Initialize barcode processor based on operation mode
        if (_config.Mode == OperationMode.Interception)
        {
            var interceptionService = new InterceptionService(_config.CrossTagApiBaseUrl, _config.CrossTagApiTimeoutMs);
            _barcodeProcessor = new BarcodeProcessor(_config, interceptionService);
            _barcodeProcessor.ProcessingStatusChanged += OnProcessingStatusChanged;
            _barcodeProcessor.BarcodeRejected += OnBarcodeRejected;
        }
        else if (_config.Mode == OperationMode.OrderAssignment)
        {
            _orderAssignmentService = new OrderAssignmentService();
            _orderAssignmentProcessor = new OrderAssignmentProcessor(_config, _orderAssignmentService);
            _orderAssignmentProcessor.AssignmentCompleted += OnAssignmentCompleted;
        }

        // Initialize update service
        _updateService = new UpdateService("https://github.com/eng-bf/shipvillan-win");
        _updateService.UpdateStatusChanged += OnUpdateStatusChanged;
        _updateService.UpdateError += OnUpdateError;

        // Build context menu
        _contextMenu = CreateContextMenu();

        // Initialize system tray icon
        _trayIcon = CreateTrayIcon(_contextMenu);
        _trayIcon.MouseUp += OnTrayIconMouseUp;

        // Auto-connect to configured COM port if set
        if (!string.IsNullOrEmpty(_config.ComPort))
        {
            TryConnectToComPort(_config.ComPort);
        }

        // Initialize update service (async, fire and forget)
        _ = _updateService.InitializeAsync();
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
        var menu = new ContextMenuStrip();

        // Mode selection submenu
        var modeMenu = new ToolStripMenuItem("Mode");
        _modeMenuOrderAssignment = new ToolStripMenuItem("Order Assignment (MX)", null, OnModeSelected);
        _modeMenuOrderAssignment.Tag = OperationMode.OrderAssignment;
        _modeMenuOrderAssignment.Checked = _config.Mode == OperationMode.OrderAssignment;

        _modeMenuInterception = new ToolStripMenuItem("Interception (US)", null, OnModeSelected);
        _modeMenuInterception.Tag = OperationMode.Interception;
        _modeMenuInterception.Checked = _config.Mode == OperationMode.Interception;

        _modeMenuPassthrough = new ToolStripMenuItem("Passthrough (Disabled)", null, OnModeSelected);
        _modeMenuPassthrough.Tag = OperationMode.Passthrough;
        _modeMenuPassthrough.Checked = _config.Mode == OperationMode.Passthrough;

        modeMenu.DropDownItems.Add(_modeMenuOrderAssignment);
        modeMenu.DropDownItems.Add(_modeMenuInterception);
        modeMenu.DropDownItems.Add(_modeMenuPassthrough);

        // COM port submenu
        _comPortMenu = new ToolStripMenuItem("COM Port");
        _comPortMenu.DropDownOpening += OnComPortMenuOpening;
        RefreshComPortMenu();

        // Status item
        _statusItem = new ToolStripMenuItem(GetStatusText())
        {
            Enabled = false // Non-clickable, just for display
        };

        // Version item
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown";
        var versionItem = new ToolStripMenuItem($"Version: {version}")
        {
            Enabled = false // Non-clickable, just for display
        };

        // Build menu structure
        menu.Items.Add(modeMenu);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_comPortMenu);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_statusItem);
        menu.Items.Add(versionItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Check for Updates", null, OnCheckForUpdatesClick));
        menu.Items.Add(new ToolStripMenuItem("Rollback to Previous Version", null, OnRollbackClick));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Exit", null, OnExitClick));

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
            Icon = IconHelper.GetTrayIcon(),
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
    /// Gets the status text for display in the menu.
    /// </summary>
    private string GetStatusText()
    {
        if (!_comPortManager.IsConnected)
        {
            return "Status: Disconnected";
        }

        var status = $"Status: Connected to {_comPortManager.CurrentPort} ✓";

        if (_barcodeProcessor?.IsProcessing == true)
        {
            status += " (Processing...)";
        }

        return status;
    }

    /// <summary>
    /// Updates the status menu item.
    /// </summary>
    private void UpdateStatus()
    {
        if (_statusItem != null)
        {
            _statusItem.Text = GetStatusText();
        }
    }

    /// <summary>
    /// Refreshes the COM port menu with available ports.
    /// </summary>
    private void RefreshComPortMenu()
    {
        if (_comPortMenu == null)
            return;

        _comPortMenu.DropDownItems.Clear();

        var ports = ComPortManager.GetAvailablePorts();

        if (ports.Count == 0)
        {
            _comPortMenu.DropDownItems.Add(new ToolStripMenuItem("No ports available") { Enabled = false });
        }
        else
        {
            foreach (var port in ports)
            {
                var item = new ToolStripMenuItem(port.DisplayName, null, OnComPortSelected);
                item.Tag = port;
                item.Checked = port.PortName == _config.ComPort;
                _comPortMenu.DropDownItems.Add(item);
            }
        }

        _comPortMenu.DropDownItems.Add(new ToolStripSeparator());
        _comPortMenu.DropDownItems.Add(new ToolStripMenuItem("Refresh Ports", null, OnRefreshPorts));
    }

    /// <summary>
    /// Tries to connect to a COM port, showing errors if connection fails.
    /// </summary>
    private void TryConnectToComPort(string portName)
    {
        try
        {
            _comPortManager.Connect(portName, _config.BaudRate, _config.DataBits);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to connect to {portName}: {ex.Message}");
            MessageBox.Show(
                $"Failed to connect to {portName}:\n\n{ex.Message}",
                AppTitle,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
        }
    }

    #region Event Handlers

    /// <summary>
    /// Handles mode selection changes.
    /// </summary>
    private void OnModeSelected(object? sender, EventArgs e)
    {
        if (sender is not ToolStripMenuItem item || item.Tag is not OperationMode newMode)
            return;

        if (_config.Mode == newMode)
            return; // Already in this mode

        _config.Mode = newMode;
        _config.Save();

        // Update checkmarks
        if (_modeMenuOrderAssignment != null)
            _modeMenuOrderAssignment.Checked = newMode == OperationMode.OrderAssignment;
        if (_modeMenuInterception != null)
            _modeMenuInterception.Checked = newMode == OperationMode.Interception;
        if (_modeMenuPassthrough != null)
            _modeMenuPassthrough.Checked = newMode == OperationMode.Passthrough;

        MessageBox.Show(
            $"Mode changed to: {newMode}\n\nThe application will restart to apply changes.",
            AppTitle,
            MessageBoxButtons.OK,
            MessageBoxIcon.Information
        );

        // Restart application
        Application.Restart();
        Environment.Exit(0);
    }

    /// <summary>
    /// Handles COM port menu opening (refresh ports).
    /// </summary>
    private void OnComPortMenuOpening(object? sender, EventArgs e)
    {
        RefreshComPortMenu();
    }

    /// <summary>
    /// Handles COM port selection.
    /// </summary>
    private void OnComPortSelected(object? sender, EventArgs e)
    {
        if (sender is not ToolStripMenuItem item || item.Tag is not ComPortInfo portInfo)
            return;

        // Disconnect from current port
        _comPortManager.Disconnect();

        // Connect to new port
        _config.ComPort = portInfo.PortName;
        _config.Save();

        TryConnectToComPort(portInfo.PortName);
        RefreshComPortMenu();
    }

    /// <summary>
    /// Handles refresh ports menu click.
    /// </summary>
    private void OnRefreshPorts(object? sender, EventArgs e)
    {
        RefreshComPortMenu();
    }

    /// <summary>
    /// Handles barcode received from COM port.
    /// </summary>
    private async void OnBarcodeReceived(object? sender, string barcode)
    {
        Debug.WriteLine($"TrayAppContext: Barcode received: {barcode}");

        // Process based on operation mode
        if (_config.Mode == OperationMode.Interception && _barcodeProcessor != null)
        {
            await _barcodeProcessor.ProcessBarcodeAsync(barcode);
        }
        else if (_config.Mode == OperationMode.OrderAssignment && _orderAssignmentProcessor != null)
        {
            await _orderAssignmentProcessor.ProcessBarcodeAsync(barcode);
        }
        else if (_config.Mode == OperationMode.Passthrough)
        {
            // Passthrough mode: Forward immediately without any processing
            Debug.WriteLine($"TrayAppContext: Passthrough mode - forwarding barcode '{barcode}' directly");
            try
            {
                await KeyboardSimulator.SendKeysAsync(
                    barcode,
                    _config.KeyboardDelayMs,
                    _config.AppendEnterKey
                );
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TrayAppContext: Error forwarding barcode in Passthrough mode: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Handles COM port connection status changes.
    /// </summary>
    private void OnConnectionStatusChanged(object? sender, bool isConnected)
    {
        Debug.WriteLine($"COM port connection status: {(isConnected ? "Connected" : "Disconnected")}");

        // Update status on UI thread
        if (_contextMenu.InvokeRequired)
        {
            _contextMenu.Invoke(UpdateStatus);
        }
        else
        {
            UpdateStatus();
        }
    }

    /// <summary>
    /// Handles COM port errors.
    /// </summary>
    private void OnComPortError(object? sender, Exception ex)
    {
        Debug.WriteLine($"COM port error: {ex.Message}");
        // Errors are logged but don't show user dialogs (would be disruptive)
    }

    /// <summary>
    /// Handles barcode processing status changes.
    /// </summary>
    private void OnProcessingStatusChanged(object? sender, bool isProcessing)
    {
        // Update status on UI thread
        if (_contextMenu.InvokeRequired)
        {
            _contextMenu.Invoke(UpdateStatus);
        }
        else
        {
            UpdateStatus();
        }
    }

    /// <summary>
    /// Handles barcode rejection (scanned during processing).
    /// </summary>
    private void OnBarcodeRejected(object? sender, EventArgs e)
    {
        Debug.WriteLine("Barcode scan rejected (processing in progress)");
        // Could show a visual/audio indication here
    }

    /// <summary>
    /// Handles order assignment completion (success or failure).
    /// </summary>
    private void OnAssignmentCompleted(object? sender, OrderAssignmentResult result)
    {
        if (result.Success)
        {
            Debug.WriteLine($"Order assignment succeeded: {result.OrderBarcode} → {result.ToteBarcode}");
        }
        else
        {
            Debug.WriteLine($"Order assignment failed: {result.OrderBarcode} → {result.ToteBarcode}. Error: {result.ErrorMessage}");
            // TODO: Implement remote logging for failures
            // This will eventually send failure logs to a remote system for monitoring
        }
    }

    /// <summary>
    /// Handles the "Check for Updates" menu item click.
    /// </summary>
    private async void OnCheckForUpdatesClick(object? sender, EventArgs e)
    {
        await _updateService.CheckForUpdatesManuallyAsync();
    }

    /// <summary>
    /// Handles the "Rollback to Previous Version" menu item click.
    /// </summary>
    private async void OnRollbackClick(object? sender, EventArgs e)
    {
        var result = MessageBox.Show(
            "Are you sure you want to rollback to the previous version?\n\nThis will restart the application.",
            AppTitle,
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning
        );

        if (result == DialogResult.Yes)
        {
            await _updateService.RollbackToPreviousVersionAsync();
        }
    }

    /// <summary>
    /// Handles update status changes from the UpdateService.
    /// </summary>
    private void OnUpdateStatusChanged(object? sender, string status)
    {
        Debug.WriteLine($"Update status: {status}");

        // Show notification to user
        _trayIcon.ShowBalloonTip(
            3000,
            "ShipvillanWin Updates",
            status,
            ToolTipIcon.Info
        );
    }

    /// <summary>
    /// Handles update errors from the UpdateService.
    /// </summary>
    private void OnUpdateError(object? sender, Exception ex)
    {
        Debug.WriteLine($"Update error: {ex.Message}");

        // Show error notification
        _trayIcon.ShowBalloonTip(
            5000,
            "ShipvillanWin Update Error",
            $"Update failed: {ex.Message}",
            ToolTipIcon.Error
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

    #endregion

    /// <summary>
    /// Performs cleanup when the application exits.
    /// Ensures the tray icon is properly disposed and hidden.
    /// </summary>
    protected override void ExitThreadCore()
    {
        // Disconnect from COM port
        _comPortManager?.Disconnect();

        // Hide and dispose tray icon
        if (_trayIcon != null)
        {
            _trayIcon.Visible = false;
            _trayIcon.MouseUp -= OnTrayIconMouseUp;
            _trayIcon.Dispose();
        }

        _contextMenu?.Dispose();
        _comPortManager?.Dispose();
        _updateService?.Dispose();

        base.ExitThreadCore();
    }

    /// <summary>
    /// Disposes of resources used by the context.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _comPortManager?.Dispose();
            _orderAssignmentProcessor?.Dispose();
            _orderAssignmentService?.Dispose();
            _trayIcon?.Dispose();
            _contextMenu?.Dispose();
            _updateService?.Dispose();
        }

        base.Dispose(disposing);
    }
}
