using System.Text.Json;
using DocumentGenerator.Bridge.Configuration;
using Microsoft.AspNetCore.DataProtection;

namespace DocumentGenerator.Bridge.Services;

/// <summary>
/// Manages reading and writing the bridge configuration file (<c>appsettings.json</c>)
/// during and after the first-run setup wizard.
///
/// <para>
/// The cloud API key is stored in protected form using ASP.NET Core Data Protection
/// (DPAPI on Windows, OS-level key ring on Linux/macOS). The <see cref="CloudBadgeClient"/>
/// reads the protected value at runtime and decrypts it using the same <see cref="IDataProtector"/>.
/// </para>
/// </summary>
public sealed class SetupService
{
    private readonly string _settingsPath;
    private readonly ILogger<SetupService> _logger;
    private readonly IDataProtector _protector;

    private const string ProtectorPurpose = "Bridge.CloudApiKey.v1";

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented        = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Initialises a new <see cref="SetupService"/>.
    /// </summary>
    public SetupService(
        IWebHostEnvironment env,
        IDataProtectionProvider dataProtectionProvider,
        ILogger<SetupService> logger)
    {
        _logger       = logger;
        _protector    = dataProtectionProvider.CreateProtector(ProtectorPurpose);
        _settingsPath = Path.Combine(env.ContentRootPath, "appsettings.json");
    }

    /// <summary>
    /// Saves the completed setup configuration to <c>appsettings.json</c> and
    /// marks the bridge as configured so normal operation resumes on next start.
    ///
    /// The API key is stored as a Data-Protection-encrypted string.
    /// </summary>
    public async Task SaveConfigurationAsync(
        string  cloudBaseUrl,
        string  apiKey,
        string? defaultPrinterName,
        string  format,
        int     port)
    {
        _logger.LogInformation("Saving bridge configuration to {Path}", _settingsPath);

        // Protect the API key using ASP.NET Core Data Protection (DPAPI on Windows,
        // OS key ring on Linux/macOS). This prevents the key from being readable in
        // plaintext if the appsettings.json file is leaked.
        var protectedApiKey = _protector.Protect(apiKey);

        var raw = await File.ReadAllTextAsync(_settingsPath).ConfigureAwait(false);
        var doc = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(raw) ?? [];

        var updated = new Dictionary<string, object?>(
            doc.ToDictionary(k => k.Key, k => (object?)k.Value))
        {
            [BridgeOptions.SectionName] = new
            {
                port,
                isConfigured = true
            },
            [CloudOptions.SectionName] = new
            {
                baseUrl          = cloudBaseUrl,
                protectedApiKey,          // stored encrypted; runtime reads via IDataProtector
                timeout          = "00:00:30"
            },
            [PrinterOptions.SectionName] = new
            {
                defaultPrinterName,
                format
            }
        };

        var json = JsonSerializer.Serialize(updated, WriteOptions);
        await File.WriteAllTextAsync(_settingsPath, json).ConfigureAwait(false);

        _logger.LogInformation("Bridge configuration saved successfully (API key is protected).");
    }

    /// <summary>
    /// Decrypts and returns the stored API key, or an empty string if not set.
    /// </summary>
    public string UnprotectApiKey(string protectedApiKey)
    {
        if (string.IsNullOrWhiteSpace(protectedApiKey))
            return string.Empty;
        try
        {
            return _protector.Unprotect(protectedApiKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt cloud API key. The key may have been protected on a different machine or with a different key ring. Please re-run the setup wizard.");
            return string.Empty;
        }
    }

    /// <summary>
    /// Verifies connectivity to the cloud API by calling its <c>/health</c> endpoint.
    /// Uses a fresh <see cref="HttpClient"/> so it is not affected by circuit-breaker state.
    /// </summary>
    public async Task<bool> TestCloudConnectionAsync(
        string baseUrl,
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = new HttpClient
            {
                BaseAddress = new Uri(baseUrl),
                Timeout     = TimeSpan.FromSeconds(10)
            };
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
