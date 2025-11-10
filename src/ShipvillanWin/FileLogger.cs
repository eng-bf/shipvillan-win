using System;
using System.Diagnostics;
using System.IO;

namespace ShipvillanWin;

/// <summary>
/// Simple file logger that writes all Debug.WriteLine calls to a log file.
/// Enable by calling FileLogger.Enable() in Program.cs
/// </summary>
public static class FileLogger
{
    private static TextWriterTraceListener? _fileListener;

    public static void Enable()
    {
        try
        {
            var logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ShipvillanWin",
                "Logs"
            );

            Directory.CreateDirectory(logPath);

            var logFile = Path.Combine(logPath, $"debug_{DateTime.Now:yyyyMMdd_HHmmss}.log");

            _fileListener = new TextWriterTraceListener(logFile)
            {
                TraceOutputOptions = TraceOptions.DateTime | TraceOptions.ThreadId
            };

            Trace.Listeners.Add(_fileListener);
            Trace.AutoFlush = true;

            Debug.WriteLine($"File logging enabled. Log file: {logFile}");
            Debug.WriteLine("=".PadRight(80, '='));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to enable file logging: {ex.Message}");
        }
    }

    public static void Disable()
    {
        if (_fileListener != null)
        {
            Trace.Listeners.Remove(_fileListener);
            _fileListener.Close();
            _fileListener = null;
        }
    }
}
