using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace ShipvillanWin;

/// <summary>
/// Service for calling the cross-tag tote lookup API.
/// Checks if a barcode corresponds to a valid tote in ShipHero.
/// </summary>
public class ToteApiService : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    public ToteApiService(string baseUrl, int timeoutMs = 3000)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMilliseconds(timeoutMs)
        };
    }

    /// <summary>
    /// Checks if the given barcode is a valid tote.
    /// Returns the tote data if found, null otherwise.
    /// This method is non-blocking and designed to be called with fire-and-forget.
    /// </summary>
    public async Task<ToteData?> CheckToteAsync(string barcode, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"{_baseUrl}/cross-tag/tote?barcode={Uri.EscapeDataString(barcode)}";
            Debug.WriteLine($"[ToteApiService] ========== TOTE API REQUEST ==========");
            Debug.WriteLine($"[ToteApiService] URL: {url}");
            Debug.WriteLine($"[ToteApiService] Barcode: '{barcode}'");
            Debug.WriteLine($"[ToteApiService] Making GET request...");

            var response = await _httpClient.GetAsync(url, cancellationToken);

            Debug.WriteLine($"[ToteApiService] Response Status: {(int)response.StatusCode} {response.StatusCode}");
            Debug.WriteLine($"[ToteApiService] Response Headers: {response.Headers}");

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                Debug.WriteLine($"[ToteApiService] ========== RESPONSE BODY ==========");
                Debug.WriteLine($"[ToteApiService] {json}");
                Debug.WriteLine($"[ToteApiService] ====================================");

                var toteData = JsonSerializer.Deserialize<ToteData>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (toteData != null)
                {
                    Debug.WriteLine($"[ToteApiService] ✓ Tote found successfully!");
                    Debug.WriteLine($"[ToteApiService]   - ID: {toteData.Id}");
                    Debug.WriteLine($"[ToteApiService]   - Name: {toteData.Name}");
                    Debug.WriteLine($"[ToteApiService]   - Barcode: {toteData.Barcode}");
                    Debug.WriteLine($"[ToteApiService]   - Warehouse ID: {toteData.Warehouse?.Id}");
                    Debug.WriteLine($"[ToteApiService]   - Warehouse Identifier: {toteData.Warehouse?.Identifier}");
                    Debug.WriteLine($"[ToteApiService]   - Orders Count: {toteData.Orders?.Length ?? 0}");

                    if (toteData.Orders != null && toteData.Orders.Length > 0)
                    {
                        var firstOrder = toteData.Orders[0];
                        Debug.WriteLine($"[ToteApiService]   - First Order ID: {firstOrder.Id}");
                        Debug.WriteLine($"[ToteApiService]   - First Order Number: {firstOrder.OrderNumber}");
                        Debug.WriteLine($"[ToteApiService]   - First Order Partner ID: {firstOrder.PartnerOrderId}");
                        Debug.WriteLine($"[ToteApiService]   - First Order Account ID: {firstOrder.AccountId}");

                        if (firstOrder.LineItems?.Edges != null && firstOrder.LineItems.Edges.Length > 0)
                        {
                            Debug.WriteLine($"[ToteApiService]   - Line Items Count: {firstOrder.LineItems.Edges.Length}");
                            for (int i = 0; i < firstOrder.LineItems.Edges.Length; i++)
                            {
                                var lineItem = firstOrder.LineItems.Edges[i].Node;
                                if (lineItem != null)
                                {
                                    Debug.WriteLine($"[ToteApiService]   - Line Item {i + 1}:");
                                    Debug.WriteLine($"[ToteApiService]     * SKU: {lineItem.Sku}");
                                    Debug.WriteLine($"[ToteApiService]     * Product: {lineItem.ProductName}");
                                    Debug.WriteLine($"[ToteApiService]     * Quantity: {lineItem.Quantity}");
                                    Debug.WriteLine($"[ToteApiService]     * Price: {lineItem.Price}");
                                    Debug.WriteLine($"[ToteApiService]     * Custom Barcode: {lineItem.CustomBarcode}");
                                    Debug.WriteLine($"[ToteApiService]     * Warehouse: {lineItem.Warehouse}");
                                }
                            }
                        }
                        else
                        {
                            Debug.WriteLine($"[ToteApiService]   - No line items in order");
                        }
                    }

                    Debug.WriteLine($"[ToteApiService] ========================================");
                    return toteData;
                }
                else
                {
                    Debug.WriteLine($"[ToteApiService] ✗ Failed to deserialize tote data");
                }
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                Debug.WriteLine($"[ToteApiService] ✗ Tote not found (404) for barcode '{barcode}'");
                return null;
            }
            else
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                Debug.WriteLine($"[ToteApiService] ✗ API Error Response:");
                Debug.WriteLine($"[ToteApiService]   Status: {response.StatusCode}");
                Debug.WriteLine($"[ToteApiService]   Body: {errorBody}");
                return null;
            }
        }
        catch (OperationCanceledException)
        {
            Debug.WriteLine($"[ToteApiService] ✗ Operation CANCELLED (timeout) for barcode '{barcode}'");
            return null;
        }
        catch (HttpRequestException ex)
        {
            Debug.WriteLine($"[ToteApiService] ✗ HTTP REQUEST FAILED for barcode '{barcode}'");
            Debug.WriteLine($"[ToteApiService]   Error: {ex.Message}");
            Debug.WriteLine($"[ToteApiService]   Stack: {ex.StackTrace}");
            return null;
        }
        catch (JsonException ex)
        {
            Debug.WriteLine($"[ToteApiService] ✗ JSON DESERIALIZATION FAILED for barcode '{barcode}'");
            Debug.WriteLine($"[ToteApiService]   Error: {ex.Message}");
            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ToteApiService] ✗ UNEXPECTED ERROR checking tote for barcode '{barcode}'");
            Debug.WriteLine($"[ToteApiService]   Type: {ex.GetType().Name}");
            Debug.WriteLine($"[ToteApiService]   Error: {ex.Message}");
            Debug.WriteLine($"[ToteApiService]   Stack: {ex.StackTrace}");
            return null;
        }

        return null;
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}

/// <summary>
/// Represents tote data returned from the API.
/// </summary>
public class ToteData
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("legacy_id")]
    public int? LegacyId { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("barcode")]
    public string? Barcode { get; set; }

    [JsonPropertyName("warehouse")]
    public WarehouseData? Warehouse { get; set; }

    [JsonPropertyName("orders")]
    public OrderData[]? Orders { get; set; }
}

/// <summary>
/// Represents warehouse data in the tote response.
/// </summary>
public class WarehouseData
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("legacy_id")]
    public int? LegacyId { get; set; }

    [JsonPropertyName("account_id")]
    public string? AccountId { get; set; }

    [JsonPropertyName("identifier")]
    public string? Identifier { get; set; }

    [JsonPropertyName("profile")]
    public string? Profile { get; set; }
}

/// <summary>
/// Represents order data in the tote response.
/// </summary>
public class OrderData
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("legacy_id")]
    public int? LegacyId { get; set; }

    [JsonPropertyName("order_number")]
    public string? OrderNumber { get; set; }

    [JsonPropertyName("account_id")]
    public string? AccountId { get; set; }

    [JsonPropertyName("partner_order_id")]
    public string? PartnerOrderId { get; set; }

    [JsonPropertyName("fulfillment_status")]
    public string? FulfillmentStatus { get; set; }

    [JsonPropertyName("line_items")]
    public LineItemConnection? LineItems { get; set; }
}

/// <summary>
/// Represents GraphQL connection for line items (pagination pattern).
/// </summary>
public class LineItemConnection
{
    [JsonPropertyName("edges")]
    public LineItemEdge[]? Edges { get; set; }
}

/// <summary>
/// Represents an edge in the line items connection.
/// </summary>
public class LineItemEdge
{
    [JsonPropertyName("node")]
    public LineItemData? Node { get; set; }
}

/// <summary>
/// Represents line item data in an order.
/// </summary>
public class LineItemData
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("legacy_id")]
    public int? LegacyId { get; set; }

    [JsonPropertyName("sku")]
    public string? Sku { get; set; }

    [JsonPropertyName("partner_line_item_id")]
    public string? PartnerLineItemId { get; set; }

    [JsonPropertyName("quantity")]
    public int? Quantity { get; set; }

    [JsonPropertyName("quantity_allocated")]
    public int? QuantityAllocated { get; set; }

    [JsonPropertyName("quantity_pending_fulfillment")]
    public int? QuantityPendingFulfillment { get; set; }

    [JsonPropertyName("price")]
    public string? Price { get; set; }

    [JsonPropertyName("product_name")]
    public string? ProductName { get; set; }

    [JsonPropertyName("fulfillment_status")]
    public string? FulfillmentStatus { get; set; }

    [JsonPropertyName("custom_barcode")]
    public string? CustomBarcode { get; set; }

    [JsonPropertyName("warehouse")]
    public string? Warehouse { get; set; }
}
