using DocumentGenerator.Api.Configuration;
using DocumentGenerator.Api.Messaging;
using DocumentGenerator.Api.Models;
using DocumentGenerator.Api.Services;
using DocumentGenerator.Core.Errors;
using DocumentGenerator.Core.Interfaces;
using DocumentGenerator.Core.Models;
using DocumentGenerator.Messaging.Messages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Rebus.Bus;

namespace DocumentGenerator.Api.Controllers;

/// <summary>
/// Provides badge rendering endpoints consumed by the client-side bridge service.
///
/// <para>
/// Two render paths are supported, selected at startup via <c>Kafka:Enabled</c>:
/// </para>
/// <list type="bullet">
///   <item>
///     <term>Kafka path (recommended for production)</term>
///     <description>
///       The request is published to <c>render.requests</c>. The Console render service
///       processes it with Chromium and publishes the result to <c>render.results</c>.
///       The API awaits the result for up to <c>Kafka:ResultTimeoutSeconds</c> seconds
///       (default 25) before returning HTTP 504.
///     </description>
///   </item>
///   <item>
///     <term>Inline path (development / standalone)</term>
///     <description>
///       The render runs in-process using the embedded Chromium pool. No Kafka dependency.
///     </description>
///   </item>
/// </list>
/// <para>All endpoints require a valid <c>X-Api-Key</c> header.</para>
/// </summary>
[ApiController]
[Route("api/badges")]
[Authorize]
public sealed class BadgesController : ControllerBase
{
    private readonly IDocumentPipeline          _pipeline;
    private readonly TemplateLocator            _locator;
    private readonly ILogger<BadgesController>  _logger;
    private readonly ApiKafkaOptions?           _kafkaOpts;
    private readonly IBus?                      _bus;
    private readonly PendingRenderStore?        _pendingStore;

    /// <summary>
    /// Initialises a new <see cref="BadgesController"/> with mandatory dependencies.
    /// </summary>
    /// <param name="pipeline">In-process render pipeline (used when Kafka is disabled).</param>
    /// <param name="locator">Resolves template names to <see cref="DocumentTemplate"/> instances.</param>
    /// <param name="logger">Logger for request diagnostics.</param>
    /// <param name="kafkaOpts">
    /// Kafka settings. When <see cref="ApiKafkaOptions.Enabled"/> is <see langword="true"/>
    /// the controller uses the Kafka path instead of the inline pipeline.
    /// </param>
    /// <param name="bus">
    /// Rebus bus for publishing to Kafka. Injected only when Kafka is enabled; otherwise null.
    /// </param>
    /// <param name="pendingStore">
    /// In-process awaiter store. Injected only when Kafka is enabled; otherwise null.
    /// </param>
    public BadgesController(
        IDocumentPipeline         pipeline,
        TemplateLocator           locator,
        ILogger<BadgesController> logger,
        IOptions<ApiKafkaOptions>? kafkaOpts    = null,
        IBus?                      bus          = null,
        PendingRenderStore?        pendingStore = null)
    {
        _pipeline     = pipeline;
        _locator      = locator;
        _logger       = logger;
        _kafkaOpts    = kafkaOpts?.Value;
        _bus          = bus;
        _pendingStore = pendingStore;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // POST /api/badges/render
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Renders a badge from the specified template and attendee data, returning the result
    /// as a Base64-encoded PDF in the response body.
    /// </summary>
    /// <param name="request">Badge render parameters including template name and attendee variables.</param>
    /// <param name="cancellationToken">Propagated from the HTTP request lifetime.</param>
    /// <returns>
    /// <list type="bullet">
    ///   <item>200 OK — <see cref="BadgeRenderResponse"/> with Base64 document on success.</item>
    ///   <item>400 Bad Request — unknown template name.</item>
    ///   <item>401 Unauthorized — missing or invalid <c>X-Api-Key</c>.</item>
    ///   <item>504 Gateway Timeout — Kafka render worker did not respond within the timeout.</item>
    ///   <item>500 Internal Server Error — unexpected rendering failure.</item>
    /// </list>
    /// </returns>
    [HttpPost("render")]
    [EnableRateLimiting("render")]
    [ProducesResponseType(typeof(BadgeRenderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BadgeRenderResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status504GatewayTimeout)]
    [ProducesResponseType(typeof(BadgeRenderResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> RenderAsync(
        [FromBody] BadgeRenderRequest request,
        CancellationToken cancellationToken)
    {
        var correlationId = request.CorrelationId ?? Guid.NewGuid();

        _logger.LogInformation(
            "Badge render request — CorrelationId={CorrelationId} Template={Template} " +
            "Format={Format} KafkaEnabled={KafkaEnabled}",
            correlationId, request.TemplateName, request.Format,
            _kafkaOpts?.Enabled ?? false);

        // Resolve branding overrides if provided
        Branding? branding = null;
        if (request.Branding is { } b)
        {
            branding = new Branding
            {
                CompanyName     = b.CompanyName     ?? string.Empty,
                LogoUrl         = b.LogoUrl,
                PrimaryColour   = b.PrimaryColour,
                SecondaryColour = b.SecondaryColour,
                HeadingFont     = b.HeadingFont,
                BodyFont        = b.BodyFont,
                Custom          = b.Custom
            };
        }

        // Resolve the template — returns 400 if template name is unknown or invalid
        DocumentTemplate template;
        try
        {
            template = _locator.Resolve(request.TemplateName, request.Variables, branding);
        }
        catch (TemplateException ex) when (
            ex.Code is ErrorCode.TemplateNotFound or ErrorCode.TemplateNameInvalid)
        {
            _logger.LogWarning(
                "[{ErrorCode}] Template lookup failed — TemplateName={TemplateName}",
                ex.ToString(), request.TemplateName);
            return BadRequest(BadgeRenderResponse.Fail(correlationId, ex.Message));
        }

        return (_kafkaOpts?.Enabled ?? false) && _bus is not null && _pendingStore is not null
            ? await RenderViaKafkaAsync(request, template, correlationId, cancellationToken)
            : await RenderInlineAsync(request, template, correlationId, cancellationToken);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GET /api/badges/templates
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns a list of all available badge template names hosted on this server.
    /// The bridge or iPad can use this to populate a template selection UI.
    /// </summary>
    /// <returns>200 OK with an array of template name strings.</returns>
    [HttpGet("templates")]
    [ProducesResponseType(typeof(IEnumerable<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult ListTemplates()
    {
        var templates = _locator.ListTemplates();
        return Ok(templates);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Private — Kafka render path
    // ─────────────────────────────────────────────────────────────────────────

    private async Task<IActionResult> RenderViaKafkaAsync(
        BadgeRenderRequest request,
        DocumentTemplate   template,
        Guid               correlationId,
        CancellationToken  cancellationToken)
    {
        var timeout = TimeSpan.FromSeconds(_kafkaOpts!.ResultTimeoutSeconds);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        var resultTask = _pendingStore!.RegisterAsync(correlationId, timeoutCts.Token);

        var kafkaRequest = new DocumentRenderRequest
        {
            CorrelationId   = correlationId,
            DeviceId        = "api",
            Template        = template,
            ReturnPdfInline = true
        };

        try
        {
            await _bus!.Send(kafkaRequest);

            _logger.LogInformation(
                "Render request published to Kafka — CorrelationId={CorrelationId} " +
                "Template={Template} Timeout={TimeoutS}s",
                correlationId, request.TemplateName, _kafkaOpts.ResultTimeoutSeconds);
        }
        catch (Exception ex)
        {
            var brokerEx = BrokerException.PublishFailed(correlationId, ex);
            _logger.LogError(ex,
                "[{ErrorCode}] Failed to publish render request to Kafka — CorrelationId={CorrelationId}",
                brokerEx.ToString(), correlationId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                BadgeRenderResponse.Fail(correlationId, brokerEx.Message));
        }

        DocumentRenderResult result;
        try
        {
            result = await resultTask;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            var timeoutEx = BrokerException.ResultTimeout(correlationId, _kafkaOpts.ResultTimeoutSeconds);
            _logger.LogWarning(
                "[{ErrorCode}] Render result timed out — CorrelationId={CorrelationId}",
                timeoutEx.ToString(), correlationId);
            return StatusCode(StatusCodes.Status504GatewayTimeout,
                BadgeRenderResponse.Fail(correlationId, timeoutEx.Message));
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning(
                "Render cancelled by client — CorrelationId={CorrelationId}", correlationId);
            return StatusCode(StatusCodes.Status499ClientClosedRequest);
        }

        if (!result.Success)
        {
            _logger.LogError(
                "[{ErrorCode}] Render failed (reported by Console) — CorrelationId={CorrelationId} Error={Error}",
                result.ErrorCode, correlationId, result.ErrorMessage);
            return StatusCode(StatusCodes.Status500InternalServerError,
                BadgeRenderResponse.Fail(correlationId, result.ErrorMessage ?? "Render failed.", result.ErrorCode));
        }

        var pdfBytes = Convert.FromBase64String(result.PdfBase64!);

        _logger.LogInformation(
            "Badge rendered via Kafka — CorrelationId={CorrelationId} " +
            "DocumentType={DocumentType} Bytes={Bytes} ElapsedMs={ElapsedMs}",
            correlationId, result.DocumentType, pdfBytes.Length,
            (int)result.ElapsedTime.TotalMilliseconds);

        return Ok(BadgeRenderResponse.Ok(
            correlationId,
            correlationId,          // jobId — use correlationId as job identifier
            pdfBytes,
            "application/pdf",
            result.DocumentType,
            result.ElapsedTime));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Private — inline render path (no Kafka)
    // ─────────────────────────────────────────────────────────────────────────

    private async Task<IActionResult> RenderInlineAsync(
        BadgeRenderRequest request,
        DocumentTemplate   template,
        Guid               correlationId,
        CancellationToken  cancellationToken)
    {
        var renderRequest = new RenderRequest { Template = template };

        try
        {
            var result = await _pipeline.ExecuteAsync(renderRequest, cancellationToken);

            var mimeType = request.Format.Equals("Png", StringComparison.OrdinalIgnoreCase)
                ? "image/png"
                : "application/pdf";

            _logger.LogInformation(
                "Badge rendered inline — CorrelationId={CorrelationId} " +
                "JobId={JobId} ElapsedMs={ElapsedMs}",
                correlationId, result.JobId, result.ElapsedTime.TotalMilliseconds);

            return Ok(BadgeRenderResponse.Ok(
                correlationId,
                result.JobId,
                result.PdfBytes,
                mimeType,
                result.DocumentType,
                result.ElapsedTime));
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning(
                "Badge render cancelled — CorrelationId={CorrelationId}", correlationId);
            return StatusCode(StatusCodes.Status499ClientClosedRequest);
        }
        catch (DocumentGeneratorException ex)
        {
            _logger.LogError(ex,
                "[{ErrorCode}] Badge render failed — CorrelationId={CorrelationId}",
                ex.Code, correlationId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                BadgeRenderResponse.Fail(correlationId, ex.Message, ex.Code.ToString()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Badge render failed — CorrelationId={CorrelationId}", correlationId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                BadgeRenderResponse.Fail(correlationId, "An internal rendering error occurred."));
        }
    }
}
