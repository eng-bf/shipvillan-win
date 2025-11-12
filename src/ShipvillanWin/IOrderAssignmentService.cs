using System.Threading;
using System.Threading.Tasks;

namespace ShipvillanWin;

/// <summary>
/// Service interface for order assignment operations.
/// Performs async operations to link cross-tag codes to orders in totes.
/// </summary>
public interface IOrderAssignmentService
{
    /// <summary>
    /// Links a cross-tag code to an order in a tote via the link_order endpoint.
    /// </summary>
    /// <param name="toteBarcode">The tote barcode</param>
    /// <param name="toteData">The tote data including order information</param>
    /// <param name="crossTagCode">The CT- cross-tag code</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if link succeeded, false otherwise</returns>
    Task<bool> LinkOrderAsync(string toteBarcode, ToteData toteData, string crossTagCode, CancellationToken cancellationToken);
}
