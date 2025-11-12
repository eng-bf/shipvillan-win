using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ShipvillanWin;

/// <summary>
/// Default implementation of IOrderAssignmentService.
/// Calls the link_order endpoint to associate cross-tag codes with orders.
/// </summary>
public class OrderAssignmentService : IOrderAssignmentService, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _linkOrderUrl;

    public OrderAssignmentService()
    {
        var config = AppConfiguration.Load();
        _linkOrderUrl = config.LinkOrderUrl;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMilliseconds(config.ApiTimeoutMs)
        };
    }

    /// <summary>
    /// Links a cross-tag code to an order in a tote via the link_order endpoint.
    /// </summary>
    public async Task<bool> LinkOrderAsync(string toteBarcode, ToteData toteData, string crossTagCode, CancellationToken cancellationToken)
    {
        try
        {
            Debug.WriteLine($"[OrderAssignmentService] ========== LINK ORDER REQUEST ==========");
            Debug.WriteLine($"[OrderAssignmentService] Cross-Tag Code: '{crossTagCode}'");
            Debug.WriteLine($"[OrderAssignmentService] Tote Barcode: '{toteBarcode}'");
            Debug.WriteLine($"[OrderAssignmentService] Tote Name: '{toteData.Name}'");

            // Validate we have order data
            if (toteData.Orders == null || toteData.Orders.Length == 0)
            {
                Debug.WriteLine($"[OrderAssignmentService] ✗ VALIDATION FAILED: No orders found in tote");
                LogError(toteBarcode, crossTagCode, new InvalidOperationException("No orders in tote"));
                return false;
            }

            Debug.WriteLine($"[OrderAssignmentService] Orders count in tote: {toteData.Orders.Length}");

            // Get the first order
            var order = toteData.Orders[0];

            Debug.WriteLine($"[OrderAssignmentService] Using first order:");
            Debug.WriteLine($"[OrderAssignmentService]   - ID: {order.Id}");
            Debug.WriteLine($"[OrderAssignmentService]   - Partner Order ID: {order.PartnerOrderId}");
            Debug.WriteLine($"[OrderAssignmentService]   - Order Number: {order.OrderNumber}");
            Debug.WriteLine($"[OrderAssignmentService]   - Account ID: {order.AccountId}");
            Debug.WriteLine($"[OrderAssignmentService]   - Fulfillment Status: {order.FulfillmentStatus}");

            // Validate required order fields
            if (string.IsNullOrEmpty(order.OrderNumber))
            {
                Debug.WriteLine($"[OrderAssignmentService] ✗ VALIDATION FAILED: Order missing order_number");
                LogError(toteBarcode, crossTagCode, new InvalidOperationException("Order missing order_number"));
                return false;
            }

            // Build request payload
            var requestPayload = new
            {
                cross_tag_code = crossTagCode,
                order_id = order.LegacyId?.ToString() ?? "", // Use legacy_id from tote order response
                order_number = order.OrderNumber,
                bin_id = toteBarcode,
                customer_id = order.AccountId ?? "",
                tracking_number = (string?)null, // Optional - we don't have this info
                order_url = (string?)null // Optional - we don't have this info
            };

            var jsonPayload = JsonSerializer.Serialize(requestPayload, new JsonSerializerOptions { WriteIndented = true });
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            Debug.WriteLine($"[OrderAssignmentService] ========== REQUEST DETAILS ==========");
            Debug.WriteLine($"[OrderAssignmentService] Endpoint: {_linkOrderUrl}");
            Debug.WriteLine($"[OrderAssignmentService] Method: POST");
            Debug.WriteLine($"[OrderAssignmentService] Content-Type: application/json");
            Debug.WriteLine($"[OrderAssignmentService] ========== REQUEST PAYLOAD ==========");
            Debug.WriteLine($"[OrderAssignmentService] {jsonPayload}");
            Debug.WriteLine($"[OrderAssignmentService] ====================================");
            Debug.WriteLine($"[OrderAssignmentService] Sending HTTP POST request...");

            // Make HTTP POST request
            var response = await _httpClient.PostAsync(_linkOrderUrl, content, cancellationToken);

            Debug.WriteLine($"[OrderAssignmentService] Response Status: {(int)response.StatusCode} {response.StatusCode}");
            Debug.WriteLine($"[OrderAssignmentService] Response Headers: {response.Headers}");

            if (response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                Debug.WriteLine($"[OrderAssignmentService] ========== RESPONSE BODY ==========");
                Debug.WriteLine($"[OrderAssignmentService] {responseBody}");
                Debug.WriteLine($"[OrderAssignmentService] ===================================");

                // Parse response to get link ID
                var linkResponse = JsonSerializer.Deserialize<LinkOrderResponse>(responseBody, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (linkResponse != null)
                {
                    Debug.WriteLine($"[OrderAssignmentService] ✓ Link created successfully!");
                    Debug.WriteLine($"[OrderAssignmentService]   - Link ID: {linkResponse.Id}");
                    Debug.WriteLine($"[OrderAssignmentService]   - Linked Date: {linkResponse.LinkedDate}");
                    Debug.WriteLine($"[OrderAssignmentService]   - CT Code: {crossTagCode}");
                    Debug.WriteLine($"[OrderAssignmentService]   - Order Number: {order.OrderNumber}");
                    Debug.WriteLine($"[OrderAssignmentService]   - Tote: {toteBarcode}");
                    Debug.WriteLine($"[OrderAssignmentService] ===========================================");
                }
                else
                {
                    Debug.WriteLine($"[OrderAssignmentService] ⚠ Success but failed to parse response");
                }

                return true;
            }
            else
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                Debug.WriteLine($"[OrderAssignmentService] ✗ LINK FAILED");
                Debug.WriteLine($"[OrderAssignmentService]   Status Code: {(int)response.StatusCode} {response.StatusCode}");
                Debug.WriteLine($"[OrderAssignmentService]   Reason: {response.ReasonPhrase}");
                Debug.WriteLine($"[OrderAssignmentService] ========== ERROR RESPONSE ==========");
                Debug.WriteLine($"[OrderAssignmentService] {errorBody}");
                Debug.WriteLine($"[OrderAssignmentService] ===================================");
                LogError(toteBarcode, crossTagCode, new HttpRequestException($"HTTP {response.StatusCode}: {errorBody}"));
                return false;
            }
        }
        catch (OperationCanceledException)
        {
            Debug.WriteLine($"[OrderAssignmentService] ✗ Operation CANCELLED (timeout)");
            Debug.WriteLine($"[OrderAssignmentService]   CT Code: {crossTagCode}");
            Debug.WriteLine($"[OrderAssignmentService]   Tote: {toteBarcode}");
            LogError(toteBarcode, crossTagCode, new OperationCanceledException("Link operation timed out"));
            return false;
        }
        catch (HttpRequestException ex)
        {
            Debug.WriteLine($"[OrderAssignmentService] ✗ HTTP REQUEST FAILED");
            Debug.WriteLine($"[OrderAssignmentService]   Error: {ex.Message}");
            Debug.WriteLine($"[OrderAssignmentService]   Inner Exception: {ex.InnerException?.Message}");
            Debug.WriteLine($"[OrderAssignmentService]   Stack: {ex.StackTrace}");
            LogError(toteBarcode, crossTagCode, ex);
            return false;
        }
        catch (JsonException ex)
        {
            Debug.WriteLine($"[OrderAssignmentService] ✗ JSON SERIALIZATION/DESERIALIZATION FAILED");
            Debug.WriteLine($"[OrderAssignmentService]   Error: {ex.Message}");
            Debug.WriteLine($"[OrderAssignmentService]   Path: {ex.Path}");
            LogError(toteBarcode, crossTagCode, ex);
            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[OrderAssignmentService] ✗ UNEXPECTED ERROR");
            Debug.WriteLine($"[OrderAssignmentService]   Type: {ex.GetType().Name}");
            Debug.WriteLine($"[OrderAssignmentService]   Error: {ex.Message}");
            Debug.WriteLine($"[OrderAssignmentService]   Stack: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                Debug.WriteLine($"[OrderAssignmentService]   Inner Exception: {ex.InnerException.Message}");
            }
            LogError(toteBarcode, crossTagCode, ex);
            return false;
        }
    }

    /// <summary>
    /// Logs errors for diagnostics.
    /// </summary>
    private void LogError(string toteBarcode, string crossTagCode, Exception ex)
    {
        Debug.WriteLine($"[OrderAssignmentService] ========== ERROR LOG ==========");
        Debug.WriteLine($"[OrderAssignmentService] Operation: Link Order");
        Debug.WriteLine($"[OrderAssignmentService] CT Code: {crossTagCode}");
        Debug.WriteLine($"[OrderAssignmentService] Tote: {toteBarcode}");
        Debug.WriteLine($"[OrderAssignmentService] Error Type: {ex.GetType().Name}");
        Debug.WriteLine($"[OrderAssignmentService] Error Message: {ex.Message}");
        if (ex.InnerException != null)
        {
            Debug.WriteLine($"[OrderAssignmentService] Inner Exception: {ex.InnerException.Message}");
        }
        Debug.WriteLine($"[OrderAssignmentService] Stack Trace:");
        Debug.WriteLine($"{ex.StackTrace}");
        Debug.WriteLine($"[OrderAssignmentService] ==============================");
        // TODO: Send to remote logging system if needed
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}

/// <summary>
/// Response from the link_order endpoint.
/// </summary>
public class LinkOrderResponse
{
    public int? Id { get; set; }
    public string? LinkedDate { get; set; }
}
