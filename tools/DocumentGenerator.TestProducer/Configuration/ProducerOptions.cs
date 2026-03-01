namespace DocumentGenerator.TestProducer.Configuration;

/// <summary>
/// Strongly-typed options bound from the "Producer" section of appsettings.json.
/// All values can be overridden via environment variables using the double-underscore
/// delimiter, e.g.  PRODUCER__BatchSize=3
/// </summary>
public sealed class ProducerOptions
{
    public const string SectionName = "Producer";

    /// <summary>
    /// How long the worker sleeps between job batches.
    /// Mapped from the ISO 8601 duration string in appsettings, e.g. "00:00:30".
    /// </summary>
    public TimeSpan ScheduleInterval { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Number of render requests sent per batch cycle.</summary>
    public int BatchSize { get; init; } = 1;

    /// <summary>
    /// How long to wait for a <see cref="DocumentGenerator.Messaging.Messages.DocumentRenderResult"/>
    /// reply before declaring the job timed out.
    /// </summary>
    public int ResultTimeoutSeconds { get; init; } = 120;

    /// <summary>Directory where received inline PDFs are written.</summary>
    public string OutputDirectory { get; init; } = "Generated";

    /// <summary>
    /// When <c>true</c> the render service Base64-encodes the PDF and returns it
    /// directly in the Kafka reply.  When <c>false</c> the service writes the PDF
    /// to its own disk and returns the path.
    /// </summary>
    public bool ReturnPdfInline { get; init; } = true;

    /// <summary>
    /// Explicit list of template JSON file names to cycle through.
    /// When empty the worker discovers all *.json files in
    /// <see cref="TemplatesDirectory"/> automatically.
    /// </summary>
    public IReadOnlyList<string> Templates { get; init; } = [];

    /// <summary>
    /// Path to the templates directory.  Resolved relative to the executable
    /// when not absolute.  Auto-discovered by walking up the directory tree
    /// when left blank.
    /// </summary>
    public string TemplatesDirectory { get; init; } = string.Empty;
}

/// <summary>
/// OpenTelemetry pipeline configuration, bound from the "OpenTelemetry" section.
/// </summary>
public sealed class OtelOptions
{
    public const string SectionName = "OpenTelemetry";

    /// <summary>Enable or disable the entire OTel pipeline.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Service name reported to the OTel collector / APM backend.</summary>
    public string ServiceName { get; init; } = "DocumentGenerator.TestProducer";

    /// <summary>Service version reported to the OTel collector.</summary>
    public string ServiceVersion { get; init; } = "1.0.0";

    /// <summary>
    /// OTLP gRPC endpoint, e.g. "http://localhost:4317".
    /// Set to null or empty string to skip OTLP export.
    /// </summary>
    public string? OtlpEndpoint { get; init; } = "http://localhost:4317";

    /// <summary>Echo telemetry to stdout (useful during development).</summary>
    public bool ConsoleExporterEnabled { get; init; } = false;
}

/// <summary>
/// Selects how the host registers itself with the OS service manager.
/// </summary>
public enum ServiceMode
{
    /// <summary>Detect at startup: systemd → Windows Service → Console.</summary>
    Auto,

    /// <summary>Plain interactive console process.</summary>
    Console,

    /// <summary>Registered and managed by the Windows Service Control Manager.</summary>
    WindowsService,

    /// <summary>Registered and managed by Linux systemd.</summary>
    Systemd
}
