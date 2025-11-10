namespace ShipvillanWin;

/// <summary>
/// Defines the operational mode of the application.
/// </summary>
public enum OperationMode
{
    /// <summary>
    /// Order Assignment mode - used in Mexican warehouse (MX).
    /// </summary>
    OrderAssignment,

    /// <summary>
    /// Interception mode - used in US warehouse.
    /// Intercepts barcode scans and performs async processing for CT- prefixed codes.
    /// </summary>
    Interception,

    /// <summary>
    /// Passthrough mode - disables all processing.
    /// All barcodes are immediately forwarded without any interception or processing.
    /// </summary>
    Passthrough
}
