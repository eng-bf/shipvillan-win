using System;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;

namespace ShipvillanWin;

/// <summary>
/// Processes barcodes for Order Assignment mode.
/// Always forwards all barcodes immediately while performing async operations in the background.
/// Implements a state machine to track tote and order barcode pairs.
/// </summary>
[SupportedOSPlatform("windows")]
public class OrderAssignmentProcessor
{
    private readonly AppConfiguration _config;
    private readonly IOrderAssignmentService _orderAssignmentService;
    private readonly SemaphoreSlim _stateLock = new(1, 1);

    private string? _pendingToteBarcode = null;
    private int _assignmentCount = 0;
    private int _failureCount = 0;

    /// <summary>
    /// Fired when an assignment operation completes (success or failure).
    /// </summary>
    public event EventHandler<OrderAssignmentResult>? AssignmentCompleted;

    /// <summary>
    /// Gets the total count of assignment operations attempted.
    /// </summary>
    public int AssignmentCount => _assignmentCount;

    /// <summary>
    /// Gets the count of failed assignment operations.
    /// </summary>
    public int FailureCount => _failureCount;

    public OrderAssignmentProcessor(AppConfiguration config, IOrderAssignmentService orderAssignmentService)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _orderAssignmentService = orderAssignmentService ?? throw new ArgumentNullException(nameof(orderAssignmentService));
    }

    /// <summary>
    /// Processes a scanned barcode.
    /// ALWAYS forwards the barcode immediately, then performs async operations in the background.
    /// </summary>
    public async Task ProcessBarcodeAsync(string barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode))
            return;

        try
        {
            // CRITICAL: Forward barcode immediately, without waiting
            Debug.WriteLine($"OrderAssignmentProcessor: Forwarding barcode '{barcode}' immediately");

            // Fire and forget - don't await, we want to return immediately
            _ = ForwardBarcodeAsync(barcode);

            // Now process the barcode for order assignment logic (non-blocking)
            await ProcessOrderAssignmentLogicAsync(barcode);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"OrderAssignmentProcessor: Error in ProcessBarcodeAsync: {ex.Message}");
            // Don't throw - we want to continue processing
        }
    }

    /// <summary>
    /// Processes the order assignment state machine logic.
    /// </summary>
    private async Task ProcessOrderAssignmentLogicAsync(string barcode)
    {
        await _stateLock.WaitAsync();

        try
        {
            // Check if this is a tote barcode
            if (barcode.StartsWith("tote:", StringComparison.OrdinalIgnoreCase))
            {
                Debug.WriteLine($"OrderAssignmentProcessor: Detected tote barcode '{barcode}', waiting for order barcode");
                _pendingToteBarcode = barcode;
                return;
            }

            // Check if we have a pending tote barcode
            if (!string.IsNullOrEmpty(_pendingToteBarcode))
            {
                var toteBarcode = _pendingToteBarcode;
                var orderBarcode = barcode;

                // Clear the pending tote barcode before starting async operation
                _pendingToteBarcode = null;

                Debug.WriteLine($"OrderAssignmentProcessor: Received order barcode '{orderBarcode}' after tote '{toteBarcode}', triggering assignment");

                // Fire and forget - perform assignment in background without blocking
                _ = PerformAssignmentAsync(toteBarcode, orderBarcode);
            }
            else
            {
                // Not a tote barcode and no pending tote - just a regular barcode
                Debug.WriteLine($"OrderAssignmentProcessor: Regular barcode '{barcode}' (no pending tote)");
            }
        }
        finally
        {
            _stateLock.Release();
        }
    }

    /// <summary>
    /// Performs the async assignment operation in the background.
    /// This runs independently and does not block barcode forwarding.
    /// </summary>
    private async Task PerformAssignmentAsync(string toteBarcode, string orderBarcode)
    {
        Interlocked.Increment(ref _assignmentCount);

        try
        {
            Debug.WriteLine($"OrderAssignmentProcessor: Starting background assignment of '{orderBarcode}' to '{toteBarcode}'");

            using var cts = new CancellationTokenSource(_config.InterceptionTimeoutMs);

            var success = await _orderAssignmentService.AssignOrderToToteAsync(toteBarcode, orderBarcode, cts.Token);

            if (success)
            {
                Debug.WriteLine($"OrderAssignmentProcessor: Successfully assigned '{orderBarcode}' to '{toteBarcode}'");
                AssignmentCompleted?.Invoke(this, new OrderAssignmentResult(toteBarcode, orderBarcode, true, null));
            }
            else
            {
                Interlocked.Increment(ref _failureCount);
                Debug.WriteLine($"OrderAssignmentProcessor: Failed to assign '{orderBarcode}' to '{toteBarcode}'");
                AssignmentCompleted?.Invoke(this, new OrderAssignmentResult(toteBarcode, orderBarcode, false, "Assignment operation returned false"));
            }
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _failureCount);
            Debug.WriteLine($"OrderAssignmentProcessor: Error during assignment: {ex.Message}");
            AssignmentCompleted?.Invoke(this, new OrderAssignmentResult(toteBarcode, orderBarcode, false, ex.Message));
        }
    }

    /// <summary>
    /// Forwards a barcode directly to keyboard simulation.
    /// </summary>
    private async Task ForwardBarcodeAsync(string barcode)
    {
        try
        {
            await KeyboardSimulator.SendKeysAsync(
                barcode,
                _config.KeyboardDelayMs,
                _config.AppendEnterKey
            );
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"OrderAssignmentProcessor: Error forwarding barcode: {ex.Message}");
            // Log but don't throw - we don't want to break the processor
        }
    }

    /// <summary>
    /// Resets the assignment statistics.
    /// </summary>
    public void ResetStatistics()
    {
        Interlocked.Exchange(ref _assignmentCount, 0);
        Interlocked.Exchange(ref _failureCount, 0);
    }

    /// <summary>
    /// Clears any pending tote barcode.
    /// </summary>
    public async Task ClearPendingStateAsync()
    {
        await _stateLock.WaitAsync();
        try
        {
            _pendingToteBarcode = null;
            Debug.WriteLine("OrderAssignmentProcessor: Cleared pending state");
        }
        finally
        {
            _stateLock.Release();
        }
    }
}

/// <summary>
/// Result of an order assignment operation.
/// </summary>
public class OrderAssignmentResult
{
    public string ToteBarcode { get; }
    public string OrderBarcode { get; }
    public bool Success { get; }
    public string? ErrorMessage { get; }

    public OrderAssignmentResult(string toteBarcode, string orderBarcode, bool success, string? errorMessage)
    {
        ToteBarcode = toteBarcode;
        OrderBarcode = orderBarcode;
        Success = success;
        ErrorMessage = errorMessage;
    }
}
