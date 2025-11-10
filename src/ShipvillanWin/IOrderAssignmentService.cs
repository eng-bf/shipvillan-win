using System.Threading;
using System.Threading.Tasks;

namespace ShipvillanWin;

/// <summary>
/// Service interface for order assignment operations.
/// Performs async mutations when a tote and order barcode are scanned.
/// </summary>
public interface IOrderAssignmentService
{
    /// <summary>
    /// Assigns an order to a tote via async mutation.
    /// </summary>
    /// <param name="toteBarcode">The tote barcode (includes 'tote:' prefix)</param>
    /// <param name="orderBarcode">The order barcode (any alphanumeric)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if assignment succeeded, false otherwise</returns>
    Task<bool> AssignOrderToToteAsync(string toteBarcode, string orderBarcode, CancellationToken cancellationToken);
}
