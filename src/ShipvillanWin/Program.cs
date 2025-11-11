using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Versioning;
using System.Threading;
using System.Windows.Forms;

// Mark the assembly as Squirrel-aware for auto-updates
[assembly: AssemblyMetadata("SquirrelAwareVersion", "1")]

namespace ShipvillanWin;

internal static class Program
{
    private const string MutexName = @"Local\ShipvillanWin_{7D692F1E-6C5F-4E27-8A0C-5F61F9D7F6EC}";

    [STAThread]
    [SupportedOSPlatform("windows")]
    private static void Main()
    {
        // Single-instance guard using a named mutex
        using var singleInstanceMutex = new Mutex(true, MutexName, out var createdNew);

        if (!createdNew)
        {
            // Another instance is already running; exit quietly
            return;
        }

        try
        {
            // In Debug builds, enable console trace output
#if DEBUG
            Trace.Listeners.Add(new ConsoleTraceListener());
            Debug.WriteLine("Debug output enabled - use DebugView to see logs");
#endif

            // Enable high DPI support for modern displays
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            ApplicationConfiguration.Initialize();
            Application.Run(new TrayAppContext());
        }
        finally
        {
            // Ensure mutex is released even if an exception occurs
            try
            {
                singleInstanceMutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // Mutex was already released or abandoned - this is acceptable during shutdown
            }
        }
    }
}
