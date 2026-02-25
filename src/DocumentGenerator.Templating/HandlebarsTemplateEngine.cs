using System.Text.Json;
using DocumentGenerator.Core.Interfaces;
using DocumentGenerator.Core.Models;
using HandlebarsDotNet;
using Microsoft.Extensions.Logging;
using QRCoder;
using ZXing;
using ZXing.Common;
using ZXing.Rendering;

namespace DocumentGenerator.Templating;

/// <summary>
/// Handlebars.Net-backed template engine.
///
/// The Handlebars context exposed to templates has the shape:
/// <code>
/// {
///   "branding": { ...Branding properties... },
///   "variables": { ...DocumentTemplate.Variables... },
///   "meta": { "documentType": "...", "version": "...", "generatedAt": "..." }
/// }
/// </code>
/// CSS (if present) is inlined into a &lt;style&gt; tag appended to &lt;head&gt;,
/// or appended at the top of the output if no &lt;head&gt; tag is present.
/// </summary>
public sealed class HandlebarsTemplateEngine : ITemplateEngine
{
    private readonly IHandlebars _handlebars;
    private readonly ILogger<HandlebarsTemplateEngine> _logger;

    /// <summary>
    /// Initialises the engine, creates a scoped Handlebars environment, and registers
    /// the built-in helpers (<c>upper</c>, <c>lower</c>, <c>formatDate</c>, <c>currency</c>,
    /// <c>ifEquals</c>, <c>qrCode</c>, <c>barCode</c>).
    /// </summary>
    /// <param name="logger">Logger for template rendering events.</param>
    public HandlebarsTemplateEngine(ILogger<HandlebarsTemplateEngine> logger)
    {
        _logger = logger;
        _handlebars = Handlebars.Create(new HandlebarsConfiguration
        {
            ThrowOnUnresolvedBindingExpression = false,
            NoEscape = false
        });

        RegisterBuiltInHelpers(_handlebars);
    }

    /// <summary>
    /// Renders a <see cref="DocumentTemplate"/> to an HTML string by executing its
    /// Handlebars template and CSS, then injecting the CSS into the output.
    /// </summary>
    /// <param name="template">The template definition including HTML, CSS, branding, and variables.</param>
    /// <param name="cancellationToken">Token to cancel the operation (checked before compilation).</param>
    /// <returns>A complete HTML document string ready for PDF rendering.</returns>
    public Task<string> RenderAsync(DocumentTemplate template, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogDebug("Rendering template '{DocumentType}' v{Version}", template.DocumentType, template.Version);

        // Register any per-template partials
        foreach (var (name, partial) in template.Template.Partials)
        {
            _logger.LogDebug("Registering partial '{PartialName}'", name);
            _handlebars.RegisterTemplate(name, partial);
        }

        var context = BuildContext(template);

        // Render CSS first (it can also use Handlebars variables)
        string? renderedCss = null;
        if (!string.IsNullOrWhiteSpace(template.Template.Css))
        {
            // Handlebars misparses `}}}`  as a triple-stache closing delimiter.
            // Insert a zero-width space between `}}` and a following `}` so the
            // CSS closing brace is never adjacent to a Handlebars closing tag.
            var safeCss = template.Template.Css.Replace("}}}", "}} }");
            var cssTemplate = _handlebars.Compile(safeCss);
            renderedCss = cssTemplate(context);
        }

        // Render HTML body
        var htmlTemplate = _handlebars.Compile(template.Template.Html);
        var renderedHtml = htmlTemplate(context);

        // Inject CSS
        if (!string.IsNullOrWhiteSpace(renderedCss))
        {
            renderedHtml = InjectCss(renderedHtml, renderedCss);
        }

        _logger.LogDebug("Template '{DocumentType}' rendered successfully ({Length} chars)", template.DocumentType, renderedHtml.Length);
        return Task.FromResult(renderedHtml);
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private static Dictionary<string, object?> BuildContext(DocumentTemplate template)
    {
        // Flatten Branding into a dictionary so Handlebars can access it
        var branding = new Dictionary<string, object?>
        {
            ["companyName"]   = template.Branding.CompanyName,
            ["logoUrl"]       = template.Branding.LogoUrl,
            ["primaryColour"] = template.Branding.PrimaryColour,
            ["secondaryColour"] = template.Branding.SecondaryColour,
            ["headingFont"]   = template.Branding.HeadingFont,
            ["bodyFont"]      = template.Branding.BodyFont,
            ["custom"]        = template.Branding.Custom
        };

        var meta = new Dictionary<string, object?>
        {
            ["documentType"] = template.DocumentType,
            ["version"]      = template.Version,
            ["generatedAt"]  = DateTimeOffset.UtcNow.ToString("o")
        };

        // Deep-convert any JsonElement values from System.Text.Json deserialization
        var variables = DeepConvert(template.Variables);

        return new Dictionary<string, object?>
        {
            ["branding"]  = branding,
            ["variables"] = variables,
            ["meta"]      = meta
        };
    }

    /// <summary>
    /// System.Text.Json deserialises unknown object values as <see cref="JsonElement"/>.
    /// Handlebars.Net cannot reflect over JsonElement, so we unwrap them recursively.
    /// </summary>
    private static Dictionary<string, object?> DeepConvert(Dictionary<string, object?> source)
    {
        var result = new Dictionary<string, object?>(source.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in source)
        {
            result[key] = UnwrapJsonElement(value);
        }
        return result;
    }

    private static object? UnwrapJsonElement(object? value) => value switch
    {
        JsonElement el => el.ValueKind switch
        {
            JsonValueKind.Object  => el.EnumerateObject().ToDictionary(p => p.Name, p => UnwrapJsonElement(p.Value)),
            JsonValueKind.Array   => el.EnumerateArray().Select(e => UnwrapJsonElement(e)).ToList(),
            JsonValueKind.String  => el.GetString(),
            JsonValueKind.Number  => el.TryGetInt64(out var l) ? (object?)l : el.GetDouble(),
            JsonValueKind.True    => true,
            JsonValueKind.False   => false,
            JsonValueKind.Null    => null,
            _                     => el.ToString()
        },
        _ => value
    };

    private static string InjectCss(string html, string css)
    {
        var styleBlock = $"<style>{css}</style>";

        // Prefer injecting before </head>
        const string headClose = "</head>";
        var headIdx = html.IndexOf(headClose, StringComparison.OrdinalIgnoreCase);
        if (headIdx >= 0)
        {
            return string.Concat(html.AsSpan(0, headIdx), styleBlock, html.AsSpan(headIdx));
        }

        // Fall back to prepending
        return styleBlock + html;
    }

    private static void RegisterBuiltInHelpers(IHandlebars hbs)
    {
        // {{formatDate value "yyyy-MM-dd"}}
        hbs.RegisterHelper("formatDate", (output, _, args) =>
        {
            if (args.Length >= 1 && DateTimeOffset.TryParse(args[0]?.ToString(), out var dt))
            {
                var fmt = args.Length >= 2 ? args[1]?.ToString() ?? "d" : "d";
                output.WriteSafeString(dt.ToString(fmt));
            }
        });

        // {{upper value}}
        hbs.RegisterHelper("upper", (output, _, args) =>
        {
            output.WriteSafeString(args[0]?.ToString()?.ToUpperInvariant() ?? string.Empty);
        });

        // {{lower value}}
        hbs.RegisterHelper("lower", (output, _, args) =>
        {
            output.WriteSafeString(args[0]?.ToString()?.ToLowerInvariant() ?? string.Empty);
        });

        // {{currency value "GBP"}}
        hbs.RegisterHelper("currency", (output, _, args) =>
        {
            if (args.Length >= 1 && decimal.TryParse(args[0]?.ToString(), out var amount))
            {
                var culture = args.Length >= 2 ? args[1]?.ToString() ?? "en-GB" : "en-GB";
                output.WriteSafeString(amount.ToString("C", new System.Globalization.CultureInfo(culture)));
            }
        });

        // {{ifEquals a b}}...{{/ifEquals}}
        hbs.RegisterHelper("ifEquals", (output, options, context, args) =>
        {
            if (args.Length >= 2 && args[0]?.ToString() == args[1]?.ToString())
                options.Template(output, context);
            else
                options.Inverse(output, context);
        });

        // {{qrCode value [darkColour] [lightColour]}}
        // Emits an inline SVG QR code sized via viewBox — use CSS width/height to control display size.
        //   darkColour  – fill colour for dark modules, e.g. "#ffffff" (default "#000000")
        //   lightColour – fill colour for light modules, "transparent" supported (default "transparent")
        hbs.RegisterHelper("qrCode", (output, _, args) =>
        {
            var data = args.Length >= 1 ? args[0]?.ToString() : null;
            if (string.IsNullOrWhiteSpace(data))
                return;

            var darkColour  = args.Length >= 2 ? args[1]?.ToString() ?? "#000000" : "#000000";
            var lightColour = args.Length >= 3 ? args[2]?.ToString() ?? "transparent" : "transparent";
            var lightHex    = lightColour == "transparent" ? "#ffffff" : lightColour;

            using var generator = new QRCodeGenerator();
            var qrData = generator.CreateQrCode(data, QRCodeGenerator.ECCLevel.M);
            using var svgCode = new SvgQRCode(qrData);
            var svg = svgCode.GetGraphic(
                pixelsPerModule : 10,
                darkColorHex    : darkColour,
                lightColorHex   : lightHex,
                drawQuietZones  : false,
                sizingMode      : SvgQRCode.SizingMode.ViewBoxAttribute);

            // Replace the white background rect with transparent when requested
            if (lightColour == "transparent")
                svg = svg.Replace("fill=\"#ffffff\"", "fill=\"none\"", StringComparison.OrdinalIgnoreCase)
                         .Replace("fill=\"white\"",   "fill=\"none\"", StringComparison.OrdinalIgnoreCase);

            output.WriteSafeString(svg);
        });

        // {{barCode value [height] [showText] [darkColour]}}
        // Emits an inline SVG Code-128 barcode.
        //   height     – bar height in pixels (default 60)
        //   showText   – "true" | "false" whether to render the human-readable text (default "false")
        //   darkColour – bar fill colour, e.g. "#ffffff" (default "#000000")
        hbs.RegisterHelper("barCode", (output, _, args) =>
        {
            var data = args.Length >= 1 ? args[0]?.ToString() : null;
            if (string.IsNullOrWhiteSpace(data))
                return;

            var height     = args.Length >= 2 && int.TryParse(args[1]?.ToString(), out var h) ? h : 60;
            var showText   = args.Length >= 3 && string.Equals(args[2]?.ToString(), "true", StringComparison.OrdinalIgnoreCase);
            var darkColour = args.Length >= 4 ? args[3]?.ToString() ?? "#000000" : "#000000";

            var writer  = new BarcodeWriterSvg
            {
                Format  = BarcodeFormat.CODE_128,
                Options = new EncodingOptions
                {
                    Height  = height,
                    Width   = 0,        // auto-width based on content
                    Margin  = 0,
                    PureBarcode = !showText
                }
            };

            var svgImage = writer.Write(data);
            var svg = svgImage.Content;

            // Recolour bars — ZXing.Net emits fill="rgb(0,0,0)" on the <svg> root attribute
            // and individual <rect> elements inherit it. Replace all common black representations.
            if (!string.Equals(darkColour, "#000000", StringComparison.OrdinalIgnoreCase))
                svg = svg.Replace("fill=\"rgb(0,0,0)\"", $"fill=\"{darkColour}\"", StringComparison.OrdinalIgnoreCase)
                         .Replace("fill=\"black\"",      $"fill=\"{darkColour}\"", StringComparison.OrdinalIgnoreCase)
                         .Replace("fill=\"#000000\"",    $"fill=\"{darkColour}\"", StringComparison.OrdinalIgnoreCase);

            output.WriteSafeString(svg);
        });
    }
}
