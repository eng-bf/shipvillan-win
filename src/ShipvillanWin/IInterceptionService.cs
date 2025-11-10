using System.Threading;
using System.Threading.Tasks;

namespace ShipvillanWin;

/// <summary>
/// Service for processing intercepted barcodes that match the configured prefix.
/// Implementations can query databases, call APIs, or perform other async operations.
/// </summary>
public interface IInterceptionService
{
    /// <summary>
    /// Processes a barcode asynchronously and returns the data to be sent to the keyboard.
    /// </summary>
    /// <param name="barcode">The scanned barcode (including prefix).</param>
    /// <param name="cancellationToken">Cancellation token for timeout/cancellation.</param>
    /// <returns>
    /// The data to send via keyboard simulation.
    /// Return null or empty string if no keyboard input should be sent (e.g., on error).
    /// </returns>
    Task<string?> ProcessBarcodeAsync(string barcode, CancellationToken cancellationToken);
}
