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
    private ManagementEventWatcher? _deviceArrivalWatcher;
    private ManagementEventWatcher? _deviceRemovalWatcher;
    private string? _lastConfiguredPort;
    private int _lastBaudRate = 9600;
    private int _lastDataBits = 8;

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
    /// Checks if the current connection is still healthy.
    /// Returns true if connected and port is accessible, false otherwise.
    /// </summary>
    public bool CheckConnectionHealth()
    {
        if (_serialPort == null || !_serialPort.IsOpen)
            return false;

        try
        {
            // Try multiple checks to verify the connection
            var portName = _serialPort.PortName;

            // Check 1: Is the port still in the available ports list?
            var availablePorts = SerialPort.GetPortNames();
            if (!availablePorts.Contains(portName))
            {
                Debug.WriteLine($"Health check failed: {portName} not in available ports");
                Disconnect();
                return false;
            }

            // Check 2: Can we still access the port properties?
            try
            {
                var _ = _serialPort.BytesToRead;
                var __ = _serialPort.IsOpen;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Health check failed: Cannot access {portName} properties - {ex.Message}");
                Disconnect();
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Health check exception: {ex.Message}");
            Disconnect();
            return false;
        }
    }

    /// <summary>
    /// Initializes the COM port manager and starts monitoring for device changes.
    /// </summary>
    public ComPortManager()
    {
        StartDeviceMonitoring();
    }

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

            // Remember the port configuration for auto-reconnect
            _lastConfiguredPort = portName;
            _lastBaudRate = baudRate;
            _lastDataBits = dataBits;

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

            // If we get an IOException or UnauthorizedAccessException, the device might be gone
            if (ex is System.IO.IOException || ex is UnauthorizedAccessException)
            {
                Debug.WriteLine("Device appears to be disconnected, closing port");
                Disconnect();
                return;
            }

            ErrorOccurred?.Invoke(this, ex);
        }
    }

    /// <summary>
    /// Handles serial port errors.
    /// </summary>
    private void OnErrorReceived(object sender, SerialErrorReceivedEventArgs e)
    {
        Debug.WriteLine($"Serial port error: {e.EventType}");

        // Check if the port is still accessible - device might have been removed
        if (_serialPort != null && !string.IsNullOrEmpty(_serialPort.PortName))
        {
            try
            {
                // Try to check if the port is still valid
                if (_serialPort.IsOpen)
                {
                    var _ = _serialPort.BytesToRead;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Port no longer accessible after error: {ex.Message}");
                Disconnect();
                return;
            }
        }

        var exception = new InvalidOperationException($"Serial port error: {e.EventType}");
        ErrorOccurred?.Invoke(this, exception);
    }

    /// <summary>
    /// Starts monitoring for USB device arrival and removal using WMI.
    /// </summary>
    private void StartDeviceMonitoring()
    {
        try
        {
            // Monitor for device arrival (USB devices being plugged in)
            var arrivalQuery = new WqlEventQuery("SELECT * FROM Win32_DeviceChangeEvent WHERE EventType = 2");
            _deviceArrivalWatcher = new ManagementEventWatcher(arrivalQuery);
            _deviceArrivalWatcher.EventArrived += OnDeviceArrived;
            _deviceArrivalWatcher.Start();

            // Monitor for device removal (USB devices being unplugged)
            var removalQuery = new WqlEventQuery("SELECT * FROM Win32_DeviceChangeEvent WHERE EventType = 3");
            _deviceRemovalWatcher = new ManagementEventWatcher(removalQuery);
            _deviceRemovalWatcher.EventArrived += OnDeviceRemoved;
            _deviceRemovalWatcher.Start();

            Debug.WriteLine("COM port device monitoring started");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to start device monitoring: {ex.Message}");
        }
    }

    /// <summary>
    /// Stops monitoring for device changes.
    /// </summary>
    private void StopDeviceMonitoring()
    {
        try
        {
            if (_deviceArrivalWatcher != null)
            {
                _deviceArrivalWatcher.Stop();
                _deviceArrivalWatcher.EventArrived -= OnDeviceArrived;
                _deviceArrivalWatcher.Dispose();
                _deviceArrivalWatcher = null;
            }

            if (_deviceRemovalWatcher != null)
            {
                _deviceRemovalWatcher.Stop();
                _deviceRemovalWatcher.EventArrived -= OnDeviceRemoved;
                _deviceRemovalWatcher.Dispose();
                _deviceRemovalWatcher = null;
            }

            Debug.WriteLine("COM port device monitoring stopped");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error stopping device monitoring: {ex.Message}");
        }
    }

    /// <summary>
    /// Handles device arrival events (when a USB device is plugged in).
    /// Attempts to auto-reconnect if the previously configured port is now available.
    /// </summary>
    private void OnDeviceArrived(object sender, EventArrivedEventArgs e)
    {
        Debug.WriteLine("Device arrival detected");

        // If we had a configured port and we're not currently connected, try to reconnect
        if (!string.IsNullOrEmpty(_lastConfiguredPort) && !IsConnected)
        {
            // Small delay to allow the device to be fully enumerated
            System.Threading.Thread.Sleep(500);

            var availablePorts = SerialPort.GetPortNames();
            if (availablePorts.Contains(_lastConfiguredPort))
            {
                Debug.WriteLine($"Previously configured port {_lastConfiguredPort} is now available, attempting auto-reconnect");
                try
                {
                    Connect(_lastConfiguredPort, _lastBaudRate, _lastDataBits);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Auto-reconnect failed: {ex.Message}");
                }
            }
        }
    }

    /// <summary>
    /// Handles device removal events (when a USB device is unplugged).
    /// Checks if the currently connected COM port was removed.
    /// </summary>
    private void OnDeviceRemoved(object sender, EventArrivedEventArgs e)
    {
        Debug.WriteLine("Device removal detected");

        // Check if we have an active connection
        if (_serialPort == null || string.IsNullOrEmpty(_serialPort.PortName))
            return;

        var currentPortName = _serialPort.PortName;

        try
        {
            // First check: Is the port still in the available ports list?
            var availablePorts = SerialPort.GetPortNames();
            if (!availablePorts.Contains(currentPortName))
            {
                Debug.WriteLine($"Port {currentPortName} was removed");
                Disconnect();
                return;
            }

            // Second check: Try to verify the port is still accessible
            // Sometimes Windows keeps the port in the list even after physical removal
            if (_serialPort.IsOpen)
            {
                try
                {
                    // Try to access the port properties - this will fail if device is gone
                    var _ = _serialPort.BytesToRead;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Port {currentPortName} no longer accessible: {ex.Message}");
                    Disconnect();
                    return;
                }
            }
            else
            {
                // Port is unexpectedly closed - this happens when device is physically removed
                // Call Disconnect() to clean up and fire the ConnectionStatusChanged event
                Debug.WriteLine($"Port {currentPortName} unexpectedly closed");
                Disconnect();
                return;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error checking port status during device removal: {ex.Message}");
            Disconnect();
        }
    }

    /// <summary>
    /// Disposes of the COM port resources.
    /// </summary>
    public void Dispose()
    {
        StopDeviceMonitoring();
        Disconnect();
        GC.SuppressFinalize(this);
    }
}
