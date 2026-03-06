using System.Text.Json;

namespace DocumentGenerator.Editor.Core.Models;

/// <summary>
/// Wraps sample / test data for a template. Internally stored as flat dotted keys
/// (e.g. "variables.firstName") and can be converted to / from nested dictionaries
/// for JSON serialisation.
/// </summary>
public class SampleData
{
    /// <summary>
    /// Flat key-value store where keys use dot notation (e.g. "branding.primaryColour").
    /// </summary>
    public Dictionary<string, string> FlatData { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Converts the flat dotted-key dictionary into a nested dictionary structure
    /// suitable for Handlebars template rendering.
    /// </summary>
    /// <example>
    /// { "variables.firstName": "Jane" } → { "variables": { "firstName": "Jane" } }
    /// </example>
    public Dictionary<string, object> ToNested()
    {
        var root = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, value) in FlatData)
        {
            var parts = key.Split('.');
            var current = root;

            for (var i = 0; i < parts.Length - 1; i++)
            {
                if (!current.TryGetValue(parts[i], out var existing) || existing is not Dictionary<string, object> dict)
                {
                    dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                    current[parts[i]] = dict;
                }
                current = dict;
            }

            current[parts[^1]] = value;
        }

        return root;
    }

    /// <summary>
    /// Flattens a nested dictionary (from JSON) into dotted-key form.
    /// </summary>
    /// <param name="nested">The nested dictionary.</param>
    /// <returns>A new <see cref="SampleData"/> instance with flattened keys.</returns>
    public static SampleData FromNested(Dictionary<string, object> nested)
    {
        ArgumentNullException.ThrowIfNull(nested);

        var data = new SampleData();
        FlattenRecursive(nested, string.Empty, data.FlatData);
        return data;
    }

    /// <summary>
    /// Creates a <see cref="SampleData"/> from a <see cref="JsonElement"/> (handles nested JSON objects).
    /// </summary>
    public static SampleData FromJsonElement(JsonElement element)
    {
        var data = new SampleData();
        FlattenJsonElement(element, string.Empty, data.FlatData);
        return data;
    }

    /// <summary>
    /// Default sample data matching the standard badge template fields.
    /// </summary>
    public static SampleData DefaultSampleData => new()
    {
        FlatData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["branding.companyName"] = "TechConf 2026",
            ["branding.primaryColour"] = "#6C3CE1",
            ["branding.secondaryColour"] = "#F3F0FF",
            ["branding.bodyFont"] = "Segoe UI, Arial, sans-serif",
            ["branding.custom.accentColour"] = "#FF5A5F",
            ["variables.firstName"] = "Jane",
            ["variables.lastName"] = "Smith",
            ["variables.jobTitle"] = "Senior Engineer",
            ["variables.company"] = "Acme Corp",
            ["variables.ticketType"] = "Speaker",
            ["variables.attendeeId"] = "TC2026-00842",
            ["variables.sessionName"] = "Hall A \u2014 Keynote",
            ["variables.eventDate"] = "12\u201314 March 2026",
            ["variables.eventVenue"] = "ExCeL London"
        }
    };

    private static void FlattenRecursive(Dictionary<string, object> dict, string prefix, Dictionary<string, string> output)
    {
        foreach (var (key, value) in dict)
        {
            var fullKey = string.IsNullOrEmpty(prefix) ? key : $"{prefix}.{key}";

            if (value is Dictionary<string, object> child)
            {
                FlattenRecursive(child, fullKey, output);
            }
            else
            {
                output[fullKey] = value?.ToString() ?? string.Empty;
            }
        }
    }

    private static void FlattenJsonElement(JsonElement element, string prefix, Dictionary<string, string> output)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    var fullKey = string.IsNullOrEmpty(prefix) ? prop.Name : $"{prefix}.{prop.Name}";
                    FlattenJsonElement(prop.Value, fullKey, output);
                }
                break;

            case JsonValueKind.Array:
                // Arrays are stored as JSON strings for simplicity
                output[prefix] = element.GetRawText();
                break;

            case JsonValueKind.String:
                output[prefix] = element.GetString() ?? string.Empty;
                break;

            case JsonValueKind.Number:
                output[prefix] = element.GetRawText();
                break;

            case JsonValueKind.True:
                output[prefix] = "true";
                break;

            case JsonValueKind.False:
                output[prefix] = "false";
                break;

            case JsonValueKind.Null:
                output[prefix] = string.Empty;
                break;
        }
    }
}
