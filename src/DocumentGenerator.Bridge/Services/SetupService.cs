using System.Text.Json;
using DocumentGenerator.Bridge.Configuration;

namespace DocumentGenerator.Bridge.Services;

/// <summary>
/// Manages reading and writing the bridge configuration file (<c>appsettings.json</c>)
/// during and after the first-run setup wizard.
/// </summary>
public sealed class SetupService
{
    private readonly string _settingsPath;
    private readonly ILogger<SetupService> _logger;
    private readonly IWebHostEnvironment _env;

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Initialises a new <see cref="SetupService"/>.
    /// </summary>
    /// <param name="env">Host environment — used to locate the content root.</param>
    /// <param name="logger">Logger for setup diagnostics.</param>
    public SetupService(IWebHostEnvironment env, ILogger<SetupService> logger)
    {
        _env          = env;
        _logger       = logger;
        _settingsPath = Path.Combine(env.ContentRootPath, "appsettings.json");
    }

    /// <summary>
    /// Saves the completed setup configuration to <c>appsettings.json</c> and
    /// marks the bridge as configured so normal operation resumes on next start.
    /// </summary>
    /// <param name="cloudBaseUrl">Base URL of the cloud Badge Producer API.</param>
    /// <param name="apiKey">API key for authenticating with the cloud API.</param>
    /// <param name="defaultPrinterName">Local printer name; <c>null</c> means OS default.</param>
    /// <param name="format">Document format: <c>"Pdf"</c> or <c>"Png"</c>.</param>
    /// <param name="port">Port the bridge HTTP server should listen on.</param>
    public async Task SaveConfigurationAsync(
        string  cloudBaseUrl,
        string  apiKey,
        string? defaultPrinterName,
        string  format,
        int     port)
    {
        _logger.LogInformation("Saving bridge configuration to {Path}", _settingsPath);

        // Read the existing appsettings.json so we preserve any other keys
        var raw = await File.ReadAllTextAsync(_settingsPath);
        var doc = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(raw)
                  ?? [];

        // Build updated sections
        var updated = new Dictionary<string, object?>(
            doc.ToDictionary(k => k.Key, k => (object?)k.Value))
        {
            [BridgeOptions.Section] = new
            {
                port,
                isConfigured = true
            },
            [CloudOptions.Section] = new
            {
                baseUrl = cloudBaseUrl,
                apiKey,
                timeout = "00:00:30"
            },
            [PrinterOptions.Section] = new
            {
                defaultPrinterName,
                format
            }
        };

        var json = JsonSerializer.Serialize(updated, WriteOptions);
        await File.WriteAllTextAsync(_settingsPath, json);

        _logger.LogInformation("Bridge configuration saved successfully.");
    }

    /// <summary>
    /// Verifies connectivity to the cloud API by calling its <c>/health</c> endpoint.
    /// </summary>
    /// <param name="baseUrl">Cloud base URL to test.</param>
    /// <param name="apiKey">API key to include in the test request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> when the cloud API responds with a healthy status.</returns>
    public async Task<bool> TestCloudConnectionAsync(
        string baseUrl,
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = new HttpClient { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromSeconds(10) };
            client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);

            var response = await client.GetAsync("/health", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cloud connection test failed — BaseUrl={BaseUrl}", baseUrl);
            return false;
        }
    }
}
