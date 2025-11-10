using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ShipvillanWin;

/// <summary>
/// Application configuration settings.
/// Stored as JSON in %APPDATA%\ShipvillanWin\config.json
/// </summary>
public class AppConfiguration
{
    private static readonly string ConfigDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ShipvillanWin"
    );

    private static readonly string ConfigFilePath = Path.Combine(ConfigDirectory, "config.json");

    /// <summary>
    /// Current operation mode (Order Assignment or Interception).
    /// </summary>
    [JsonPropertyName("operationMode")]
    public OperationMode Mode { get; set; } = OperationMode.Interception;

    /// <summary>
    /// Selected COM port for barcode scanner (e.g., "COM3").
    /// </summary>
    [JsonPropertyName("comPort")]
    public string? ComPort { get; set; }

    /// <summary>
    /// Baud rate for serial communication.
    /// </summary>
    [JsonPropertyName("baudRate")]
    public int BaudRate { get; set; } = 9600;

    /// <summary>
    /// Data bits for serial communication.
    /// </summary>
    [JsonPropertyName("dataBits")]
    public int DataBits { get; set; } = 8;

    /// <summary>
    /// Barcode prefix that triggers async interception (e.g., "CT-").
    /// </summary>
    [JsonPropertyName("barcodePrefix")]
    public string BarcodePrefix { get; set; } = "CT-";

    /// <summary>
    /// Delay in milliseconds between simulated keystrokes.
    /// </summary>
    [JsonPropertyName("keyboardDelayMs")]
    public int KeyboardDelayMs { get; set; } = 5;

    /// <summary>
    /// Timeout in milliseconds for async interception operations.
    /// </summary>
    [JsonPropertyName("interceptionTimeoutMs")]
    public int InterceptionTimeoutMs { get; set; } = 5000;

    /// <summary>
    /// Whether to append Enter key after keyboard simulation.
    /// </summary>
    [JsonPropertyName("appendEnterKey")]
    public bool AppendEnterKey { get; set; } = true;

    /// <summary>
    /// Loads configuration from disk. Returns default configuration if file doesn't exist.
    /// </summary>
    public static AppConfiguration Load()
    {
        try
        {
            if (!File.Exists(ConfigFilePath))
            {
                var defaultConfig = new AppConfiguration();
                defaultConfig.Save();
                return defaultConfig;
            }

            var json = File.ReadAllText(ConfigFilePath);
            var config = JsonSerializer.Deserialize<AppConfiguration>(json);

            return config ?? new AppConfiguration();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load configuration: {ex.Message}");
            return new AppConfiguration();
        }
    }

    /// <summary>
    /// Saves configuration to disk.
    /// </summary>
    public void Save()
    {
        try
        {
            Directory.CreateDirectory(ConfigDirectory);

            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            var json = JsonSerializer.Serialize(this, options);
            File.WriteAllText(ConfigFilePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save configuration: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Gets the full path to the configuration file.
    /// </summary>
    public static string GetConfigFilePath() => ConfigFilePath;
}
