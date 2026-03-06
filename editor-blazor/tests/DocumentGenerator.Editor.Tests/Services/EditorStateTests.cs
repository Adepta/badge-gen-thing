using FluentAssertions;
using DocumentGenerator.Editor.Core.Models;
using DocumentGenerator.Editor.Web.Services;

namespace DocumentGenerator.Editor.Tests.Services;

public class EditorStateTests
{
    private readonly EditorState _state = new();

    // ── Initial State ──

    [Fact]
    public void InitialState_HasCorrectDefaults()
    {
        _state.CurrentTemplateName.Should().BeNull();
        _state.HtmlContent.Should().BeEmpty();
        _state.CssContent.Should().BeEmpty();
        _state.IsDirty.Should().BeFalse();
        _state.IsLoading.Should().BeFalse();
        _state.PreviewMode.Should().Be("editor");
        _state.SampleData.Should().BeNull();
        _state.SaveStatus.Should().Be("Ready");
        _state.LastSavedAt.Should().BeNull();
        _state.CursorLine.Should().Be(1);
        _state.CursorColumn.Should().Be(1);
    }

    // ── LoadTemplateAsync ──

    [Fact]
    public async Task LoadTemplateAsync_SetsAllProperties()
    {
        var sampleData = SampleData.DefaultSampleData;

        await _state.LoadTemplateAsync("test-template", "<div>Hello</div>", ".test{}", sampleData);

        _state.CurrentTemplateName.Should().Be("test-template");
        _state.HtmlContent.Should().Be("<div>Hello</div>");
        _state.CssContent.Should().Be(".test{}");
        _state.SampleData.Should().BeSameAs(sampleData);
        _state.IsDirty.Should().BeFalse();
        _state.SaveStatus.Should().Be("Ready");
    }

    [Fact]
    public async Task LoadTemplateAsync_FiresTemplateLoadedEvent()
    {
        bool fired = false;
        _state.OnTemplateLoaded += () => { fired = true; return Task.CompletedTask; };

        await _state.LoadTemplateAsync("test", "<div/>", "", null);

        fired.Should().BeTrue();
    }

    [Fact]
    public async Task LoadTemplateAsync_FiresStateChangedEvent()
    {
        bool fired = false;
        _state.OnStateChanged += () => { fired = true; return Task.CompletedTask; };

        await _state.LoadTemplateAsync("test", "<div/>", "", null);

        fired.Should().BeTrue();
    }

    [Fact]
    public async Task LoadTemplateAsync_WithNullSampleData_SetsNull()
    {
        await _state.LoadTemplateAsync("test", "<div/>", "", null);
        _state.SampleData.Should().BeNull();
    }

    // ── UpdateHtmlAsync ──

    [Fact]
    public async Task UpdateHtmlAsync_SetsContentAndDirty()
    {
        await _state.UpdateHtmlAsync("<h1>Updated</h1>");

        _state.HtmlContent.Should().Be("<h1>Updated</h1>");
        _state.IsDirty.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateHtmlAsync_FiresContentChangedEvent()
    {
        bool fired = false;
        _state.OnContentChanged += () => { fired = true; return Task.CompletedTask; };

        await _state.UpdateHtmlAsync("<div/>");

        fired.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateHtmlAsync_FiresStateChangedEvent()
    {
        bool fired = false;
        _state.OnStateChanged += () => { fired = true; return Task.CompletedTask; };

        await _state.UpdateHtmlAsync("<div/>");

        fired.Should().BeTrue();
    }

    // ── UpdateCssAsync ──

    [Fact]
    public async Task UpdateCssAsync_SetsContentAndDirty()
    {
        await _state.UpdateCssAsync("body { color: red; }");

        _state.CssContent.Should().Be("body { color: red; }");
        _state.IsDirty.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateCssAsync_FiresContentChangedEvent()
    {
        bool fired = false;
        _state.OnContentChanged += () => { fired = true; return Task.CompletedTask; };

        await _state.UpdateCssAsync(".test {}");

        fired.Should().BeTrue();
    }

    // ── MarkSavedAsync ──

    [Fact]
    public async Task MarkSavedAsync_ClearsDirtyAndSetsSaved()
    {
        await _state.UpdateHtmlAsync("<div>change</div>");
        _state.IsDirty.Should().BeTrue();

        await _state.MarkSavedAsync();

        _state.IsDirty.Should().BeFalse();
        _state.SaveStatus.Should().Be("Saved");
        _state.LastSavedAt.Should().NotBeNull();
        _state.LastSavedAt.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(5));
    }

    // ── SetSaveStatusAsync ──

    [Fact]
    public async Task SetSaveStatusAsync_SetsStatus()
    {
        await _state.SetSaveStatusAsync("Saving...");
        _state.SaveStatus.Should().Be("Saving...");
    }

    [Fact]
    public async Task SetSaveStatusAsync_FiresStateChanged()
    {
        bool fired = false;
        _state.OnStateChanged += () => { fired = true; return Task.CompletedTask; };

        await _state.SetSaveStatusAsync("Error");

        fired.Should().BeTrue();
    }

    // ── SetPreviewModeAsync ──

    [Fact]
    public async Task SetPreviewModeAsync_SetsMode()
    {
        await _state.SetPreviewModeAsync("live");
        _state.PreviewMode.Should().Be("live");
    }

    [Fact]
    public async Task SetPreviewModeAsync_FiresPreviewModeChangedEvent()
    {
        bool fired = false;
        _state.OnPreviewModeChanged += () => { fired = true; return Task.CompletedTask; };

        await _state.SetPreviewModeAsync("live");

        fired.Should().BeTrue();
    }

    // ── UpdateSampleDataAsync ──

    [Fact]
    public async Task UpdateSampleDataAsync_SetsSampleData()
    {
        var data = SampleData.DefaultSampleData;

        await _state.UpdateSampleDataAsync(data);

        _state.SampleData.Should().BeSameAs(data);
    }

    [Fact]
    public async Task UpdateSampleDataAsync_FiresSampleDataChangedEvent()
    {
        bool fired = false;
        _state.OnSampleDataChanged += () => { fired = true; return Task.CompletedTask; };

        await _state.UpdateSampleDataAsync(new SampleData());

        fired.Should().BeTrue();
    }

    // ── SetLoadingAsync ──

    [Fact]
    public async Task SetLoadingAsync_SetsLoadingState()
    {
        await _state.SetLoadingAsync(true);
        _state.IsLoading.Should().BeTrue();

        await _state.SetLoadingAsync(false);
        _state.IsLoading.Should().BeFalse();
    }

    // ── RequestSaveAsync ──

    [Fact]
    public async Task RequestSaveAsync_FiresSaveRequestedEvent()
    {
        bool fired = false;
        _state.OnSaveRequested += () => { fired = true; return Task.CompletedTask; };

        await _state.RequestSaveAsync();

        fired.Should().BeTrue();
    }

    [Fact]
    public async Task RequestSaveAsync_NoSubscribers_DoesNotThrow()
    {
        // No subscribers - should not throw
        await _state.RequestSaveAsync();
    }

    // ── Clear ──

    [Fact]
    public void Clear_ResetsAllState()
    {
        // Set some state first
        _state.CursorLine = 10;
        _state.CursorColumn = 25;

        _state.Clear();

        _state.CurrentTemplateName.Should().BeNull();
        _state.HtmlContent.Should().BeEmpty();
        _state.CssContent.Should().BeEmpty();
        _state.IsDirty.Should().BeFalse();
        _state.SampleData.Should().BeNull();
        _state.SaveStatus.Should().Be("Ready");
        _state.LastSavedAt.Should().BeNull();
        _state.CursorLine.Should().Be(1);
        _state.CursorColumn.Should().Be(1);
    }

    // ── ClearAsync ──

    [Fact]
    public async Task ClearAsync_ResetsAllStateAndNotifies()
    {
        await _state.LoadTemplateAsync("test", "<div/>", ".test{}", SampleData.DefaultSampleData);
        await _state.UpdateHtmlAsync("<changed/>");

        bool stateChanged = false;
        _state.OnStateChanged += () => { stateChanged = true; return Task.CompletedTask; };

        await _state.ClearAsync();

        _state.CurrentTemplateName.Should().BeNull();
        _state.IsDirty.Should().BeFalse();
        stateChanged.Should().BeTrue();
    }

    // ── Full Workflow ──

    [Fact]
    public async Task FullWorkflow_LoadEditSaveClear()
    {
        // Load
        await _state.LoadTemplateAsync("workflow-test", "<div/>", "", null);
        _state.IsDirty.Should().BeFalse();

        // Edit
        await _state.UpdateHtmlAsync("<div>edited</div>");
        _state.IsDirty.Should().BeTrue();

        // Save
        await _state.MarkSavedAsync();
        _state.IsDirty.Should().BeFalse();
        _state.SaveStatus.Should().Be("Saved");

        // Clear
        await _state.ClearAsync();
        _state.CurrentTemplateName.Should().BeNull();
        _state.HtmlContent.Should().BeEmpty();
    }

    // ── Event Safety ──

    [Fact]
    public async Task Events_WithNoSubscribers_DoNotThrow()
    {
        // All of these should succeed with no event subscribers
        await _state.LoadTemplateAsync("test", "", "", null);
        await _state.UpdateHtmlAsync("<div/>");
        await _state.UpdateCssAsync(".test{}");
        await _state.MarkSavedAsync();
        await _state.SetSaveStatusAsync("test");
        await _state.SetPreviewModeAsync("live");
        await _state.UpdateSampleDataAsync(new SampleData());
        await _state.SetLoadingAsync(true);
        await _state.RequestSaveAsync();
        await _state.ClearAsync();
    }
}
