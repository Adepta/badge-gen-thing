using DocumentGenerator.Core.Interfaces;
using DocumentGenerator.Core.Models;
using DocumentGenerator.Messaging.Configuration;
using DocumentGenerator.Messaging.Handlers;
using DocumentGenerator.Messaging.Messages;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Rebus.Bus;
using Xunit;

namespace DocumentGenerator.UnitTests.Messaging;

/// <summary>
/// Unit tests for <see cref="DocumentRenderRequestHandler"/>.
/// The pipeline, bus, and metrics are all mocked.
/// </summary>
public sealed class DocumentRenderRequestHandlerTests : IDisposable
{
    private readonly Mock<IDocumentPipeline> _pipelineMock = new();
    private readonly Mock<IBus>              _busMock      = new();
    private readonly Mock<IRenderMetrics>    _metricsMock  = new();
    private readonly DocumentRenderRequestHandler _sut;

    // Temp directory used by tests that exercise ReturnPdfInline=false
    private readonly string _tempOutputDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

    private static readonly byte[] FakePdfBytes = [0x25, 0x50, 0x44, 0x46]; // %PDF

    public DocumentRenderRequestHandlerTests()
    {
        var kafkaOptions = Options.Create(new KafkaOptions
        {
            PdfOutputPath = _tempOutputDir
        });

        _sut = new DocumentRenderRequestHandler(
            _pipelineMock.Object,
            _busMock.Object,
            _metricsMock.Object,
            kafkaOptions,
            NullLogger<DocumentRenderRequestHandler>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempOutputDir))
            Directory.Delete(_tempOutputDir, recursive: true);
    }

    // -----------------------------------------------------------------------
    // Success path — ReturnPdfInline = true (default)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Handle_SuccessfulRender_RepliesSuccessResult()
    {
        var message = BuildRequest();
        ArrangeSuccess(FakePdfBytes);

        await _sut.Handle(message);

        _busMock.Verify(
            b => b.Reply(It.Is<DocumentRenderResult>(r => r.Success == true), It.IsAny<IDictionary<string, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_SuccessfulRender_ResultContainsBase64Pdf()
    {
        var message = BuildRequest(returnPdfInline: true);
        ArrangeSuccess(FakePdfBytes);

        DocumentRenderResult? captured = null;
        _busMock
            .Setup(b => b.Reply(It.IsAny<object>(), It.IsAny<IDictionary<string, string>>()))
            .Callback<object, IDictionary<string, string>>((msg, _) => captured = msg as DocumentRenderResult)
            .Returns(Task.CompletedTask);

        await _sut.Handle(message);

        captured.Should().NotBeNull();
        captured!.PdfBase64.Should().Be(Convert.ToBase64String(FakePdfBytes));
    }

    [Fact]
    public async Task Handle_SuccessfulRender_InlineTrue_PdfPathIsNull()
    {
        var message = BuildRequest(returnPdfInline: true);
        ArrangeSuccess(FakePdfBytes);

        DocumentRenderResult? captured = null;
        _busMock
            .Setup(b => b.Reply(It.IsAny<object>(), It.IsAny<IDictionary<string, string>>()))
            .Callback<object, IDictionary<string, string>>((msg, _) => captured = msg as DocumentRenderResult)
            .Returns(Task.CompletedTask);

        await _sut.Handle(message);

        captured!.PdfPath.Should().BeNull();
    }

    [Fact]
    public async Task Handle_SuccessfulRender_ResultEchoesCorrelationId()
    {
        var message = BuildRequest();
        ArrangeSuccess(FakePdfBytes);

        DocumentRenderResult? captured = null;
        _busMock
            .Setup(b => b.Reply(It.IsAny<object>(), It.IsAny<IDictionary<string, string>>()))
            .Callback<object, IDictionary<string, string>>((msg, _) => captured = msg as DocumentRenderResult)
            .Returns(Task.CompletedTask);

        await _sut.Handle(message);

        captured!.CorrelationId.Should().Be(message.CorrelationId);
    }

    [Fact]
    public async Task Handle_SuccessfulRender_ResultEchoesDeviceId()
    {
        var message = BuildRequest(deviceId: "iPad-007");
        ArrangeSuccess(FakePdfBytes);

        DocumentRenderResult? captured = null;
        _busMock
            .Setup(b => b.Reply(It.IsAny<object>(), It.IsAny<IDictionary<string, string>>()))
            .Callback<object, IDictionary<string, string>>((msg, _) => captured = msg as DocumentRenderResult)
            .Returns(Task.CompletedTask);

        await _sut.Handle(message);

        captured!.DeviceId.Should().Be("iPad-007");
    }

    [Fact]
    public async Task Handle_SuccessfulRender_ResultEchoesSessionId()
    {
        var message = BuildRequest(sessionId: "conference-2026");
        ArrangeSuccess(FakePdfBytes);

        DocumentRenderResult? captured = null;
        _busMock
            .Setup(b => b.Reply(It.IsAny<object>(), It.IsAny<IDictionary<string, string>>()))
            .Callback<object, IDictionary<string, string>>((msg, _) => captured = msg as DocumentRenderResult)
            .Returns(Task.CompletedTask);

        await _sut.Handle(message);

        captured!.SessionId.Should().Be("conference-2026");
    }

    [Fact]
    public async Task Handle_SuccessfulRender_RecordsSuccess()
    {
        var message = BuildRequest();
        ArrangeSuccess(FakePdfBytes);

        await _sut.Handle(message);

        _metricsMock.Verify(m => m.RecordSuccess(), Times.Once);
        _metricsMock.Verify(m => m.RecordFailure(), Times.Never);
    }

    [Fact]
    public async Task Handle_SuccessfulRender_PipelineCalledWithCorrectJobId()
    {
        var message = BuildRequest();
        ArrangeSuccess(FakePdfBytes);

        RenderRequest? captured = null;
        _pipelineMock
            .Setup(p => p.ExecuteAsync(It.IsAny<RenderRequest>(), It.IsAny<CancellationToken>()))
            .Callback<RenderRequest, CancellationToken>((req, _) => captured = req)
            .ReturnsAsync(RenderResult.Success(message.CorrelationId, FakePdfBytes, TimeSpan.FromSeconds(1), "test"));

        await _sut.Handle(message);

        captured!.JobId.Should().Be(message.CorrelationId);
    }

    // -----------------------------------------------------------------------
    // Success path — ReturnPdfInline = false (save to disk, return path)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Handle_ReturnPdfInlineFalse_PdfBase64IsNull()
    {
        var message = BuildRequest(returnPdfInline: false);
        ArrangeSuccess(FakePdfBytes);

        DocumentRenderResult? captured = null;
        _busMock
            .Setup(b => b.Reply(It.IsAny<object>(), It.IsAny<IDictionary<string, string>>()))
            .Callback<object, IDictionary<string, string>>((msg, _) => captured = msg as DocumentRenderResult)
            .Returns(Task.CompletedTask);

        await _sut.Handle(message);

        captured!.PdfBase64.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ReturnPdfInlineFalse_PdfPathIsPopulated()
    {
        var message = BuildRequest(returnPdfInline: false);
        ArrangeSuccess(FakePdfBytes);

        DocumentRenderResult? captured = null;
        _busMock
            .Setup(b => b.Reply(It.IsAny<object>(), It.IsAny<IDictionary<string, string>>()))
            .Callback<object, IDictionary<string, string>>((msg, _) => captured = msg as DocumentRenderResult)
            .Returns(Task.CompletedTask);

        await _sut.Handle(message);

        captured!.PdfPath.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Handle_ReturnPdfInlineFalse_PdfPathPointsToWrittenFile()
    {
        var message = BuildRequest(returnPdfInline: false);
        ArrangeSuccess(FakePdfBytes);

        DocumentRenderResult? captured = null;
        _busMock
            .Setup(b => b.Reply(It.IsAny<object>(), It.IsAny<IDictionary<string, string>>()))
            .Callback<object, IDictionary<string, string>>((msg, _) => captured = msg as DocumentRenderResult)
            .Returns(Task.CompletedTask);

        await _sut.Handle(message);

        File.Exists(captured!.PdfPath).Should().BeTrue();
        (await File.ReadAllBytesAsync(captured.PdfPath!)).Should().Equal(FakePdfBytes);
    }

    [Fact]
    public async Task Handle_ReturnPdfInlineFalse_OutputFilenameContainsDocumentTypeAndCorrelationId()
    {
        var message = BuildRequest(returnPdfInline: false, documentType: "badge");
        ArrangeSuccess(FakePdfBytes);

        DocumentRenderResult? captured = null;
        _busMock
            .Setup(b => b.Reply(It.IsAny<object>(), It.IsAny<IDictionary<string, string>>()))
            .Callback<object, IDictionary<string, string>>((msg, _) => captured = msg as DocumentRenderResult)
            .Returns(Task.CompletedTask);

        await _sut.Handle(message);

        var fileName = Path.GetFileName(captured!.PdfPath);
        fileName.Should().StartWith("badge_");
        fileName.Should().Contain(message.CorrelationId.ToString("N"));
        fileName.Should().EndWith(".pdf");
    }

    [Fact]
    public async Task Handle_ReturnPdfInlineFalse_StillRepliesSuccessTrue()
    {
        var message = BuildRequest(returnPdfInline: false);
        ArrangeSuccess(FakePdfBytes);

        await _sut.Handle(message);

        _busMock.Verify(
            b => b.Reply(It.Is<DocumentRenderResult>(r => r.Success == true), It.IsAny<IDictionary<string, string>>()),
            Times.Once);
    }

    // -----------------------------------------------------------------------
    // Failure path
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Handle_PipelineThrows_RepliesFailureResult()
    {
        var message = BuildRequest();
        _pipelineMock
            .Setup(p => p.ExecuteAsync(It.IsAny<RenderRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("render failed"));

        _busMock
            .Setup(b => b.Reply(It.IsAny<object>(), It.IsAny<IDictionary<string, string>>()))
            .Returns(Task.CompletedTask);

        await _sut.Handle(message);

        _busMock.Verify(
            b => b.Reply(It.Is<DocumentRenderResult>(r => r.Success == false), It.IsAny<IDictionary<string, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_PipelineThrows_ErrorMessageIsIncluded()
    {
        var message = BuildRequest();
        _pipelineMock
            .Setup(p => p.ExecuteAsync(It.IsAny<RenderRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("render failed"));

        DocumentRenderResult? captured = null;
        _busMock
            .Setup(b => b.Reply(It.IsAny<object>(), It.IsAny<IDictionary<string, string>>()))
            .Callback<object, IDictionary<string, string>>((msg, _) => captured = msg as DocumentRenderResult)
            .Returns(Task.CompletedTask);

        await _sut.Handle(message);

        captured!.ErrorMessage.Should().Be("render failed");
    }

    [Fact]
    public async Task Handle_PipelineThrows_RecordsFailure()
    {
        var message = BuildRequest();
        _pipelineMock
            .Setup(p => p.ExecuteAsync(It.IsAny<RenderRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("boom"));

        _busMock
            .Setup(b => b.Reply(It.IsAny<object>(), It.IsAny<IDictionary<string, string>>()))
            .Returns(Task.CompletedTask);

        await _sut.Handle(message);

        _metricsMock.Verify(m => m.RecordFailure(), Times.Once);
        _metricsMock.Verify(m => m.RecordSuccess(), Times.Never);
    }

    [Fact]
    public async Task Handle_PipelineThrows_BusStillReceivesReply()
    {
        var message = BuildRequest();
        _pipelineMock
            .Setup(p => p.ExecuteAsync(It.IsAny<RenderRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("boom"));

        _busMock
            .Setup(b => b.Reply(It.IsAny<object>(), It.IsAny<IDictionary<string, string>>()))
            .Returns(Task.CompletedTask);

        await _sut.Handle(message);

        // Bus.Reply must be called exactly once even on failure
        _busMock.Verify(
            b => b.Reply(It.IsAny<object>(), It.IsAny<IDictionary<string, string>>()),
            Times.Once);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private void ArrangeSuccess(byte[] pdfBytes)
    {
        _pipelineMock
            .Setup(p => p.ExecuteAsync(It.IsAny<RenderRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RenderRequest req, CancellationToken _) =>
                RenderResult.Success(req.JobId, pdfBytes, TimeSpan.FromSeconds(1), req.Template.DocumentType));

        _busMock
            .Setup(b => b.Reply(It.IsAny<object>(), It.IsAny<IDictionary<string, string>>()))
            .Returns(Task.CompletedTask);
    }

    private static DocumentRenderRequest BuildRequest(
        string deviceId       = "device-1",
        string? sessionId     = "session-1",
        string documentType   = "test",
        bool returnPdfInline  = true) =>
        new()
        {
            CorrelationId    = Guid.NewGuid(),
            DeviceId         = deviceId,
            SessionId        = sessionId,
            Template         = new DocumentTemplate { DocumentType = documentType },
            ReturnPdfInline  = returnPdfInline
        };
}
