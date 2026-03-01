using System.Net.Http.Json;
using System.Text.Json;
using DocumentGenerator.Bridge.Configuration;
using DocumentGenerator.Bridge.Models;
using Microsoft.Extensions.Options;

namespace DocumentGenerator.Bridge.Services;

/// <summary>
/// HTTP client that calls the cloud-hosted <c>DocumentGenerator.Api</c> to render badges.
/// The bridge uses this to forward iPad requests to the cloud and retrieve Base64 documents.
/// </summary>
public sealed class CloudBadgeClient
{
    /// <summary>Named <see cref="HttpClient"/> key used in DI registration.</summary>
    public const string HttpClientName = "CloudBadgeApi";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<CloudOptions> _cloudOptions;
    private readonly ILogger<CloudBadgeClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Initialises a new <see cref="CloudBadgeClient"/>.
    /// </summary>
    /// <param name="httpClientFactory">Factory used to resolve the named HTTP client.</param>
    /// <param name="cloudOptions">Live cloud connection options (URL + API key).</param>
    /// <param name="logger">Logger for request diagnostics.</param>
    public CloudBadgeClient(
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<CloudOptions> cloudOptions,
        ILogger<CloudBadgeClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _cloudOptions      = cloudOptions;
        _logger            = logger;
    }

    /// <summary>
    /// Requests the cloud API to render a badge and returns the response containing Base64 bytes.
    /// </summary>
    /// <param name="request">Badge render parameters from the iPad.</param>
    /// <param name="format">Output format: <c>"Pdf"</c> or <c>"Png"</c>.</param>
    /// <param name="correlationId">Correlation ID to pass through to the cloud.</param>
    /// <param name="cancellationToken">Propagated from the HTTP request lifetime.</param>
    /// <returns>A <see cref="CloudRenderResponse"/> containing the Base64 document.</returns>
    /// <exception cref="HttpRequestException">Thrown on non-success HTTP status.</exception>
    public async Task<CloudRenderResponse> RenderAsync(
        PrintRequest      request,
        string            format,
        Guid              correlationId,
        CancellationToken cancellationToken = default)
    {
        var opts   = _cloudOptions.CurrentValue;
        var client = _httpClientFactory.CreateClient(HttpClientName);

        var payload = new
        {
            templateName  = request.TemplateName,
            variables     = request.Variables,
            branding      = request.Branding,
            format,
            correlationId
        };

        _logger.LogInformation(
            "Cloud render request — CorrelationId={CorrelationId} Template={Template} Url={Url}",
            correlationId, request.TemplateName, opts.BaseUrl);

        var response = await client.PostAsJsonAsync(
            "/api/badges/render", payload, JsonOptions, cancellationToken);

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<CloudRenderResponse>(
            JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Cloud API returned an empty response body.");

        _logger.LogInformation(
            "Cloud render complete — CorrelationId={CorrelationId} Success={Success} Elapsed={Elapsed}ms",
            correlationId, result.Success, result.ElapsedTime.TotalMilliseconds);

        return result;
    }

    /// <summary>
    /// Retrieves the list of badge templates available on the cloud.
    /// </summary>
    /// <param name="cancellationToken">Propagated from the HTTP request lifetime.</param>
    /// <returns>Array of template name strings.</returns>
    public async Task<IEnumerable<string>> ListTemplatesAsync(CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);
        var result = await client.GetFromJsonAsync<IEnumerable<string>>(
            "/api/badges/templates", JsonOptions, cancellationToken);
        return result ?? [];
    }
}

/// <summary>
/// Shape of the JSON response returned by <c>POST /api/badges/render</c> on the cloud API.
/// </summary>
public sealed class CloudRenderResponse
{
    /// <summary>Correlation ID echoed from the request.</summary>
    public Guid CorrelationId { get; init; }
    /// <summary>Server-side job ID.</summary>
    public Guid JobId { get; init; }
    /// <summary><c>true</c> on success.</summary>
    public bool Success { get; init; }
    /// <summary>Base64-encoded PDF or PNG bytes.</summary>
    public string? DocumentBase64 { get; init; }
    /// <summary>MIME type of the document.</summary>
    public string? MimeType { get; init; }
    /// <summary>Document type from the template.</summary>
    public string? DocumentType { get; init; }
    /// <summary>Cloud-side render duration.</summary>
    public TimeSpan ElapsedTime { get; init; }
    /// <summary>Error message when <see cref="Success"/> is <c>false</c>.</summary>
    public string? Error { get; init; }
}
