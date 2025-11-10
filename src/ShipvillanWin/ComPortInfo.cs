namespace ShipvillanWin;

/// <summary>
/// Information about a COM port device.
/// </summary>
public class ComPortInfo
{
    /// <summary>
    /// Port name (e.g., "COM3").
    /// </summary>
    public string PortName { get; set; } = string.Empty;

    /// <summary>
    /// Friendly device name from Windows (e.g., "USB Serial Port (COM3)").
    /// </summary>
    public string FriendlyName { get; set; } = string.Empty;

    /// <summary>
    /// Device description.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Manufacturer name if available.
    /// </summary>
    public string? Manufacturer { get; set; }

    /// <summary>
    /// Returns a display-friendly string for UI.
    /// </summary>
    public string DisplayName => string.IsNullOrWhiteSpace(FriendlyName)
        ? PortName
        : $"{PortName} - {FriendlyName}";

    public override string ToString() => DisplayName;
}
