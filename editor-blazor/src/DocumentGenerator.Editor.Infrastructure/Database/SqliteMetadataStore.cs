using Dapper;
using Microsoft.Data.Sqlite;
using DocumentGenerator.Editor.Core.DTOs;
using DocumentGenerator.Editor.Core.Interfaces;
using DocumentGenerator.Editor.Core.Models;

namespace DocumentGenerator.Editor.Infrastructure.Database;

/// <summary>
/// SQLite-backed implementation of <see cref="IMetadataStore"/>.
/// Provides indexed search and filtering over template metadata.
/// </summary>
public class SqliteMetadataStore : IMetadataStore
{
    private readonly string _connectionString;

    /// <summary>
    /// The SQL migration script embedded in the assembly.
    /// </summary>
    private const string MigrationSql = """
        CREATE TABLE IF NOT EXISTS templates (
            name TEXT PRIMARY KEY,
            family TEXT NOT NULL,
            size_preset TEXT NOT NULL,
            modified_at TEXT NOT NULL,
            thumbnail_path TEXT
        );
        """;

    /// <summary>
    /// Creates a new metadata store using the given SQLite connection string.
    /// </summary>
    /// <param name="connectionString">SQLite connection string (e.g. "Data Source=editor-metadata.db").</param>
    public SqliteMetadataStore(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _connectionString = connectionString;
    }

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await connection.ExecuteAsync(MigrationSql);
    }

    /// <inheritdoc />
    public async Task UpsertTemplateAsync(string name, TemplateFamily family, SizePreset size, DateTime modifiedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        const string sql = """
            INSERT INTO templates (name, family, size_preset, modified_at)
            VALUES (@Name, @Family, @SizePreset, @ModifiedAt)
            ON CONFLICT(name) DO UPDATE SET
                family = @Family,
                size_preset = @SizePreset,
                modified_at = @ModifiedAt;
            """;

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await connection.ExecuteAsync(sql, new
        {
            Name = name,
            Family = family.ToString(),
            SizePreset = size.ToString(),
            ModifiedAt = modifiedAt.ToString("O")
        });
    }

    /// <inheritdoc />
    public async Task DeleteTemplateAsync(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        const string sql = "DELETE FROM templates WHERE name = @Name;";

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await connection.ExecuteAsync(sql, new { Name = name });
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TemplateListItem>> SearchAsync(string? query = null, TemplateFamily? family = null)
    {
        var sql = "SELECT name, family, size_preset AS SizePreset, modified_at AS ModifiedAt FROM templates WHERE 1=1";
        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(query))
        {
            sql += " AND name LIKE @Query";
            parameters.Add("Query", $"%{query}%");
        }

        if (family.HasValue)
        {
            sql += " AND family = @Family";
            parameters.Add("Family", family.Value.ToString());
        }

        sql += " ORDER BY name;";

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var rows = await connection.QueryAsync<TemplateRow>(sql, parameters);

        var items = rows.Select(r =>
        {
            Enum.TryParse<TemplateFamily>(r.Family, ignoreCase: true, out var f);
            Enum.TryParse<SizePreset>(r.SizePreset, ignoreCase: true, out var s);
            DateTime.TryParse(r.ModifiedAt, out var mod);

            return new TemplateListItem(r.Name, f, s, mod == default ? null : mod, HasCss: true);
        }).ToList();

        return items;
    }

    /// <inheritdoc />
    public async Task RenameTemplateAsync(string oldName, string newName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(oldName);
        ArgumentException.ThrowIfNullOrWhiteSpace(newName);

        const string sql = "UPDATE templates SET name = @NewName WHERE name = @OldName;";

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await connection.ExecuteAsync(sql, new { OldName = oldName, NewName = newName });
    }

    /// <summary>
    /// Internal DTO for Dapper row mapping.
    /// </summary>
    private sealed class TemplateRow
    {
        public string Name { get; set; } = string.Empty;
        public string Family { get; set; } = string.Empty;
        public string SizePreset { get; set; } = string.Empty;
        public string ModifiedAt { get; set; } = string.Empty;
    }
}
