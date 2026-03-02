using DocumentGenerator.Core.Models;
using DocumentGenerator.Messaging.Messages;
using Shouldly;
using Xunit;

namespace DocumentGenerator.UnitTests.Core;

/// <summary>
/// Unit tests for Core models and factory methods.
/// Tests are purely in-memory; no I/O or external dependencies.
/// </summary>
public sealed class ModelTests
{
    // -----------------------------------------------------------------------
    // RenderResult.Success factory
    // -----------------------------------------------------------------------

    [Fact]
    public void RenderResult_Success_SetsJobId()
    {
        var jobId    = Guid.NewGuid();
        var result   = RenderResult.Success(jobId, [0x01], TimeSpan.FromSeconds(1), "badge");

        result.JobId.ShouldBe(jobId);
    }

    [Fact]
    public void RenderResult_Success_SetsPdfBytes()
    {
        byte[] bytes = [0x25, 0x50, 0x44, 0x46];
        var result   = RenderResult.Success(Guid.NewGuid(), bytes, TimeSpan.Zero, "badge");

        result.PdfBytes.ShouldBe(bytes);
    }

    [Fact]
    public void RenderResult_Success_SetsElapsedTime()
    {
        var elapsed = TimeSpan.FromMilliseconds(350);
        var result  = RenderResult.Success(Guid.NewGuid(), [0x01], elapsed, "badge");

        result.ElapsedTime.ShouldBe(elapsed);
    }

    [Fact]
    public void RenderResult_Success_SetsDocumentType()
    {
        var result = RenderResult.Success(Guid.NewGuid(), [0x01], TimeSpan.Zero, "invoice");

        result.DocumentType.ShouldBe("invoice");
    }

    // -----------------------------------------------------------------------
    // DocumentRenderResult.Succeeded factory
    // -----------------------------------------------------------------------

    [Fact]
    public void DocumentRenderResult_Succeeded_SetsSuccessTrue()
    {
        var result = DocumentRenderResult.Succeeded(
            Guid.NewGuid(), "device-1", null, "badge", [0x01], TimeSpan.Zero);

        result.Success.ShouldBeTrue();
    }

    [Fact]
    public void DocumentRenderResult_Succeeded_SetsPdfBase64()
    {
        byte[] bytes  = [0x25, 0x50, 0x44, 0x46];
        var    result = DocumentRenderResult.Succeeded(
            Guid.NewGuid(), "device-1", null, "badge", bytes, TimeSpan.Zero);

        result.PdfBase64.ShouldBe(Convert.ToBase64String(bytes));
    }

    [Fact]
    public void DocumentRenderResult_Succeeded_ErrorMessageIsNull()
    {
        var result = DocumentRenderResult.Succeeded(
            Guid.NewGuid(), "device-1", null, "badge", [0x01], TimeSpan.Zero);

        result.ErrorMessage.ShouldBeNull();
    }

    [Fact]
    public void DocumentRenderResult_Succeeded_EchoesAllIds()
    {
        var correlationId = Guid.NewGuid();
        var result        = DocumentRenderResult.Succeeded(
            correlationId, "iPad-7", "conf-2026", "badge", [0x01], TimeSpan.Zero);

        result.CorrelationId.ShouldBe(correlationId);
        result.DeviceId.ShouldBe("iPad-7");
        result.SessionId.ShouldBe("conf-2026");
    }

    // -----------------------------------------------------------------------
    // DocumentRenderResult.Failed factory
    // -----------------------------------------------------------------------

    [Fact]
    public void DocumentRenderResult_Failed_SetsSuccessFalse()
    {
        var result = DocumentRenderResult.Failed(
            Guid.NewGuid(), "device-1", null, "badge", "Something went wrong");

        result.Success.ShouldBeFalse();
    }

    [Fact]
    public void DocumentRenderResult_Failed_SetsErrorMessage()
    {
        const string error = "Chromium crashed";
        var result = DocumentRenderResult.Failed(
            Guid.NewGuid(), "device-1", null, "badge", error);

        result.ErrorMessage.ShouldBe(error);
    }

    [Fact]
    public void DocumentRenderResult_Failed_PdfBase64IsNull()
    {
        var result = DocumentRenderResult.Failed(
            Guid.NewGuid(), "device-1", null, "badge", "error");

        result.PdfBase64.ShouldBeNull();
    }

    // -----------------------------------------------------------------------
    // RenderRequest defaults
    // -----------------------------------------------------------------------

    [Fact]
    public void RenderRequest_DefaultJobId_IsNewGuid()
    {
        var r1 = new RenderRequest { Template = new DocumentTemplate() };
        var r2 = new RenderRequest { Template = new DocumentTemplate() };

        r1.JobId.ShouldNotBe(Guid.Empty);
        r1.JobId.ShouldNotBe(r2.JobId);
    }

    [Fact]
    public void RenderRequest_CreatedAt_IsApproximatelyNow()
    {
        var before  = DateTimeOffset.UtcNow.AddSeconds(-1);
        var request = new RenderRequest { Template = new DocumentTemplate() };
        var after   = DateTimeOffset.UtcNow.AddSeconds(1);

        request.CreatedAt.ShouldBeGreaterThan(before);
        request.CreatedAt.ShouldBeLessThan(after);
    }

    // -----------------------------------------------------------------------
    // DocumentTemplate defaults
    // -----------------------------------------------------------------------

    [Fact]
    public void DocumentTemplate_DefaultVersion_Is1_0()
    {
        var t = new DocumentTemplate();
        t.Version.ShouldBe("1.0");
    }

    [Fact]
    public void DocumentTemplate_DefaultVariables_IsEmptyDictionary()
    {
        var t = new DocumentTemplate();
        t.Variables.ShouldNotBeNull();
        t.Variables.ShouldBeEmpty();
    }

    [Fact]
    public void DocumentTemplate_DefaultPdf_HasA4Format()
    {
        var t = new DocumentTemplate();
        t.Pdf.Format.ShouldBe("A4");
    }

    // -----------------------------------------------------------------------
    // PdfOptions defaults
    // -----------------------------------------------------------------------

    [Fact]
    public void PdfOptions_Defaults_AreCorrect()
    {
        var o = new PdfOptions();

        o.Format.ShouldBe("A4");
        o.Landscape.ShouldBeFalse();
        o.PrintBackground.ShouldBeTrue();
        o.Scale.ShouldBe(1.0);
        o.Width.ShouldBeNull();
        o.Height.ShouldBeNull();
        o.Margins.ShouldBeNull();
    }

    // -----------------------------------------------------------------------
    // Branding defaults
    // -----------------------------------------------------------------------

    [Fact]
    public void Branding_DefaultCustomDictionary_IsEmpty()
    {
        var b = new Branding();
        b.Custom.ShouldNotBeNull();
        b.Custom.ShouldBeEmpty();
    }

    // -----------------------------------------------------------------------
    // TemplateContent defaults
    // -----------------------------------------------------------------------

    [Fact]
    public void TemplateContent_DefaultPartials_IsEmpty()
    {
        var t = new TemplateContent();
        t.Partials.ShouldNotBeNull();
        t.Partials.ShouldBeEmpty();
    }

    [Fact]
    public void TemplateContent_DefaultCss_IsNull()
    {
        var t = new TemplateContent();
        t.Css.ShouldBeNull();
    }
}
