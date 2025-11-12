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
public class OrderAssignmentProcessor : IDisposable
{
    private readonly AppConfiguration _config;
    private readonly IOrderAssignmentService _orderAssignmentService;
    private readonly ToteApiService _toteApiService;
    private readonly SemaphoreSlim _stateLock = new(1, 1);

    private string? _pendingToteBarcode = null;
    private ToteData? _pendingToteData = null;
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
        _toteApiService = new ToteApiService(config.ApiBaseUrl, config.ApiTimeoutMs);
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

            // Skip tote API check for CT- barcodes (they will never be totes)
            if (!barcode.StartsWith("CT-", StringComparison.OrdinalIgnoreCase))
            {
                // Check if this barcode is a tote via API (fire and forget, non-blocking)
                _ = CheckAndStoreToteAsync(barcode);
            }

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
    /// Checks if a barcode is a tote via API and stores it if confirmed.
    /// Runs asynchronously without blocking.
    /// </summary>
    private async Task CheckAndStoreToteAsync(string barcode)
    {
        try
        {
            Debug.WriteLine($"OrderAssignmentProcessor: Checking if '{barcode}' is a tote via API");

            var toteData = await _toteApiService.CheckToteAsync(barcode);

            if (toteData != null)
            {
                await _stateLock.WaitAsync();
                try
                {
                    // Store the tote barcode and data (replaces any previous tote)
                    _pendingToteBarcode = barcode;
                    _pendingToteData = toteData;

                    var orderCount = toteData.Orders?.Length ?? 0;
                    Debug.WriteLine($"OrderAssignmentProcessor: Confirmed tote '{barcode}' (Name: {toteData.Name}, Orders: {orderCount}). Waiting for CT- barcode.");
                }
                finally
                {
                    _stateLock.Release();
                }
            }
            else
            {
                Debug.WriteLine($"OrderAssignmentProcessor: Barcode '{barcode}' is not a tote");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"OrderAssignmentProcessor: Error checking tote: {ex.Message}");
        }
    }

    /// <summary>
    /// Processes the order assignment state machine logic.
    /// Only triggers assignment when we have a pending tote AND barcode starts with CT-.
    /// </summary>
    private async Task ProcessOrderAssignmentLogicAsync(string barcode)
    {
        await _stateLock.WaitAsync();

        try
        {
            // Check if we have a pending tote barcode AND this barcode starts with CT-
            if (!string.IsNullOrEmpty(_pendingToteBarcode) &&
                _pendingToteData != null &&
                barcode.StartsWith("CT-", StringComparison.OrdinalIgnoreCase))
            {
                var toteBarcode = _pendingToteBarcode;
                var toteData = _pendingToteData;
                var crossTagCode = barcode;

                // Clear the pending tote data before starting async operation
                _pendingToteBarcode = null;
                _pendingToteData = null;

                Debug.WriteLine($"OrderAssignmentProcessor: Received CT- barcode '{crossTagCode}' after tote '{toteBarcode}', triggering assignment");

                // Fire and forget - perform assignment in background without blocking
                _ = PerformAssignmentAsync(toteBarcode, toteData, crossTagCode);
            }
            else if (barcode.StartsWith("CT-", StringComparison.OrdinalIgnoreCase))
            {
                // CT- barcode without pending tote
                Debug.WriteLine($"OrderAssignmentProcessor: Received CT- barcode '{barcode}' but no pending tote (ignoring)");
            }
            else
            {
                // Not a CT- barcode - just a regular barcode
                Debug.WriteLine($"OrderAssignmentProcessor: Regular barcode '{barcode}' (waiting for CT- barcode)");
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
    private async Task PerformAssignmentAsync(string toteBarcode, ToteData toteData, string crossTagCode)
    {
        Interlocked.Increment(ref _assignmentCount);

        try
        {
            Debug.WriteLine($"OrderAssignmentProcessor: Starting background assignment of CT '{crossTagCode}' to tote '{toteBarcode}'");

            using var cts = new CancellationTokenSource(_config.InterceptionTimeoutMs);

            var success = await _orderAssignmentService.LinkOrderAsync(toteBarcode, toteData, crossTagCode, cts.Token);

            if (success)
            {
                Debug.WriteLine($"OrderAssignmentProcessor: Successfully linked CT '{crossTagCode}' to tote '{toteBarcode}'");
                AssignmentCompleted?.Invoke(this, new OrderAssignmentResult(toteBarcode, crossTagCode, true, null));
            }
            else
            {
                Interlocked.Increment(ref _failureCount);
                Debug.WriteLine($"OrderAssignmentProcessor: Failed to link CT '{crossTagCode}' to tote '{toteBarcode}'");
                AssignmentCompleted?.Invoke(this, new OrderAssignmentResult(toteBarcode, crossTagCode, false, "Link operation returned false"));
            }
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _failureCount);
            Debug.WriteLine($"OrderAssignmentProcessor: Error during link operation: {ex.Message}");
            AssignmentCompleted?.Invoke(this, new OrderAssignmentResult(toteBarcode, crossTagCode, false, ex.Message));
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
            _pendingToteData = null;
            Debug.WriteLine("OrderAssignmentProcessor: Cleared pending state");
        }
        finally
        {
            _stateLock.Release();
        }
    }

    /// <summary>
    /// Disposes resources used by the processor.
    /// </summary>
    public void Dispose()
    {
        _toteApiService?.Dispose();
        _stateLock?.Dispose();
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
