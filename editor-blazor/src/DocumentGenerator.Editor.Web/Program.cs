using System.IO.Compression;
using System.Text.Json;
using DocumentGenerator.Editor.Web.Services;
using DocumentGenerator.Editor.Web.Components;
using DocumentGenerator.Editor.Core.Interfaces;
using DocumentGenerator.Editor.Infrastructure.FileSystem;
using DocumentGenerator.Editor.Infrastructure.Database;
using DocumentGenerator.Editor.Infrastructure.Services;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Configuration
var editorConfig = builder.Configuration.GetSection("Editor");
var templatesDir = Path.GetFullPath(
    editorConfig["TemplatesDir"] ?? "../../../templates",
    builder.Environment.ContentRootPath);
var assetsDir = Path.GetFullPath(
    editorConfig["AssetsDir"] ?? Path.Combine(templatesDir, "assets"),
    builder.Environment.ContentRootPath);
var sqliteConn = editorConfig["SqliteConnectionString"] ?? "Data Source=editor-metadata.db";

// Ensure directories exist
Directory.CreateDirectory(templatesDir);
Directory.CreateDirectory(assetsDir);

// Register services
builder.Services.AddScoped<ThemeService>();
builder.Services.AddScoped<SettingsService>();
builder.Services.AddScoped<KeyboardShortcutService>();
builder.Services.AddScoped<MonacoInteropService>();
builder.Services.AddScoped<MonacoLanguageService>();
builder.Services.AddScoped<MonacoCompletionService>();
builder.Services.AddScoped<MonacoValidationService>();
builder.Services.AddScoped<EditorState>();
builder.Services.AddScoped<HandlebarsInteropService>();
builder.Services.AddScoped<PreviewService>();
builder.Services.AddSingleton<ITemplateRepository>(new FileTemplateRepository(templatesDir));
builder.Services.AddSingleton<IAssetRepository>(new FileAssetRepository(assetsDir));
builder.Services.AddSingleton<ISampleDataRepository>(new SampleDataService(templatesDir));
builder.Services.AddSingleton<IMetadataStore>(new SqliteMetadataStore(sqliteConn));
builder.Services.AddSingleton<TemplateService>();
builder.Services.AddSingleton<AssetService>();
builder.Services.AddScoped<ExportService>();

var app = builder.Build();

// Initialize metadata store and sync on startup
using (var scope = app.Services.CreateScope())
{
    var metadataStore = scope.ServiceProvider.GetRequiredService<IMetadataStore>();
    await metadataStore.InitializeAsync();

    var templateService = scope.ServiceProvider.GetRequiredService<TemplateService>();
    await templateService.SyncMetadataAsync();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();

// Serve uploaded assets from the assets directory
if (Directory.Exists(assetsDir))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(assetsDir),
        RequestPath = "/assets"
    });
}

app.UseAntiforgery();

// Export endpoint - downloads a template as a .zip file
app.MapGet("/api/export/{templateName}", async (string templateName, ITemplateRepository templateRepo, ISampleDataRepository sampleDataRepo) =>
{
    var template = await templateRepo.GetAsync(templateName);
    if (template is null) return Results.NotFound();

    using var memoryStream = new MemoryStream();
    using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
    {
        // Add HTML file
        var htmlEntry = archive.CreateEntry($"{templateName}.html");
        using (var writer = new StreamWriter(htmlEntry.Open()))
            await writer.WriteAsync(template.HtmlContent);

        // Add CSS file
        if (!string.IsNullOrEmpty(template.CssContent))
        {
            var cssEntry = archive.CreateEntry($"{templateName}.css");
            using (var writer = new StreamWriter(cssEntry.Open()))
                await writer.WriteAsync(template.CssContent);
        }

        // Add sample data JSON if exists
        var sampleData = await sampleDataRepo.GetAsync(templateName);
        if (sampleData is not null)
        {
            var jsonEntry = archive.CreateEntry($"sample-{templateName}.json");
            using (var writer = new StreamWriter(jsonEntry.Open()))
                await writer.WriteAsync(JsonSerializer.Serialize(
                    sampleData.ToNested(),
                    new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    memoryStream.Position = 0;
    return Results.File(memoryStream.ToArray(), "application/zip", $"{templateName}.zip");
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
