using DocumentGenerator.Api.Models;
using Shouldly;
using Xunit;

namespace DocumentGenerator.UnitTests.Api;

/// <summary>
/// Unit tests for <see cref="BadgeRenderResponse"/> factory methods.
/// All tests are purely in-memory — no I/O, no HTTP, no Chromium.
/// </summary>
public sealed class BadgeRenderResponseTests
{
    private static readonly byte[] FakeBytes = [0x25, 0x50, 0x44, 0x46]; // %PDF

    // ── Ok factory ───────────────────────────────────────────────────────────

    [Fact]
    public void Ok_SetsSuccessTrue()
    {
        var response = BadgeRenderResponse.Ok(Guid.NewGuid(), Guid.NewGuid(), FakeBytes, "application/pdf", "badge", TimeSpan.Zero);
        response.Success.ShouldBeTrue();
    }

    [Fact]
    public void Ok_SetsDocumentBase64AsBase64OfBytes()
    {
        var response = BadgeRenderResponse.Ok(Guid.NewGuid(), Guid.NewGuid(), FakeBytes, "application/pdf", "badge", TimeSpan.Zero);
        response.DocumentBase64.ShouldBe(Convert.ToBase64String(FakeBytes));
    }

    [Fact]
    public void Ok_EchoesCorrelationId()
    {
        var correlationId = Guid.NewGuid();
        var response      = BadgeRenderResponse.Ok(correlationId, Guid.NewGuid(), FakeBytes, "application/pdf", "badge", TimeSpan.Zero);
        response.CorrelationId.ShouldBe(correlationId);
    }

    [Fact]
    public void Ok_EchoesJobId()
    {
        var jobId    = Guid.NewGuid();
        var response = BadgeRenderResponse.Ok(Guid.NewGuid(), jobId, FakeBytes, "application/pdf", "badge", TimeSpan.Zero);
        response.JobId.ShouldBe(jobId);
    }

    [Fact]
    public void Ok_SetsMimeType()
    {
        var response = BadgeRenderResponse.Ok(Guid.NewGuid(), Guid.NewGuid(), FakeBytes, "image/png", "badge", TimeSpan.Zero);
        response.MimeType.ShouldBe("image/png");
    }

    [Fact]
    public void Ok_SetsDocumentType()
    {
        var response = BadgeRenderResponse.Ok(Guid.NewGuid(), Guid.NewGuid(), FakeBytes, "application/pdf", "invoice", TimeSpan.Zero);
        response.DocumentType.ShouldBe("invoice");
    }

    [Fact]
    public void Ok_SetsElapsedTime()
    {
        var elapsed  = TimeSpan.FromMilliseconds(420);
        var response = BadgeRenderResponse.Ok(Guid.NewGuid(), Guid.NewGuid(), FakeBytes, "application/pdf", "badge", elapsed);
        response.ElapsedTime.ShouldBe(elapsed);
    }

    [Fact]
    public void Ok_ErrorIsNull()
    {
        var response = BadgeRenderResponse.Ok(Guid.NewGuid(), Guid.NewGuid(), FakeBytes, "application/pdf", "badge", TimeSpan.Zero);
        response.Error.ShouldBeNull();
    }

    [Fact]
    public void Ok_CompletedAtIsApproximatelyNow()
    {
        var before   = DateTimeOffset.UtcNow.AddSeconds(-1);
        var response = BadgeRenderResponse.Ok(Guid.NewGuid(), Guid.NewGuid(), FakeBytes, "application/pdf", "badge", TimeSpan.Zero);
        var after    = DateTimeOffset.UtcNow.AddSeconds(1);

        response.CompletedAt.ShouldBeGreaterThan(before);
        response.CompletedAt.ShouldBeLessThan(after);
    }

    // ── Fail factory ─────────────────────────────────────────────────────────

    [Fact]
    public void Fail_SetsSuccessFalse()
    {
        var response = BadgeRenderResponse.Fail(Guid.NewGuid(), "something broke");
        response.Success.ShouldBeFalse();
    }

    [Fact]
    public void Fail_SetsErrorMessage()
    {
        const string error = "Template not found";
        var response       = BadgeRenderResponse.Fail(Guid.NewGuid(), error);
        response.Error.ShouldBe(error);
    }

    [Fact]
    public void Fail_EchoesCorrelationId()
    {
        var correlationId = Guid.NewGuid();
        var response      = BadgeRenderResponse.Fail(correlationId, "err");
        response.CorrelationId.ShouldBe(correlationId);
    }

    [Fact]
    public void Fail_DocumentBase64IsNull()
    {
        var response = BadgeRenderResponse.Fail(Guid.NewGuid(), "err");
        response.DocumentBase64.ShouldBeNull();
    }

    [Fact]
    public void Fail_JobIdIsEmpty()
    {
        var response = BadgeRenderResponse.Fail(Guid.NewGuid(), "err");
        response.JobId.ShouldBe(Guid.Empty);
    }

    // ── ErrorCode ─────────────────────────────────────────────────────────────

    [Fact]
    public void Fail_WithErrorCode_SetsErrorCode()
    {
        var response = BadgeRenderResponse.Fail(Guid.NewGuid(), "template not found", "DG1001");
        response.ErrorCode.ShouldBe("DG1001");
    }

    [Fact]
    public void Fail_WithoutErrorCode_ErrorCodeIsNull()
    {
        var response = BadgeRenderResponse.Fail(Guid.NewGuid(), "err");
        response.ErrorCode.ShouldBeNull();
    }

    [Fact]
    public void Ok_ErrorCodeIsNull()
    {
        var response = BadgeRenderResponse.Ok(Guid.NewGuid(), Guid.NewGuid(), FakeBytes, "application/pdf", "badge", TimeSpan.Zero);
        response.ErrorCode.ShouldBeNull();
    }
}
