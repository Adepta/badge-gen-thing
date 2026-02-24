using DocumentGenerator.Core.Configuration;
using DocumentGenerator.Core.Interfaces;
using DocumentGenerator.Pdf.Pool;
using Microsoft.Extensions.DependencyInjection;
using PuppeteerSharp;

namespace DocumentGenerator.Pdf.Extensions;

/// <summary>
/// Extension methods for registering <c>DocumentGenerator.Pdf</c> services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the <see cref="Pool.ChromiumBrowserPool"/>, <see cref="PuppeteerDocumentRenderer"/>,
    /// and <see cref="DocumentPipeline"/> with the DI container.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <returns>The same <paramref name="services"/> for chaining.</returns>
    public static IServiceCollection AddPdfRendering(this IServiceCollection services)
    {
        // Pool is a singleton — one pool shared across all render requests
        services.AddSingleton<IBrowserPool<IBrowser>, ChromiumBrowserPool>();
        services.AddSingleton<IDocumentRenderer, PuppeteerDocumentRenderer>();
        services.AddTransient<IDocumentPipeline, DocumentPipeline>();

        return services;
    }

    /// <summary>
    /// Registers PDF rendering services and applies a configuration delegate for pool options.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="configurePool">Delegate to configure <see cref="BrowserPoolOptions"/>.</param>
    /// <returns>The same <paramref name="services"/> for chaining.</returns>
    public static IServiceCollection AddPdfRendering(
        this IServiceCollection services,
        Action<BrowserPoolOptions> configurePool)
    {
        services.Configure(configurePool);
        return services.AddPdfRendering();
    }
}
