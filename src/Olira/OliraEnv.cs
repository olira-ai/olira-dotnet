using System.Text.Json.Serialization;

namespace Olira;

/// <summary>
/// Environment for event routing. Use <see cref="Development"/> for non-production systems.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<OliraEnv>))]
public enum OliraEnv
{
    /// <summary>Production systems.</summary>
    [JsonStringEnumMemberName("production")]
    Production,

    /// <summary>Non-production / development systems.</summary>
    [JsonStringEnumMemberName("development")]
    Development,
}

/// <summary>Extensions for <see cref="OliraEnv"/> wire values.</summary>
public static class OliraEnvExtensions
{
    /// <summary>Returns the API wire string (<c>production</c> / <c>development</c>).</summary>
    public static string ToWireValue(this OliraEnv env) =>
        env switch
        {
            OliraEnv.Production => "production",
            OliraEnv.Development => "development",
            _ => env.ToString().ToLowerInvariant(),
        };
}
