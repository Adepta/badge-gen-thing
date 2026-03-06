using FluentAssertions;
using DocumentGenerator.Editor.Infrastructure.Database;
using DocumentGenerator.Editor.Core.Models;

namespace DocumentGenerator.Editor.Tests.Infrastructure;

public class SqliteMetadataStoreTests : IAsyncLifetime
{
    private readonly SqliteMetadataStore _store;

    public SqliteMetadataStoreTests()
    {
        // Use a unique file-based DB per test to avoid in-memory connection issues.
        // In-memory SQLite DBs are per-connection and SqliteMetadataStore creates
        // a new connection per call, so we use a temp file instead.
        var dbPath = Path.Combine(Path.GetTempPath(), $"editor-test-{Guid.NewGuid():N}.db");
        _store = new SqliteMetadataStore($"Data Source={dbPath}");
    }

    public async Task InitializeAsync()
    {
        await _store.InitializeAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task InitializeAsync_CreatesTable()
    {
        // The table should exist after initialization (called in InitializeAsync above).
        // Verify by inserting a record without error.
        await _store.UpsertTemplateAsync("test-init", TemplateFamily.Custom, SizePreset.A6, DateTime.UtcNow);

        var results = await _store.SearchAsync();
        results.Should().ContainSingle(r => r.Name == "test-init");
    }

    [Fact]
    public async Task UpsertAsync_InsertsNew()
    {
        var now = DateTime.UtcNow;
        await _store.UpsertTemplateAsync("badge-pulse-a6", TemplateFamily.Pulse, SizePreset.A6, now);

        var results = await _store.SearchAsync();
        var item = results.Should().ContainSingle(r => r.Name == "badge-pulse-a6").Subject;
        item.Family.Should().Be(TemplateFamily.Pulse);
        item.SizePreset.Should().Be(SizePreset.A6);
    }

    [Fact]
    public async Task UpsertAsync_UpdatesExisting()
    {
        var now = DateTime.UtcNow;
        await _store.UpsertTemplateAsync("update-test", TemplateFamily.Custom, SizePreset.A6, now);
        await _store.UpsertTemplateAsync("update-test", TemplateFamily.Executive, SizePreset.CreditCard, now.AddHours(1));

        var results = await _store.SearchAsync();
        var item = results.Should().ContainSingle(r => r.Name == "update-test").Subject;
        item.Family.Should().Be(TemplateFamily.Executive);
        item.SizePreset.Should().Be(SizePreset.CreditCard);
    }

    [Fact]
    public async Task SearchAsync_ReturnsAll()
    {
        await _store.UpsertTemplateAsync("template-a", TemplateFamily.Pulse, SizePreset.A6, DateTime.UtcNow);
        await _store.UpsertTemplateAsync("template-b", TemplateFamily.Carbon, SizePreset.CreditCard, DateTime.UtcNow);
        await _store.UpsertTemplateAsync("template-c", TemplateFamily.Executive, SizePreset.A4, DateTime.UtcNow);

        var results = await _store.SearchAsync();
        results.Should().HaveCount(3);
    }

    [Fact]
    public async Task SearchAsync_FiltersByName()
    {
        await _store.UpsertTemplateAsync("badge-pulse-a6", TemplateFamily.Pulse, SizePreset.A6, DateTime.UtcNow);
        await _store.UpsertTemplateAsync("badge-carbon-cc", TemplateFamily.Carbon, SizePreset.CreditCard, DateTime.UtcNow);
        await _store.UpsertTemplateAsync("invoice-basic", TemplateFamily.Invoice, SizePreset.A4, DateTime.UtcNow);

        var results = await _store.SearchAsync(query: "badge");
        results.Should().HaveCount(2);
        results.Should().OnlyContain(r => r.Name.Contains("badge"));
    }

    [Fact]
    public async Task SearchAsync_FiltersByFamily()
    {
        await _store.UpsertTemplateAsync("badge-pulse-a6", TemplateFamily.Pulse, SizePreset.A6, DateTime.UtcNow);
        await _store.UpsertTemplateAsync("badge-carbon-cc", TemplateFamily.Carbon, SizePreset.CreditCard, DateTime.UtcNow);
        await _store.UpsertTemplateAsync("badge-pulse-cc", TemplateFamily.Pulse, SizePreset.CreditCard, DateTime.UtcNow);

        var results = await _store.SearchAsync(family: TemplateFamily.Pulse);
        results.Should().HaveCount(2);
        results.Should().OnlyContain(r => r.Family == TemplateFamily.Pulse);
    }

    [Fact]
    public async Task DeleteAsync_Removes()
    {
        await _store.UpsertTemplateAsync("to-delete", TemplateFamily.Custom, SizePreset.A6, DateTime.UtcNow);

        var beforeDelete = await _store.SearchAsync();
        beforeDelete.Should().ContainSingle(r => r.Name == "to-delete");

        await _store.DeleteTemplateAsync("to-delete");

        var afterDelete = await _store.SearchAsync();
        afterDelete.Should().NotContain(r => r.Name == "to-delete");
    }

    [Fact]
    public async Task RenameAsync_UpdatesName()
    {
        await _store.UpsertTemplateAsync("old-name", TemplateFamily.Pulse, SizePreset.A6, DateTime.UtcNow);

        await _store.RenameTemplateAsync("old-name", "new-name");

        var results = await _store.SearchAsync();
        results.Should().NotContain(r => r.Name == "old-name");
        results.Should().ContainSingle(r => r.Name == "new-name");
    }
}
