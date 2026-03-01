namespace DocumentGenerator.Bridge.Configuration;

/// <summary>
/// Root configuration for the bridge service, persisted to <c>appsettings.json</c>
/// after the first-run setup wizard completes.
/// </summary>
public sealed class BridgeOptions
{
    /// <summary>Configuration section name used when binding from <c>appsettings.json</c>.</summary>
    public const string Section = "Bridge";

    /// <summary>
    /// Port the bridge HTTP server listens on.
    /// The iPad calls <c>http://&lt;bridge-host&gt;:&lt;Port&gt;/print</c>.
    /// Defaults to <c>5100</c>.
    /// </summary>
    public int Port { get; set; } = 5100;

    /// <summary>
    /// When <c>true</c>, the bridge has been configured via the setup wizard
    /// and is ready to accept print requests.
    /// When <c>false</c>, the bridge redirects all requests to <c>/setup</c>.
    /// </summary>
    public bool IsConfigured { get; set; } = false;
}

/// <summary>
/// Cloud Badge Producer API connection settings.
/// </summary>
public sealed class CloudOptions
{
    /// <summary>Configuration section name.</summary>
    public const string Section = "Cloud";

    /// <summary>
    /// Base URL of the hosted <c>DocumentGenerator.Api</c> instance,
    /// e.g. <c>https://badges.yourcompany.com</c>.
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// API key sent in the <c>X-Api-Key</c> header with every cloud request.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// HTTP request timeout for cloud render calls.
    /// Badge rendering typically completes within a few seconds.
    /// Defaults to 30 seconds.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// Local printer configuration selected during the setup wizard.
/// </summary>
public sealed class PrinterOptions
{
    /// <summary>Configuration section name.</summary>
    public const string Section = "Printer";

    /// <summary>
    /// Name of the default local printer to send badges to.
    /// When <c>null</c> or empty, the OS default printer is used.
    /// </summary>
    public string? DefaultPrinterName { get; set; }

    /// <summary>
    /// Document format requested from the cloud API.
    /// Accepted values: <c>"Pdf"</c> (default) or <c>"Png"</c>.
    /// </summary>
    public string Format { get; set; } = "Pdf";
}
