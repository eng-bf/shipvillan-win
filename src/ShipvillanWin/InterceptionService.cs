using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ShipvillanWin;

/// <summary>
/// Default implementation of IInterceptionService.
/// Makes HTTP requests to the cross-tag API to retrieve order information.
/// </summary>
public class InterceptionService : IInterceptionService, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _crossTagApiBaseUrl;

    public InterceptionService(string crossTagApiBaseUrl = "http://152.232.229.246:5113", int timeoutMs = 10000)
    {
        _crossTagApiBaseUrl = crossTagApiBaseUrl.TrimEnd('/');
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMilliseconds(timeoutMs)
        };
    }

    /// <summary>
    /// Processes a barcode asynchronously by calling the cross-tag API.
    /// Returns the order number if found, null otherwise.
    /// Shows a warning dialog if the request fails or returns unexpected data.
    /// </summary>
    public async Task<string?> ProcessBarcodeAsync(string barcode, CancellationToken cancellationToken)
    {
        try
        {
            Debug.WriteLine($"[InterceptionService] ========== CROSS-TAG API REQUEST ==========");
            Debug.WriteLine($"[InterceptionService] Barcode: '{barcode}'");

            var url = $"{_crossTagApiBaseUrl}/get_ct_info/{Uri.EscapeDataString(barcode)}";
            Debug.WriteLine($"[InterceptionService] URL: {url}");
            Debug.WriteLine($"[InterceptionService] Making GET request...");

            var response = await _httpClient.GetAsync(url, cancellationToken);

            Debug.WriteLine($"[InterceptionService] Response Status: {(int)response.StatusCode} {response.StatusCode}");

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                Debug.WriteLine($"[InterceptionService] ========== RESPONSE BODY ==========");
                Debug.WriteLine($"[InterceptionService] {json}");
                Debug.WriteLine($"[InterceptionService] ====================================");

                var crossTagResponse = JsonSerializer.Deserialize<CrossTagResponse>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                // Validate response structure and extract order_number
                if (crossTagResponse?.Orders != null && crossTagResponse.Orders.Length > 0)
                {
                    var orderNumber = crossTagResponse.Orders[0].OrderNumber;

                    if (!string.IsNullOrEmpty(orderNumber))
                    {
                        Debug.WriteLine($"[InterceptionService] ✓ Order found successfully!");
                        Debug.WriteLine($"[InterceptionService]   - Order Number: {orderNumber}");
                        Debug.WriteLine($"[InterceptionService]   - Order ID: {crossTagResponse.Orders[0].OrderId}");
                        Debug.WriteLine($"[InterceptionService]   - Cross Tag Code: {crossTagResponse.CrossTag?.CrossTagCode}");
                        Debug.WriteLine($"[InterceptionService] ========================================");

                        return orderNumber;
                    }
                    else
                    {
                        Debug.WriteLine($"[InterceptionService] ✗ Order number is empty");
                        ShowErrorDialog(barcode);
                        return null;
                    }
                }
                else
                {
                    Debug.WriteLine($"[InterceptionService] ✗ No orders found in response");
                    ShowErrorDialog(barcode);
                    return null;
                }
            }
            else
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                Debug.WriteLine($"[InterceptionService] ✗ API Error Response:");
                Debug.WriteLine($"[InterceptionService]   Status: {response.StatusCode}");
                Debug.WriteLine($"[InterceptionService]   Body: {errorBody}");
                ShowErrorDialog(barcode);
                return null;
            }
        }
        catch (OperationCanceledException)
        {
            Debug.WriteLine($"[InterceptionService] ✗ Operation CANCELLED (timeout) for barcode '{barcode}'");
            ShowErrorDialog(barcode);
            return null;
        }
        catch (HttpRequestException ex)
        {
            Debug.WriteLine($"[InterceptionService] ✗ HTTP REQUEST FAILED for barcode '{barcode}'");
            Debug.WriteLine($"[InterceptionService]   Error: {ex.Message}");
            Debug.WriteLine($"[InterceptionService]   Stack: {ex.StackTrace}");
            LogError(barcode, ex);
            ShowErrorDialog(barcode);
            return null;
        }
        catch (JsonException ex)
        {
            Debug.WriteLine($"[InterceptionService] ✗ JSON DESERIALIZATION FAILED for barcode '{barcode}'");
            Debug.WriteLine($"[InterceptionService]   Error: {ex.Message}");
            LogError(barcode, ex);
            ShowErrorDialog(barcode);
            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[InterceptionService] ✗ UNEXPECTED ERROR processing barcode '{barcode}'");
            Debug.WriteLine($"[InterceptionService]   Type: {ex.GetType().Name}");
            Debug.WriteLine($"[InterceptionService]   Error: {ex.Message}");
            Debug.WriteLine($"[InterceptionService]   Stack: {ex.StackTrace}");
            LogError(barcode, ex);
            ShowErrorDialog(barcode);
            return null;
        }
    }

    /// <summary>
    /// Shows a Windows warning dialog when unable to find an order for the cross-tag.
    /// </summary>
    private void ShowErrorDialog(string barcode)
    {
        MessageBox.Show(
            $"Unable to find an order associated to the scanned cross tag: {barcode}\n\nPlease contact IT",
            "Cross-Tag Lookup Failed",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning
        );
    }

    /// <summary>
    /// Logs errors for diagnostics.
    /// TODO: Implement remote logging when ready.
    /// </summary>
    private void LogError(string barcode, Exception ex)
    {
        // For now, just log to debug output
        Debug.WriteLine($"ERROR: Failed to process barcode '{barcode}': {ex}");

        // TODO: Send to remote logging system
        // - Write to log file
        // - Send to logging service (Sentry, Application Insights, etc.)
        // - Store in local database for later sync
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}

/// <summary>
/// Represents the cross-tag API response structure.
/// </summary>
public class CrossTagResponse
{
    [JsonPropertyName("cross_tag")]
    public CrossTagInfo? CrossTag { get; set; }

    [JsonPropertyName("orders")]
    public CrossTagOrder[]? Orders { get; set; }
}

/// <summary>
/// Represents cross-tag information in the API response.
/// </summary>
public class CrossTagInfo
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("cross_tag_code")]
    public string? CrossTagCode { get; set; }

    [JsonPropertyName("created_at")]
    public string? CreatedAt { get; set; }

    [JsonPropertyName("created_by")]
    public int? CreatedBy { get; set; }
}

/// <summary>
/// Represents order information associated with a cross-tag.
/// </summary>
public class CrossTagOrder
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("order_id")]
    public string? OrderId { get; set; }

    [JsonPropertyName("order_number")]
    public string? OrderNumber { get; set; }

    [JsonPropertyName("order_url")]
    public string? OrderUrl { get; set; }

    [JsonPropertyName("tracking_number")]
    public string? TrackingNumber { get; set; }

    [JsonPropertyName("cross_tag_id")]
    public int? CrossTagId { get; set; }

    [JsonPropertyName("customer_id")]
    public string? CustomerId { get; set; }

    [JsonPropertyName("bin_id")]
    public string? BinId { get; set; }

    [JsonPropertyName("linked_date")]
    public string? LinkedDate { get; set; }
}
