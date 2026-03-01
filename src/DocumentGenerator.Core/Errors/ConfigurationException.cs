namespace DocumentGenerator.Core.Errors;

/// <summary>
/// Thrown when required configuration is absent or invalid at startup or first-use.
/// </summary>
public sealed class ConfigurationException : DocumentGeneratorException
{
    /// <summary>The configuration key that is missing or invalid.</summary>
    public string ConfigKey { get; }

    private ConfigurationException(ErrorCode code, string configKey, string message)
        : base(code, message, new Dictionary<string, object?> { ["configKey"] = configKey })
    {
        ConfigKey = configKey;
    }

    // ── Factory methods ───────────────────────────────────────────────────────

    /// <summary>
    /// Creates a <see cref="ErrorCode.ConfigurationMissing"/> exception.
    /// </summary>
    public static ConfigurationException Missing(string configKey) =>
        new(ErrorCode.ConfigurationMissing, configKey,
            $"Required configuration value '{configKey}' is missing or empty.");

    /// <summary>
    /// Creates a <see cref="ErrorCode.ConfigurationInvalid"/> exception.
    /// </summary>
    public static ConfigurationException Invalid(string configKey, string reason) =>
        new(ErrorCode.ConfigurationInvalid, configKey,
            $"Configuration value '{configKey}' is invalid: {reason}");
}
