using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Extensions.Hosting;

namespace DocumentGenerator.Bridge.Printing;

/// <summary>
/// Selects and registers the correct <see cref="IPrinterAdapter"/> implementation
/// based on the current environment and operating system.
/// </summary>
/// <remarks>
/// <list type="table">
///   <listheader><term>Environment / OS</term><description>Adapter</description></listheader>
///   <item><term>Development (any OS)</term><description><see cref="LocalFileAdapter"/> — writes to <c>Generated/</c></description></item>
///   <item><term>Production · Windows</term><description><see cref="WindowsPrinterAdapter"/></description></item>
///   <item><term>Production · Linux / macOS</term><description><see cref="CupsPrinterAdapter"/></description></item>
/// </list>
/// </remarks>
public static class PrinterAdapterFactory
{
    /// <summary>
    /// Registers the appropriate <see cref="IPrinterAdapter"/> as a singleton in the DI container.
    /// In the <c>Development</c> environment the <see cref="LocalFileAdapter"/> is always used
    /// regardless of OS so that no physical printer is required.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="environment">The hosting environment; used to detect Development mode.</param>
    /// <returns>The same <paramref name="services"/> for chaining.</returns>
    public static IServiceCollection AddPrinterAdapter(
        this IServiceCollection services,
        IHostEnvironment environment)
    {
        if (environment.IsDevelopment())
        {
            services.AddSingleton<IPrinterAdapter, LocalFileAdapter>();
            return services;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            RegisterWindows(services);
        else
#pragma warning disable CA1416 // RegisterCups is guarded by the runtime OS check above
            RegisterCups(services);
#pragma warning restore CA1416

        return services;
    }

    [SupportedOSPlatform("windows")]
    private static void RegisterWindows(IServiceCollection services) =>
        services.AddSingleton<IPrinterAdapter, WindowsPrinterAdapter>();

    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("macos")]
    private static void RegisterCups(IServiceCollection services) =>
        services.AddSingleton<IPrinterAdapter, CupsPrinterAdapter>();
}
