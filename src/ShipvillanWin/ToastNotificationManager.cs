using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;
using Microsoft.Toolkit.Uwp.Notifications;

namespace ShipvillanWin;

/// <summary>
/// Manages Windows toast notifications for the application.
/// Follows Windows toast UX guidance for clear, actionable, and non-intrusive notifications.
/// </summary>
[SupportedOSPlatform("windows")]
public class ToastNotificationManager : IDisposable
{
    private const string AppId = "BajaFulfillment.ShipvillanWin";
    private const string ScannerErrorTag = "scanner-error";
    private const string ScannerErrorGroup = "scanner-status";
    private const string CrosstagWarningTag = "crosstag-warning";
    private const string CrosstagWarningGroup = "crosstag-status";

    /// <summary>
    /// Initializes the toast notification manager and registers the app with Windows.
    /// Required for desktop apps to show proper toast notifications.
    /// </summary>
    public ToastNotificationManager()
    {
        try
        {
            RegisterAppForNotifications();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to register app for notifications: {ex.Message}");
        }
    }

    /// <summary>
    /// Registers the application for toast notifications.
    /// Creates a Start Menu shortcut with the AUMID for proper notification display.
    /// </summary>
    private void RegisterAppForNotifications()
    {
        var appPath = Environment.ProcessPath ?? System.Reflection.Assembly.GetExecutingAssembly().Location;
        var startMenuPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            @"Microsoft\Windows\Start Menu\Programs\ShipvillanWin.lnk"
        );

        // Register the app with Windows for toast notifications
        ToastNotificationManagerCompat.OnActivated += OnToastActivated;

        // Create shortcut if it doesn't exist
        if (!File.Exists(startMenuPath))
        {
            try
            {
                var shell = (IShellLink)new ShellLink();
                shell.SetPath(appPath);
                shell.SetDescription("ShipvillanWin - Barcode Scanner Manager");

                var propertyStore = (IPropertyStore)shell;
                var appIdKey = new PropertyKey(new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), 5);
                propertyStore.SetValue(appIdKey, AppId);
                propertyStore.Commit();

                var persistFile = (System.Runtime.InteropServices.ComTypes.IPersistFile)shell;
                persistFile.Save(startMenuPath, true);

                Debug.WriteLine($"Created Start Menu shortcut for toast notifications");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to create toast notification shortcut: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Handles toast notification activation (button clicks).
    /// </summary>
    private void OnToastActivated(ToastNotificationActivatedEventArgsCompat args)
    {
        var arguments = ToastArguments.Parse(args.Argument);

        if (arguments.TryGetValue("action", out var action))
        {
            Debug.WriteLine($"Toast action activated: {action}");
            // Handle actions if needed (e.g., "refresh" could trigger port refresh)
        }
    }

    /// <summary>
    /// Shows a persistent error notification when the barcode scanner is not detected.
    /// The notification remains in the Action Center until the issue is resolved.
    /// </summary>
    public void ShowScannerNotDetectedError()
    {
        try
        {
            // Clear any existing scanner error notifications first
            ToastNotificationManagerCompat.History.Remove(ScannerErrorTag, ScannerErrorGroup);

            // Build the toast notification following Windows UX guidance
            var toastContent = new ToastContentBuilder()
                .AddText("Barcode Scanner - Not detected", hintStyle: AdaptiveTextStyle.Title)
                .AddText("Connect a COM-configured scanner", hintStyle: AdaptiveTextStyle.Subtitle)
                .SetToastScenario(ToastScenario.Reminder) // Persistent notification that stays until dismissed
                .AddButton(new ToastButton()
                    .SetContent("Refresh Ports")
                    .AddArgument("action", "refresh")
                    .SetBackgroundActivation())
                .AddButton(new ToastButton()
                    .SetContent("Dismiss")
                    .AddArgument("action", "dismiss")
                    .SetBackgroundActivation());

            // Show the notification with tag and group for management
            toastContent.Show(toast =>
            {
                toast.Tag = ScannerErrorTag;
                toast.Group = ScannerErrorGroup;
                toast.ExpirationTime = DateTime.Now.AddDays(1); // Expires after 1 day
            });

            Debug.WriteLine("Toast notification shown: Scanner not detected");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to show toast notification: {ex.Message}");
            // Don't throw - notifications are non-critical to app functionality
        }
    }

    /// <summary>
    /// Clears the scanner error notification when the scanner is connected.
    /// </summary>
    public void ClearScannerNotDetectedError()
    {
        try
        {
            ToastNotificationManagerCompat.History.Remove(ScannerErrorTag, ScannerErrorGroup);
            Debug.WriteLine("Toast notification cleared: Scanner error resolved");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to clear toast notification: {ex.Message}");
        }
    }

    /// <summary>
    /// Shows a success notification when the scanner is connected.
    /// This is a brief, informative notification that auto-dismisses.
    /// </summary>
    public void ShowScannerConnected(string portName)
    {
        try
        {
            var toastContent = new ToastContentBuilder()
                .AddText("Barcode Scanner Connected", hintStyle: AdaptiveTextStyle.Title)
                .AddText($"Successfully connected to {portName}", hintStyle: AdaptiveTextStyle.Subtitle)
                .SetToastScenario(ToastScenario.Default); // Auto-dismisses after a few seconds

            toastContent.Show();

            Debug.WriteLine($"Toast notification shown: Scanner connected to {portName}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to show toast notification: {ex.Message}");
        }
    }

    /// <summary>
    /// Shows an informational notification (e.g., for updates).
    /// </summary>
    public void ShowInfo(string title, string message)
    {
        try
        {
            var toastContent = new ToastContentBuilder()
                .AddText(title, hintStyle: AdaptiveTextStyle.Title)
                .AddText(message, hintStyle: AdaptiveTextStyle.Subtitle)
                .SetToastScenario(ToastScenario.Default);

            toastContent.Show();

            Debug.WriteLine($"Toast notification shown: {title}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to show toast notification: {ex.Message}");
        }
    }

    /// <summary>
    /// Shows an error notification.
    /// </summary>
    public void ShowError(string title, string message)
    {
        try
        {
            var toastContent = new ToastContentBuilder()
                .AddText(title, hintStyle: AdaptiveTextStyle.Title)
                .AddText(message, hintStyle: AdaptiveTextStyle.Subtitle)
                .SetToastScenario(ToastScenario.Default);

            toastContent.Show();

            Debug.WriteLine($"Toast notification shown: {title}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to show toast notification: {ex.Message}");
        }
    }

    /// <summary>
    /// Clears all notifications from this application.
    /// Following Windows UX guidance to keep Notification Center tidy.
    /// </summary>
    public void ClearAllNotifications()
    {
        try
        {
            ToastNotificationManagerCompat.History.Clear();
            Debug.WriteLine("All toast notifications cleared");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to clear all toast notifications: {ex.Message}");
        }
    }

    /// <summary>
    /// Shows a persistent warning notification for Shipvillan crosstag orders (SKU 011299).
    /// The notification remains in Action Center until CT- tag is scanned.
    /// Note: Windows Toast notifications don't support custom background colors via standard APIs.
    /// </summary>
    public void ShowCrosstagWarning(string orderNumber)
    {
        try
        {
            // Clear any existing crosstag warning notifications first
            ToastNotificationManagerCompat.History.Remove(CrosstagWarningTag, CrosstagWarningGroup);

            // Build the persistent toast notification
            var toastContent = new ToastContentBuilder()
                .AddText("Shipvillan - Crosstag", hintStyle: AdaptiveTextStyle.Title)
                .AddText($"Order: {orderNumber}", hintStyle: AdaptiveTextStyle.Subtitle)
                .AddText("Please Scan Crosstag Label")
                .SetToastScenario(ToastScenario.Reminder) // Persistent notification that stays until dismissed
                .AddAttributionText("⚠️ Special Handling Required"); // Visual indicator

            // Show the notification with tag and group for management
            toastContent.Show(toast =>
            {
                toast.Tag = CrosstagWarningTag;
                toast.Group = CrosstagWarningGroup;
                toast.ExpirationTime = DateTime.Now.AddHours(1); // Expires after 1 hour
            });

            Debug.WriteLine($"Toast notification shown: Crosstag warning for order {orderNumber}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to show crosstag warning notification: {ex.Message}");
            // Don't throw - notifications are non-critical to app functionality
        }
    }

    /// <summary>
    /// Clears the crosstag warning notification when CT- tag is scanned.
    /// </summary>
    public void ClearCrosstagWarning()
    {
        try
        {
            ToastNotificationManagerCompat.History.Remove(CrosstagWarningTag, CrosstagWarningGroup);
            Debug.WriteLine("Toast notification cleared: Crosstag warning resolved");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to clear crosstag warning notification: {ex.Message}");
        }
    }

    public void Dispose()
    {
        // Cleanup if needed
        ToastNotificationManagerCompat.OnActivated -= OnToastActivated;
        ToastNotificationManagerCompat.Uninstall();
        GC.SuppressFinalize(this);
    }

    #region COM Interop for Shortcut Creation

    [System.Runtime.InteropServices.ComImport]
    [System.Runtime.InteropServices.Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLink { }

    [System.Runtime.InteropServices.ComImport]
    [System.Runtime.InteropServices.Guid("000214F9-0000-0000-C000-000000000046")]
    [System.Runtime.InteropServices.InterfaceType(System.Runtime.InteropServices.ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellLink
    {
        void GetPath([System.Runtime.InteropServices.Out, System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)] System.Text.StringBuilder pszFile, int cchMaxPath, IntPtr pfd, int fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([System.Runtime.InteropServices.Out, System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)] System.Text.StringBuilder pszName, int cchMaxName);
        void SetDescription([System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([System.Runtime.InteropServices.Out, System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)] System.Text.StringBuilder pszDir, int cchMaxPath);
        void SetWorkingDirectory([System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([System.Runtime.InteropServices.Out, System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)] System.Text.StringBuilder pszArgs, int cchMaxPath);
        void SetArguments([System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([System.Runtime.InteropServices.Out, System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)] System.Text.StringBuilder pszIconPath, int cchIconPath, out int piIcon);
        void SetIconLocation([System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);
        void Resolve(IntPtr hwnd, int fFlags);
        void SetPath([System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)] string pszFile);
    }

    [System.Runtime.InteropServices.ComImport]
    [System.Runtime.InteropServices.Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
    [System.Runtime.InteropServices.InterfaceType(System.Runtime.InteropServices.ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        void GetCount(out uint cProps);
        void GetAt(uint iProp, out PropertyKey pkey);
        void GetValue(ref PropertyKey key, out object pv);
        void SetValue(ref PropertyKey key, object pv);
        void Commit();
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct PropertyKey
    {
        public Guid fmtid;
        public uint pid;

        public PropertyKey(Guid fmtid, uint pid)
        {
            this.fmtid = fmtid;
            this.pid = pid;
        }
    }

    #endregion
}
