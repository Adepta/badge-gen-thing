using DocumentGenerator.TestProducer.Configuration;

namespace DocumentGenerator.TestProducer.Infrastructure;

/// <summary>
/// Static helpers for service-mode detection and host configuration.
/// </summary>
internal static class HostingHelpers
{
    /// <summary>
    /// Detects whether the process was launched by a service manager.
    /// <list type="bullet">
    ///   <item>systemd — sets the <c>INVOCATION_ID</c> environment variable.</item>
    ///   <item>Windows SCM — process runs in session 0 with no interactive desktop.</item>
    ///   <item>Console — everything else.</item>
    /// </list>
    /// </summary>
    public static ServiceMode DetectServiceMode()
    {
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("INVOCATION_ID")))
            return ServiceMode.Systemd;

        if (OperatingSystem.IsWindows() && !Environment.UserInteractive)
            return ServiceMode.WindowsService;

        return ServiceMode.Console;
    }
}
