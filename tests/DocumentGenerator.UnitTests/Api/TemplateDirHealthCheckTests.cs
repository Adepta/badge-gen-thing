using DocumentGenerator.Api.HealthChecks;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Xunit;

namespace DocumentGenerator.UnitTests.Api;

/// <summary>
/// Unit tests for <see cref="TemplateDirHealthCheck"/>.
/// Uses a real temp directory — no mocks required.
/// </summary>
public sealed class TemplateDirHealthCheckTests : IDisposable
{
    private readonly string _tempDir;

    public TemplateDirHealthCheckTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"health_tpl_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best-effort */ }
    }

    // ── Healthy ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task CheckHealthAsync_DirectoryExistsWithHtmlFiles_ReturnsHealthy()
    {
        File.WriteAllText(Path.Combine(_tempDir, "badge.html"), "<p/>");
        var sut = BuildSut(_tempDir);

        var result = await sut.CheckHealthAsync(MakeContext());

        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task CheckHealthAsync_Healthy_DescriptionContainsCount()
    {
        File.WriteAllText(Path.Combine(_tempDir, "badge.html"), "<p/>");
        var sut = BuildSut(_tempDir);

        var result = await sut.CheckHealthAsync(MakeContext());

        result.Description.Should().Contain("1");
    }

    // ── Degraded ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task CheckHealthAsync_DirectoryEmptyNoHtmlFiles_ReturnsDegraded()
    {
        // Directory exists but has no .html files
        var sut = BuildSut(_tempDir);

        var result = await sut.CheckHealthAsync(MakeContext());

        result.Status.Should().Be(HealthStatus.Degraded);
    }

    [Fact]
    public async Task CheckHealthAsync_Degraded_DescriptionMentionsEmpty()
    {
        var sut = BuildSut(_tempDir);

        var result = await sut.CheckHealthAsync(MakeContext());

        result.Description.Should().ContainAny("empty", "no .html");
    }

    // ── Unhealthy ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task CheckHealthAsync_DirectoryDoesNotExist_ReturnsUnhealthy()
    {
        var sut = BuildSut("/nonexistent-dir-abc-xyz");

        var result = await sut.CheckHealthAsync(MakeContext());

        result.Status.Should().Be(HealthStatus.Unhealthy);
    }

    [Fact]
    public async Task CheckHealthAsync_Unhealthy_DescriptionMentionsMissingDir()
    {
        var sut = BuildSut("/nonexistent-dir-abc-xyz");

        var result = await sut.CheckHealthAsync(MakeContext());

        result.Description.Should().Contain("does not exist");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static TemplateDirHealthCheck BuildSut(string templatesPath)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DocumentGenerator:TemplatesPath"] = templatesPath
            })
            .Build();

        return new TemplateDirHealthCheck(config);
    }

    private static HealthCheckContext MakeContext() =>
        new()
        {
            Registration = new HealthCheckRegistration(
                "templates", _ => null!, HealthStatus.Unhealthy, [])
        };
}
