using DocumentGenerator.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace DocumentGenerator.Templating.Extensions;

/// <summary>
/// Extension methods for registering <c>DocumentGenerator.Templating</c> services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Handlebars template engine as a singleton <see cref="ITemplateEngine"/>.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <returns>The same <paramref name="services"/> for chaining.</returns>
    public static IServiceCollection AddTemplating(this IServiceCollection services)
    {
        services.AddSingleton<ITemplateEngine, HandlebarsTemplateEngine>();
        return services;
    }
}
