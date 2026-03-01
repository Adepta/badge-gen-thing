using System.ComponentModel.DataAnnotations;
using DocumentGenerator.Api.Configuration;
using FluentAssertions;
using Xunit;

namespace DocumentGenerator.UnitTests.Api;

/// <summary>
/// Unit tests for <see cref="RateLimitOptions"/> DataAnnotations validation.
/// Verifies that invalid configurations are rejected at startup.
/// </summary>
public sealed class RateLimitOptionsTests
{
    // ── Valid defaults ────────────────────────────────────────────────────────

    [Fact]
    public void DefaultOptions_AreValid()
    {
        var opts = new RateLimitOptions();

        Validate(opts).Should().BeEmpty();
    }

    // ── PermitLimit ───────────────────────────────────────────────────────────

    [Fact]
    public void PermitLimit_Zero_IsInvalid()
    {
        var opts = new RateLimitOptions { PermitLimit = 0 };

        Validate(opts).Should().NotBeEmpty();
    }

    [Fact]
    public void PermitLimit_Negative_IsInvalid()
    {
        var opts = new RateLimitOptions { PermitLimit = -1 };

        Validate(opts).Should().NotBeEmpty();
    }

    [Fact]
    public void PermitLimit_One_IsValid()
    {
        var opts = new RateLimitOptions { PermitLimit = 1 };

        Validate(opts).Should().BeEmpty();
    }

    [Fact]
    public void PermitLimit_Large_IsValid()
    {
        var opts = new RateLimitOptions { PermitLimit = 10_000 };

        Validate(opts).Should().BeEmpty();
    }

    // ── Window ────────────────────────────────────────────────────────────────

    [Fact]
    public void Window_ZeroSeconds_IsInvalid()
    {
        var opts = new RateLimitOptions { Window = TimeSpan.Zero };

        Validate(opts).Should().NotBeEmpty();
    }

    [Fact]
    public void Window_OneSecond_IsValid()
    {
        var opts = new RateLimitOptions { Window = TimeSpan.FromSeconds(1) };

        Validate(opts).Should().BeEmpty();
    }

    // ── SegmentsPerWindow ─────────────────────────────────────────────────────

    [Fact]
    public void SegmentsPerWindow_Zero_IsInvalid()
    {
        var opts = new RateLimitOptions { SegmentsPerWindow = 0 };

        Validate(opts).Should().NotBeEmpty();
    }

    [Fact]
    public void SegmentsPerWindow_One_IsValid()
    {
        var opts = new RateLimitOptions { SegmentsPerWindow = 1 };

        Validate(opts).Should().BeEmpty();
    }

    [Fact]
    public void SegmentsPerWindow_OneHundred_IsValid()
    {
        var opts = new RateLimitOptions { SegmentsPerWindow = 100 };

        Validate(opts).Should().BeEmpty();
    }

    [Fact]
    public void SegmentsPerWindow_OneHundredAndOne_IsInvalid()
    {
        var opts = new RateLimitOptions { SegmentsPerWindow = 101 };

        Validate(opts).Should().NotBeEmpty();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ICollection<ValidationResult> Validate(RateLimitOptions opts)
    {
        var ctx     = new ValidationContext(opts);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(opts, ctx, results, validateAllProperties: true);
        return results;
    }
}
