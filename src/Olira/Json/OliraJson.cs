using System.Text.Json;
using System.Text.Json.Serialization;

namespace Olira.Json;

/// <summary>Shared <see cref="JsonSerializerOptions"/> for Olira wire formats.</summary>
public static class OliraJson
{
    /// <summary>
    /// Default options: snake_case property names, ignore nulls when writing,
    /// case-insensitive property matching when reading.
    /// </summary>
    public static JsonSerializerOptions Default { get; } = Create(ignoreNullOnWrite: true);

    /// <summary>
    /// Options that include nulls when writing (useful when the API distinguishes omit vs null).
    /// </summary>
    public static JsonSerializerOptions IncludeNulls { get; } = Create(ignoreNullOnWrite: false);

    private static JsonSerializerOptions Create(bool ignoreNullOnWrite)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = ignoreNullOnWrite
                ? JsonIgnoreCondition.WhenWritingNull
                : JsonIgnoreCondition.Never,
            WriteIndented = false,
        };
        return options;
    }
}
