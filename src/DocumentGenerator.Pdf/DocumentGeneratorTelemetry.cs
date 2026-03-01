using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace DocumentGenerator.Pdf;

/// <summary>
/// Central home for the shared <see cref="System.Diagnostics.ActivitySource"/> and
/// <see cref="System.Diagnostics.Metrics.Meter"/> used across the
/// <c>DocumentGenerator.Pdf</c> library.
///
/// Consumers (Api, Console) register these with OpenTelemetry via:
/// <code>
///   .AddSource("DocumentGenerator.*")
///   .AddMeter("DocumentGenerator.*")
/// </code>
/// </summary>
public static class DocumentGeneratorTelemetry
{
    /// <summary>Name shared by the <see cref="Source"/> and <see cref="Meter"/>.</summary>
    public const string Name = "DocumentGenerator.Pdf";

    /// <summary>
    /// <see cref="System.Diagnostics.ActivitySource"/> for distributed tracing spans
    /// emitted by the render pipeline.
    /// </summary>
    public static readonly ActivitySource Source = new(Name);

    /// <summary>
    /// <see cref="System.Diagnostics.Metrics.Meter"/> for custom metrics emitted
    /// by the render pipeline (e.g. render duration histogram).
    /// </summary>
    public static readonly Meter Meter = new(Name);

    /// <summary>
    /// Histogram recording end-to-end render durations in milliseconds.
    /// Dimensions: <c>document_type</c>, <c>success</c>.
    /// </summary>
    public static readonly Histogram<double> RenderDuration =
        Meter.CreateHistogram<double>(
            name:        "documentgenerator.render.duration_ms",
            unit:        "ms",
            description: "End-to-end render pipeline duration in milliseconds.");

    /// <summary>
    /// Counter tracking total number of completed render operations.
    /// Dimensions: <c>document_type</c>, <c>success</c>.
    /// </summary>
    public static readonly Counter<long> RenderCount =
        Meter.CreateCounter<long>(
            name:        "documentgenerator.render.count",
            unit:        "{renders}",
            description: "Total number of render pipeline executions.");
}
