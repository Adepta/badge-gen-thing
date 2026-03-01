using DocumentGenerator.Api.Services;
using DocumentGenerator.Core.Interfaces;
using DocumentGenerator.Core.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DocumentGenerator.IntegrationTests.Api;

/// <summary>
/// <see cref="WebApplicationFactory{TEntryPoint}"/> for the <c>DocumentGenerator.Api</c>.
/// Replaces <see cref="IDocumentPipeline"/> with a controllable mock and wires
/// a temp-dir-backed <see cref="TemplateLocator"/> so no real Chromium is needed.
/// </summary>
public sealed class ApiWebApplicationFactory : WebApplicationFactory<DocumentGenerator.Api.Program>
{
    /// <summary>Fake PDF bytes returned by the mocked pipeline on success.</summary>
    public static readonly byte[] FakePdfBytes = [0x25, 0x50, 0x44, 0x46]; // %PDF

    /// <summary>The pipeline mock — tests can set up expectations on this.</summary>
    public Mock<IDocumentPipeline> PipelineMock { get; } = new();

    /// <summary>Temp directory containing stub HTML templates for the locator.</summary>
    public string TemplatesDir { get; } =
        Path.Combine(Path.GetTempPath(), $"api_int_{Guid.NewGuid():N}");

    /// <summary>The API key written to test config — used in all authenticated test requests.</summary>
    public const string TestApiKey = "integration-test-key";

    public ApiWebApplicationFactory()
    {
        Directory.CreateDirectory(TemplatesDir);
        File.WriteAllText(Path.Combine(TemplatesDir, "badge-pulse-a6.html"),     "<p>{{variables.firstName}}</p>");
        File.WriteAllText(Path.Combine(TemplatesDir, "badge-pulse-a6.css"),      "body{}");
        File.WriteAllText(Path.Combine(TemplatesDir, "badge-executive-cc.html"), "<p>exec</p>");

        PipelineMock
            .Setup(p => p.ExecuteAsync(It.IsAny<RenderRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RenderResult.Success(Guid.NewGuid(), FakePdfBytes,
                TimeSpan.FromMilliseconds(50), "badge"));
    }

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Inject test settings via UseSetting — no extra NuGet packages required.
        builder.UseSetting("ApiAuth:ApiKey",                  TestApiKey);
        builder.UseSetting("DocumentGenerator:TemplatesPath", TemplatesDir);
        // Disable Kafka for integration tests — inline pipeline mock is used instead.
        builder.UseSetting("Kafka:Enabled", "false");
        // Raise rate-limit ceiling so integration tests (all from 127.0.0.1) do not hit 429.
        builder.UseSetting("RateLimit:PermitLimit", "10000");

        builder.ConfigureServices(services =>
        {
            // Replace pipeline with mock.
            services.RemoveAll<IDocumentPipeline>();
            services.AddTransient(_ => PipelineMock.Object);

            // Replace TemplateLocator — resolve IConfiguration from the host's DI
            // (which already has the settings injected via UseSetting above) so we
            // don't need Microsoft.Extensions.Configuration.Memory or .EnvironmentVariables.
            services.RemoveAll<TemplateLocator>();
            services.AddSingleton<TemplateLocator>(sp =>
            {
                var cfg = sp.GetRequiredService<IConfiguration>();
                return new TemplateLocator(cfg, NullLogger<TemplateLocator>.Instance);
            });
        });
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            try { Directory.Delete(TemplatesDir, recursive: true); } catch { /* best-effort */ }
    }
}
