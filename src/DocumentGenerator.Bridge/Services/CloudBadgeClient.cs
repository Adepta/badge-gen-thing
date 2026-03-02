using System.Net.Http.Json;
using System.Text.Json;
using DocumentGenerator.Bridge.Configuration;
using DocumentGenerator.Bridge.Models;
using DocumentGenerator.Core.Errors;
using Microsoft.AspNetCore.DataProtection;
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
    private readonly IDataProtector _protector;
    private readonly ILogger<CloudBadgeClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Initialises a new <see cref="CloudBadgeClient"/>.
    /// </summary>
    public CloudBadgeClient(
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<CloudOptions> cloudOptions,
        IDataProtectionProvider dataProtectionProvider,
        ILogger<CloudBadgeClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _cloudOptions      = cloudOptions;
        _protector         = dataProtectionProvider.CreateProtector("Bridge.CloudApiKey.v1");
        _logger            = logger;
    }

    /// <summary>
    /// Resolves the effective API key — prefers the Data-Protection-encrypted value
    /// written by the setup wizard over the plaintext fallback.
    /// </summary>
    private string GetApiKey(CloudOptions opts)
    {
        if (!string.IsNullOrWhiteSpace(opts.ProtectedApiKey))
        {
            try { return _protector.Unprotect(opts.ProtectedApiKey); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to decrypt ProtectedApiKey — falling back to ApiKey.");
            }
        }
        return opts.ApiKey;
    }

    /// <summary>
    /// Requests the cloud API to render a badge and returns the response containing Base64 bytes.
    /// </summary>
    /// <param name="request">Badge render parameters from the iPad.</param>
    /// <param name="format">Output format: <c>"Pdf"</c> or <c>"Png"</c>.</param>
    /// <param name="correlationId">Correlation ID to pass through to the cloud.</param>
    /// <param name="cancellationToken">Propagated from the HTTP request lifetime.</param>
    /// <returns>A <see cref="CloudRenderResponse"/> containing the Base64 document.</returns>
    /// <exception cref="PrintException">
    /// Thrown with <see cref="ErrorCode.CloudRenderFailed"/> on network errors or non-2xx responses.
    /// </exception>
    public async Task<CloudRenderResponse> RenderAsync(
        PrintRequest      request,
        string            format,
        Guid              correlationId,
        CancellationToken cancellationToken = default)
    {
        var opts   = _cloudOptions.CurrentValue;
        var client = _httpClientFactory.CreateClient(HttpClientName);
        var apiKey = GetApiKey(opts);

        // Use format from request if supplied, otherwise fall back to the parameter
        var effectiveFormat = !string.IsNullOrWhiteSpace(request.Format) ? request.Format : format;

        var payload = new
        {
            templateName  = request.TemplateName,
            variables     = request.Variables,
            branding      = request.Branding,
            format        = effectiveFormat,
            correlationId
        };

        _logger.LogInformation(
            "Cloud render request — CorrelationId={CorrelationId} Template={Template} Format={Format} Url={Url}",
            correlationId, request.TemplateName, effectiveFormat, opts.BaseUrl);

        // Use HttpRequestMessage so the API key header is set per-request and never stale.
        var json    = JsonSerializer.Serialize(payload, JsonOptions);
        var reqMsg  = new HttpRequestMessage(HttpMethod.Post, "/api/badges/render")
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };
        if (!string.IsNullOrWhiteSpace(apiKey))
            reqMsg.Headers.TryAddWithoutValidation("X-Api-Key", apiKey);

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(reqMsg, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw PrintException.CloudRenderFailed($"HTTP request to cloud API failed: {ex.Message}", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            throw PrintException.CloudRenderFailed(
                $"Cloud API returned HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
        }

        CloudRenderResponse? result;
        try
        {
            result = await response.Content.ReadFromJsonAsync<CloudRenderResponse>(
                JsonOptions, cancellationToken);
        }
        catch (Exception ex)
        {
            throw PrintException.CloudRenderFailed("Failed to deserialize cloud API response.", ex);
        }

        if (result is null)
            throw PrintException.CloudRenderFailed("Cloud API returned an empty response body.");

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
        var opts   = _cloudOptions.CurrentValue;
        var client = _httpClientFactory.CreateClient(HttpClientName);
        var apiKey = GetApiKey(opts);

        // Use HttpRequestMessage so the API key header is set per-request and never stale.
        var reqMsg = new HttpRequestMessage(HttpMethod.Get, "/api/badges/templates");
        if (!string.IsNullOrWhiteSpace(apiKey))
            reqMsg.Headers.TryAddWithoutValidation("X-Api-Key", apiKey);

        var response = await client.SendAsync(reqMsg, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<IEnumerable<string>>(
            JsonOptions, cancellationToken);
        return result ?? [];
    }
}

/// <summary>
/// Shape of the JSON response returned by <c>POST /api/badges/render</c> on the cloud API.
/// </summary>
public sealed record CloudRenderResponse
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
