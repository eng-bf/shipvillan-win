using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Runtime.Versioning;
using System.Text;

namespace ShipvillanWin;

/// <summary>
/// Manages COM port discovery, connection, and data reception for barcode scanners.
/// </summary>
[SupportedOSPlatform("windows")]
public class ComPortManager : IDisposable
{
    private SerialPort? _serialPort;
    private readonly StringBuilder _buffer = new();
    private readonly object _lock = new();

    /// <summary>
    /// Fired when a complete barcode is received (terminated by CR/LF or timeout).
    /// </summary>
    public event EventHandler<string>? BarcodeReceived;

    /// <summary>
    /// Fired when connection status changes.
    /// </summary>
    public event EventHandler<bool>? ConnectionStatusChanged;

    /// <summary>
    /// Fired when an error occurs.
    /// </summary>
    public event EventHandler<Exception>? ErrorOccurred;

    /// <summary>
    /// Gets whether the COM port is currently connected.
    /// </summary>
    public bool IsConnected => _serialPort?.IsOpen ?? false;

    /// <summary>
    /// Gets the currently connected port name.
    /// </summary>
    public string? CurrentPort => _serialPort?.PortName;

    /// <summary>
    /// Lists all available COM ports with device information.
    /// </summary>
    public static List<ComPortInfo> GetAvailablePorts()
    {
        var ports = SerialPort.GetPortNames().OrderBy(p => p).ToList();
        var portInfoList = new List<ComPortInfo>();

        try
        {
            // Use WMI to get detailed device information
            using var searcher = new ManagementObjectSearcher(
                "SELECT * FROM Win32_PnPEntity WHERE Name LIKE '%(COM%'"
            );

            var wmiPorts = new Dictionary<string, ComPortInfo>();

            foreach (ManagementObject obj in searcher.Get())
            {
                var name = obj["Name"]?.ToString() ?? string.Empty;
                var description = obj["Description"]?.ToString() ?? string.Empty;
                var manufacturer = obj["Manufacturer"]?.ToString();

                // Extract COM port number from name (e.g., "USB Serial Port (COM3)" -> "COM3")
                var portMatch = System.Text.RegularExpressions.Regex.Match(name, @"\(COM\d+\)");
                if (portMatch.Success)
                {
                    var portName = portMatch.Value.Trim('(', ')');
                    wmiPorts[portName] = new ComPortInfo
                    {
                        PortName = portName,
                        FriendlyName = name,
                        Description = description,
                        Manufacturer = manufacturer
                    };
                }
            }

            // Combine with SerialPort.GetPortNames() to ensure we don't miss any
            foreach (var portName in ports)
            {
                if (wmiPorts.TryGetValue(portName, out var portInfo))
                {
                    portInfoList.Add(portInfo);
                }
                else
                {
                    // Port exists but no WMI info available
                    portInfoList.Add(new ComPortInfo
                    {
                        PortName = portName,
                        FriendlyName = $"Serial Port ({portName})",
                        Description = "Standard Serial Port"
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error querying COM port details via WMI: {ex.Message}");

            // Fallback to basic port names
            portInfoList.AddRange(ports.Select(p => new ComPortInfo
            {
                PortName = p,
                FriendlyName = p,
                Description = "Serial Port"
            }));
        }

        return portInfoList;
    }

    /// <summary>
    /// Connects to the specified COM port.
    /// </summary>
    public void Connect(string portName, int baudRate = 9600, int dataBits = 8)
    {
        try
        {
            Disconnect();

            _serialPort = new SerialPort(portName, baudRate, Parity.None, dataBits, StopBits.One)
            {
                Handshake = Handshake.None,
                ReadTimeout = 500,
                WriteTimeout = 500,
                Encoding = Encoding.ASCII
            };

            _serialPort.DataReceived += OnDataReceived;
            _serialPort.ErrorReceived += OnErrorReceived;

            _serialPort.Open();

            Debug.WriteLine($"Connected to {portName} at {baudRate} baud");
            ConnectionStatusChanged?.Invoke(this, true);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to connect to {portName}: {ex.Message}");
            ErrorOccurred?.Invoke(this, ex);
            throw;
        }
    }

    /// <summary>
    /// Disconnects from the current COM port.
    /// </summary>
    public void Disconnect()
    {
        if (_serialPort != null)
        {
            try
            {
                if (_serialPort.IsOpen)
                {
                    _serialPort.Close();
                }

                _serialPort.DataReceived -= OnDataReceived;
                _serialPort.ErrorReceived -= OnErrorReceived;
                _serialPort.Dispose();

                Debug.WriteLine($"Disconnected from {_serialPort.PortName}");
                ConnectionStatusChanged?.Invoke(this, false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during disconnect: {ex.Message}");
            }
            finally
            {
                _serialPort = null;
            }
        }

        lock (_lock)
        {
            _buffer.Clear();
        }
    }

    /// <summary>
    /// Handles incoming data from the serial port.
    /// </summary>
    private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        if (_serialPort == null || !_serialPort.IsOpen)
            return;

        try
        {
            var data = _serialPort.ReadExisting();

            lock (_lock)
            {
                _buffer.Append(data);

                // Check for line terminator (CR, LF, or CRLF)
                var bufferContent = _buffer.ToString();
                var lines = bufferContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                if (bufferContent.Contains('\r') || bufferContent.Contains('\n'))
                {
                    // We have at least one complete line
                    foreach (var line in lines)
                    {
                        var trimmed = line.Trim();
                        if (!string.IsNullOrEmpty(trimmed))
                        {
                            Debug.WriteLine($"Barcode received: {trimmed}");
                            BarcodeReceived?.Invoke(this, trimmed);
                        }
                    }

                    // Clear the buffer (or keep any incomplete data)
                    _buffer.Clear();

                    // If buffer ended without terminator, there might be incomplete data
                    if (!bufferContent.EndsWith('\r') && !bufferContent.EndsWith('\n') && lines.Length > 0)
                    {
                        _buffer.Append(lines[^1]);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error reading from COM port: {ex.Message}");
            ErrorOccurred?.Invoke(this, ex);
        }
    }

    /// <summary>
    /// Handles serial port errors.
    /// </summary>
    private void OnErrorReceived(object sender, SerialErrorReceivedEventArgs e)
    {
        Debug.WriteLine($"Serial port error: {e.EventType}");
        var ex = new InvalidOperationException($"Serial port error: {e.EventType}");
        ErrorOccurred?.Invoke(this, ex);
    }

    /// <summary>
    /// Disposes of the COM port resources.
    /// </summary>
    public void Dispose()
    {
        Disconnect();
        GC.SuppressFinalize(this);
    }
}
