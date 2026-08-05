using System.Collections;
using System.Text.Json;
using System.Text.Json.Serialization;
using Olira.Json;

namespace Olira;

/// <summary>
/// One stable state module (demographics, condition_diagnosis, medications, user_preferences).
/// </summary>
public sealed class StableModule
{
    /// <summary>Module type key.</summary>
    public string ModuleType { get; set; } = "";

    /// <summary>Module payload.</summary>
    public Dictionary<string, object?>? Payload { get; set; }

    /// <summary>Creation timestamp.</summary>
    public string? CreatedAt { get; set; }

    /// <summary>Last update timestamp.</summary>
    public string? UpdatedAt { get; set; }
}

/// <summary>Result of get_stable_data(). Modules keyed by module_type.</summary>
public sealed class StableDataResult
{
    /// <summary>Patient identifier.</summary>
    public string PatientId { get; set; } = "";

    /// <summary>Modules keyed by module_type.</summary>
    public Dictionary<string, StableModule> Modules { get; set; } = new();
}

/// <summary>Metadata entry for a single event state module.</summary>
public sealed class EventStateModuleSummary
{
    /// <summary>Module type key.</summary>
    public string ModuleType { get; set; } = "";

    /// <summary>Last update timestamp.</summary>
    public string? UpdatedAt { get; set; }

    /// <summary>Creation timestamp.</summary>
    public string? CreatedAt { get; set; }
}

/// <summary>Result of get_event_state_module().</summary>
public sealed class EventStateModuleResult
{
    /// <summary>Patient identifier.</summary>
    public string PatientId { get; set; } = "";

    /// <summary>Module type key.</summary>
    public string ModuleType { get; set; } = "";

    /// <summary>Module payload (object or array).</summary>
    public JsonElement? Payload { get; set; }

    /// <summary>Creation timestamp.</summary>
    public string? CreatedAt { get; set; }

    /// <summary>Last update timestamp.</summary>
    public string? UpdatedAt { get; set; }
}

/// <summary>
/// Metadata entry for a view. <see cref="HasBlocks"/> reflects the unified block list;
/// <see cref="HasTemp"/> reflects whether live append-only TEMP entries exist.
/// </summary>
public sealed class ViewMeta
{
    /// <summary>View type key.</summary>
    public string ViewType { get; set; } = "";

    /// <summary>View identifier.</summary>
    public string ViewId { get; set; } = "";

    /// <summary>Whether the unified block list is present.</summary>
    public bool HasBlocks { get; set; }

    /// <summary>Whether live TEMP entries exist.</summary>
    public bool HasTemp { get; set; }
}

/// <summary>Metadata for one block within a view.</summary>
public sealed class ViewBlockMeta
{
    /// <summary>Block identifier.</summary>
    public string? BlockId { get; set; }

    /// <summary>Block display name.</summary>
    public string? BlockName { get; set; }

    /// <summary>Whether a result is present.</summary>
    public bool HasResult { get; set; }
}

/// <summary>Result of list_view_blocks().</summary>
public sealed class ViewBlocksListResult
{
    /// <summary>Patient identifier.</summary>
    public string PatientId { get; set; } = "";

    /// <summary>View type key.</summary>
    public string ViewType { get; set; } = "";

    /// <summary>Block metadata entries.</summary>
    public List<ViewBlockMeta> Blocks { get; set; } = [];
}

/// <summary>
/// Result of get_view(). <see cref="Content"/> holds the unified block list under
/// the key "blocks", plus "temp" entries when present.
/// </summary>
public sealed class ViewResult
{
    /// <summary>Patient identifier.</summary>
    public string PatientId { get; set; } = "";

    /// <summary>View type key.</summary>
    public string ViewType { get; set; } = "";

    /// <summary>View identifier.</summary>
    public string? ViewId { get; set; }

    /// <summary>Validity start timestamp.</summary>
    public string? ValidFrom { get; set; }

    /// <summary>Validity end timestamp.</summary>
    public string? ValidTo { get; set; }

    /// <summary>View content object.</summary>
    public Dictionary<string, object?> Content { get; set; } = new();
}

/// <summary>Result of get_view_block().</summary>
public sealed class ViewBlockResult
{
    /// <summary>Patient identifier.</summary>
    public string PatientId { get; set; } = "";

    /// <summary>View type key.</summary>
    public string ViewType { get; set; } = "";

    /// <summary>Block identifier.</summary>
    public string BlockId { get; set; } = "";

    /// <summary>Block content text.</summary>
    public string? Content { get; set; }

    /// <summary>Per-field confidence scores.</summary>
    public Dictionary<string, double>? Confidences { get; set; }

    /// <summary>Last update timestamp.</summary>
    public string? UpdatedAt { get; set; }
}

/// <summary>Result of get_view_recent_events(). Entries are the TEMP segment string list.</summary>
public sealed class ViewRecentEventsResult
{
    /// <summary>Patient identifier.</summary>
    public string PatientId { get; set; } = "";

    /// <summary>View type key.</summary>
    public string ViewType { get; set; } = "";

    /// <summary>TEMP segment entries.</summary>
    public List<string> Entries { get; set; } = [];

    /// <summary>Number of entries returned.</summary>
    public int Count { get; set; }

    /// <summary>Total available entries.</summary>
    public int TotalCount { get; set; }
}

/// <summary>One event log entry returned by get_logs().</summary>
public sealed class LogEntry
{
    /// <summary>Log entry identifier.</summary>
    public string Id { get; set; } = "";

    /// <summary>Log type.</summary>
    public string? Type { get; set; }

    /// <summary>Event timestamp.</summary>
    public string? Timestamp { get; set; }

    /// <summary>Ingestion timestamp.</summary>
    public string? IngestedAt { get; set; }

    /// <summary>Event payload.</summary>
    public Dictionary<string, object?> Payload { get; set; } = new();

    /// <summary>Optional provenance link.</summary>
    public OliraTrace? Trace { get; set; }
}

/// <summary>Result of get_logs().</summary>
public sealed class LogsResult
{
    /// <summary>Patient identifier.</summary>
    public string PatientId { get; set; } = "";

    /// <summary>Number of logs returned.</summary>
    public int Count { get; set; }

    /// <summary>Log entries.</summary>
    public List<LogEntry> Logs { get; set; } = [];
}

/// <summary>Result of a log query. Mirrors POST /v1/state/.../logs/query.</summary>
/// <remarks>
/// Implements <see cref="IEnumerable{T}"/> for Python parity; a custom converter is required
/// so System.Text.Json deserializes the object shape (not as a bare array).
/// </remarks>
[JsonConverter(typeof(LogQueryResultJsonConverter))]
public sealed class LogQueryResult : IEnumerable<Dictionary<string, object?>>
{
    /// <summary>Number of rows returned.</summary>
    public int Count { get; set; }

    /// <summary>Result rows.</summary>
    public List<Dictionary<string, object?>> Rows { get; set; } = [];

    /// <summary>Organization identifier, when present.</summary>
    public string? OrganizationId { get; set; }

    /// <summary>Patient identifier, when present.</summary>
    public string? PatientId { get; set; }

    /// <summary>Total matching rows, when present.</summary>
    public int? TotalCount { get; set; }

    /// <summary>Whether more rows are available.</summary>
    public bool? HasMore { get; set; }

    /// <summary>Row at the given index (mirrors Python <c>__getitem__</c>).</summary>
    public Dictionary<string, object?> this[int index] => Rows[index];

    /// <inheritdoc />
    public IEnumerator<Dictionary<string, object?>> GetEnumerator() => Rows.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Validate rows into typed <see cref="LogEntry"/> instances.
    /// Only valid when no column projection (.select) was used.
    /// </summary>
    public List<LogEntry> AsLogs()
    {
        var result = new List<LogEntry>(Rows.Count);
        foreach (var row in Rows)
        {
            var json = JsonSerializer.Serialize(row, OliraJson.Default);
            var entry = JsonSerializer.Deserialize<LogEntry>(json, OliraJson.Default)
                        ?? throw new ValidationError("Failed to deserialize log row into LogEntry");
            result.Add(entry);
        }
        return result;
    }
}

/// <summary>
/// Deserializes <see cref="LogQueryResult"/> as a JSON object (STJ would otherwise treat
/// <see cref="IEnumerable{T}"/> implementers as arrays).
/// </summary>
internal sealed class LogQueryResultJsonConverter : JsonConverter<LogQueryResult>
{
    private sealed class LogQueryResultDto
    {
        public int Count { get; set; }
        public List<Dictionary<string, JsonElement>> Rows { get; set; } = [];
        public string? OrganizationId { get; set; }
        public string? PatientId { get; set; }
        public int? TotalCount { get; set; }
        public bool? HasMore { get; set; }
    }

    public override LogQueryResult Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var dto = JsonSerializer.Deserialize<LogQueryResultDto>(ref reader, options)
                  ?? throw new JsonException("Expected LogQueryResult object");

        var rows = new List<Dictionary<string, object?>>(dto.Rows.Count);
        foreach (var row in dto.Rows)
        {
            var mapped = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var (key, value) in row)
            {
                mapped[key] = JsonElementToObject(value);
            }

            rows.Add(mapped);
        }

        return new LogQueryResult
        {
            Count = dto.Count,
            Rows = rows,
            OrganizationId = dto.OrganizationId,
            PatientId = dto.PatientId,
            TotalCount = dto.TotalCount,
            HasMore = dto.HasMore,
        };
    }

    public override void Write(Utf8JsonWriter writer, LogQueryResult value, JsonSerializerOptions options)
    {
        var dto = new LogQueryResultDto
        {
            Count = value.Count,
            OrganizationId = value.OrganizationId,
            PatientId = value.PatientId,
            TotalCount = value.TotalCount,
            HasMore = value.HasMore,
        };
        foreach (var row in value.Rows)
        {
            var json = JsonSerializer.Serialize(row, options);
            var mapped = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, options)
                         ?? new Dictionary<string, JsonElement>();
            dto.Rows.Add(mapped);
        }

        JsonSerializer.Serialize(writer, dto, options);
    }

    private static object? JsonElementToObject(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            JsonValueKind.String => element.GetString(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number when element.TryGetInt64(out var l) => l,
            JsonValueKind.Number when element.TryGetDouble(out var d) => d,
            JsonValueKind.Array => element.EnumerateArray().Select(JsonElementToObject).ToList(),
            JsonValueKind.Object => element.EnumerateObject()
                .ToDictionary(p => p.Name, p => JsonElementToObject(p.Value), StringComparer.Ordinal),
            _ => element.Clone(),
        };
}

/// <summary>One event returned by get_events().</summary>
public sealed class EventEntry
{
    /// <summary>Event identifier.</summary>
    public string Id { get; set; } = "";

    /// <summary>Trigger name.</summary>
    public string? Trigger { get; set; }

    /// <summary>Associated log type.</summary>
    public string? LogType { get; set; }

    /// <summary>Event status.</summary>
    public string? Status { get; set; }

    /// <summary>When the event was triggered.</summary>
    public string? TriggeredAt { get; set; }

    /// <summary>When the event completed.</summary>
    public string? CompletedAt { get; set; }

    /// <summary>Source event log identifier.</summary>
    public string? SourceEventLogId { get; set; }

    /// <summary>Source log payload.</summary>
    public Dictionary<string, object?>? LogPayload { get; set; }

    /// <summary>State changes produced by the event.</summary>
    public List<Dictionary<string, object?>>? Changes { get; set; }
}

/// <summary>Result of get_events().</summary>
public sealed class EventsResult
{
    /// <summary>Patient identifier.</summary>
    public string PatientId { get; set; } = "";

    /// <summary>Number of events returned.</summary>
    public int Count { get; set; }

    /// <summary>Event entries.</summary>
    public List<EventEntry> Events { get; set; } = [];
}

/// <summary>One memory record returned by read_memories().</summary>
public sealed class MemoryEntry
{
    /// <summary>Memory identifier.</summary>
    public string MemoryId { get; set; } = "";

    /// <summary>Memory content text.</summary>
    public string Content { get; set; } = "";

    /// <summary>Optional metadata.</summary>
    public Dictionary<string, object?>? Metadata { get; set; }

    /// <summary>Creation timestamp.</summary>
    public string? CreatedAt { get; set; }

    /// <summary>Last update timestamp.</summary>
    public string? UpdatedAt { get; set; }
}

/// <summary>Result of read_memories().</summary>
public sealed class MemoriesResult
{
    /// <summary>Patient identifier.</summary>
    public string PatientId { get; set; } = "";

    /// <summary>Number of memories returned.</summary>
    public int Count { get; set; }

    /// <summary>Memory entries.</summary>
    public List<MemoryEntry> Results { get; set; } = [];
}
