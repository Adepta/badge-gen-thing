using DocumentGenerator.Api.HealthChecks;
using DocumentGenerator.Core.Errors;
using DocumentGenerator.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;
using PuppeteerSharp;
using Xunit;

namespace DocumentGenerator.UnitTests.Api;

/// <summary>
/// Unit tests for <see cref="ChromiumPoolHealthCheck"/>.
/// The browser pool is mocked — no real Chromium is launched.
/// </summary>
public sealed class ChromiumPoolHealthCheckTests
{
    private readonly Mock<IBrowserPool<IBrowser>> _poolMock = new();

    // ── Kafka mode — check is skipped ─────────────────────────────────────────

    [Fact]
    public async Task CheckHealthAsync_KafkaEnabled_ReturnsHealthyWithSkipMessage()
    {
        var sut = BuildSut(kafkaEnabled: true);

        var result = await sut.CheckHealthAsync(MakeContext());

        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Contain("skipped");
    }

    [Fact]
    public async Task CheckHealthAsync_KafkaEnabled_DoesNotAcquireLease()
    {
        var sut = BuildSut(kafkaEnabled: true);

        await sut.CheckHealthAsync(MakeContext());

        _poolMock.Verify(
            p => p.AcquireAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── Inline mode — pool healthy ─────────────────────────────────────────────

    [Fact]
    public async Task CheckHealthAsync_KafkaDisabled_PoolHealthy_ReturnsHealthy()
    {
        var leaseMock = new Mock<IBrowserLease<IBrowser>>();
        leaseMock.Setup(l => l.DisposeAsync()).Returns(ValueTask.CompletedTask);

        _poolMock
            .Setup(p => p.AcquireAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(leaseMock.Object);

        var sut = BuildSut(kafkaEnabled: false);

        var result = await sut.CheckHealthAsync(MakeContext());

        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task CheckHealthAsync_KafkaDisabled_PoolHealthy_ReleasesLease()
    {
        var leaseMock = new Mock<IBrowserLease<IBrowser>>();
        leaseMock.Setup(l => l.DisposeAsync()).Returns(ValueTask.CompletedTask);

        _poolMock
            .Setup(p => p.AcquireAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(leaseMock.Object);

        var sut = BuildSut(kafkaEnabled: false);

        await sut.CheckHealthAsync(MakeContext());

        leaseMock.Verify(l => l.DisposeAsync(), Times.Once);
    }

    // ── Inline mode — pool unhealthy ──────────────────────────────────────────

    [Fact]
    public async Task CheckHealthAsync_KafkaDisabled_PoolThrows_ReturnsUnhealthy()
    {
        _poolMock
            .Setup(p => p.AcquireAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(BrowserPoolException.Disposed());

        var sut = BuildSut(kafkaEnabled: false);

        var result = await sut.CheckHealthAsync(MakeContext());

        result.Status.Should().Be(HealthStatus.Unhealthy);
    }

    [Fact]
    public async Task CheckHealthAsync_KafkaDisabled_PoolThrows_DescriptionMentionsPool()
    {
        _poolMock
            .Setup(p => p.AcquireAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("pool error"));

        var sut = BuildSut(kafkaEnabled: false);

        var result = await sut.CheckHealthAsync(MakeContext());

        result.Description.Should().Contain("unhealthy");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private ChromiumPoolHealthCheck BuildSut(bool kafkaEnabled)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Kafka:Enabled"] = kafkaEnabled.ToString().ToLower()
            })
            .Build();

        return new ChromiumPoolHealthCheck(_poolMock.Object, config);
    }

    private static HealthCheckContext MakeContext() =>
        new()
        {
            Registration = new HealthCheckRegistration(
                "chromium", _ => null!, HealthStatus.Unhealthy, [])
        };
}
