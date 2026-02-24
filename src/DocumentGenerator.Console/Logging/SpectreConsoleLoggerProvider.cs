using Microsoft.Extensions.Logging;

namespace DocumentGenerator.Console.Logging;

/// <summary>
/// <see cref="ILoggerProvider"/> that creates <see cref="SpectreConsoleLogger"/> instances,
/// all sharing the same <see cref="LogBuffer"/> singleton so the TUI can render them.
/// Register via <c>logging.AddSpectreConsole(buffer)</c>.
/// </summary>
[ProviderAlias("SpectreConsole")]
internal sealed class SpectreConsoleLoggerProvider(LogLevel minimumLevel, LogBuffer buffer)
    : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) =>
        new SpectreConsoleLogger(categoryName, minimumLevel, buffer);

    public void Dispose() { }
}

/// <summary>
/// Extension methods to register the Spectre.Console logger with the DI logging pipeline.
/// </summary>
internal static class SpectreConsoleLoggerExtensions
{
    /// <summary>
    /// Replaces all existing logging providers with <see cref="SpectreConsoleLoggerProvider"/>,
    /// routing log output to a shared <see cref="LogBuffer"/> for TUI rendering.
    /// </summary>
    /// <param name="builder">The logging builder to configure.</param>
    /// <param name="buffer">The shared buffer that receives log entries.</param>
    /// <param name="minimumLevel">The minimum log level to capture. Defaults to <see cref="LogLevel.Debug"/>.</param>
    /// <returns>The same <paramref name="builder"/> for chaining.</returns>
    public static ILoggingBuilder AddSpectreConsole(
        this ILoggingBuilder builder,
        LogBuffer buffer,
        LogLevel minimumLevel = LogLevel.Debug)
    {
        builder.ClearProviders();
        builder.AddProvider(new SpectreConsoleLoggerProvider(minimumLevel, buffer));
        return builder;
    }
}
