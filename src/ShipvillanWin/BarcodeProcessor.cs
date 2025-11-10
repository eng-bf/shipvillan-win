using System;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;

namespace ShipvillanWin;

/// <summary>
/// Coordinates barcode processing with prefix checking, async interception, and keyboard simulation.
/// </summary>
[SupportedOSPlatform("windows")]
public class BarcodeProcessor
{
    private readonly AppConfiguration _config;
    private readonly IInterceptionService _interceptionService;
    private readonly SemaphoreSlim _processingLock = new(1, 1);
    private int _rejectedCount = 0;

    /// <summary>
    /// Fired when processing status changes (started/completed).
    /// </summary>
    public event EventHandler<bool>? ProcessingStatusChanged;

    /// <summary>
    /// Fired when a barcode is rejected due to ongoing processing.
    /// </summary>
    public event EventHandler? BarcodeRejected;

    /// <summary>
    /// Gets whether a barcode is currently being processed.
    /// </summary>
    public bool IsProcessing => _processingLock.CurrentCount == 0;

    /// <summary>
    /// Gets the count of rejected barcodes since last reset.
    /// </summary>
    public int RejectedCount => _rejectedCount;

    public BarcodeProcessor(AppConfiguration config, IInterceptionService interceptionService)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _interceptionService = interceptionService ?? throw new ArgumentNullException(nameof(interceptionService));
    }

    /// <summary>
    /// Processes a scanned barcode.
    /// Returns immediately if another barcode is being processed (rejects the scan).
    /// </summary>
    public async Task ProcessBarcodeAsync(string barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode))
            return;

        // Try to acquire processing lock (non-blocking)
        if (!_processingLock.Wait(0))
        {
            // Another barcode is being processed - reject this one
            Interlocked.Increment(ref _rejectedCount);
            Debug.WriteLine($"BarcodeProcessor: Rejected barcode '{barcode}' (processing in progress)");
            BarcodeRejected?.Invoke(this, EventArgs.Empty);
            return;
        }

        try
        {
            ProcessingStatusChanged?.Invoke(this, true);
            Debug.WriteLine($"BarcodeProcessor: Processing barcode '{barcode}'");

            // Check if barcode starts with configured prefix
            if (barcode.StartsWith(_config.BarcodePrefix, StringComparison.OrdinalIgnoreCase))
            {
                // CT- prefix: Perform async interception
                await ProcessInterceptedBarcodeAsync(barcode);
            }
            else
            {
                // No prefix: Forward immediately
                await ForwardBarcodeAsync(barcode);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"BarcodeProcessor: Error processing barcode '{barcode}': {ex.Message}");
            // Error is logged but we don't send anything to keyboard on failure
        }
        finally
        {
            _processingLock.Release();
            ProcessingStatusChanged?.Invoke(this, false);
        }
    }

    /// <summary>
    /// Processes a barcode that matches the interception prefix.
    /// Calls the async service and sends the result to keyboard.
    /// </summary>
    private async Task ProcessInterceptedBarcodeAsync(string barcode)
    {
        Debug.WriteLine($"BarcodeProcessor: Barcode matches prefix '{_config.BarcodePrefix}', triggering interception");

        using var cts = new CancellationTokenSource(_config.InterceptionTimeoutMs);

        try
        {
            var result = await _interceptionService.ProcessBarcodeAsync(barcode, cts.Token);

            if (!string.IsNullOrEmpty(result))
            {
                Debug.WriteLine($"BarcodeProcessor: Interception returned '{result}', sending to keyboard");
                await KeyboardSimulator.SendKeysAsync(
                    result,
                    _config.KeyboardDelayMs,
                    _config.AppendEnterKey
                );
            }
            else
            {
                Debug.WriteLine("BarcodeProcessor: Interception returned null/empty, not sending keyboard input");
            }
        }
        catch (OperationCanceledException)
        {
            Debug.WriteLine($"BarcodeProcessor: Interception timed out after {_config.InterceptionTimeoutMs}ms");
            // Timeout: Don't send anything
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"BarcodeProcessor: Interception error: {ex.Message}");
            // Error: Don't send anything
        }
    }

    /// <summary>
    /// Forwards a barcode directly to keyboard simulation without interception.
    /// </summary>
    private async Task ForwardBarcodeAsync(string barcode)
    {
        Debug.WriteLine($"BarcodeProcessor: Forwarding barcode '{barcode}' directly to keyboard");

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
            Debug.WriteLine($"BarcodeProcessor: Error forwarding barcode: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Resets the rejected barcode counter.
    /// </summary>
    public void ResetRejectedCount()
    {
        Interlocked.Exchange(ref _rejectedCount, 0);
    }
}
