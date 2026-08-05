using System.Text.Json;
using Olira;
using Olira.Examples;

/*
 * Olira SDK — Read Patient State
 *
 * After patients have data (logged via LogBatch or ingested via CreateIngestionJob),
 * the state-read methods give you direct access to the compiled patient state:
 *
 *   GetStableData()           — demographics, conditions, medications, preferences
 *   GetEventStateModule()     — rolling event state (symptoms, moods, vitals, labs…)
 *   ListViews() / GetView()   — materialised summary snapshots (the "views" clinicians see)
 *   GetLogs()                 — raw event log with optional filters
 *   GetEvents()               — state transitions driven by those logs
 *   ReadMemories()            — clinical memories extracted from conversations
 *
 * These are a REST mirror of the MCP Patient State tools — useful for backends and
 * pipelines that don't go through the MCP server.
 *
 * Requires: sdk:state-read scope
 * Run: dotnet run --project 06_ReadPatientState
 *
 * Note: this script expects a patient with existing data. Set PATIENT_ID in .env
 * or supply it as the first CLI argument.
 */

ExampleEnv.Load();
var apiKey = ExampleEnv.Require("OLIRA_API_KEY");
var baseUrl = ExampleEnv.BaseUrl;
var patientId = args.Length > 0 ? args[0] : ExampleEnv.Get("PATIENT_ID", "");

if (string.IsNullOrWhiteSpace(patientId))
{
    Console.WriteLine("Usage: dotnet run --project 06_ReadPatientState -- <patient_id>");
    Console.WriteLine("  Or set PATIENT_ID in your .env file.");
    Environment.Exit(1);
}

using var client = new OliraClient(
    apiKey: apiKey,
    baseUrl: baseUrl,
    environment: ExampleEnv.EnvForBaseUrl(baseUrl),
    asyncFlush: false); // state-read uses direct HTTP calls, not the background log queue

Console.WriteLine($"Reading state for patient {patientId}\n");

// ── Stable data — demographics, condition, medications ────────────────────────
Console.WriteLine("── Stable data ──");
var stable = client.GetStableData(patientId);
foreach (var (moduleType, module) in stable.Modules)
{
    var payloadStr = JsonSerializer.Serialize(module.Payload);
    Console.WriteLine($"  {moduleType}: {(payloadStr.Length > 120 ? payloadStr[..120] : payloadStr)}");
}

// ── Event state modules — rolling clinical state ──────────────────────────────
Console.WriteLine("\n── Event state modules ──");
var moduleSummaries = client.ListEventStateModules(patientId);
Console.WriteLine($"  Present modules: [{string.Join(", ", moduleSummaries.Select(m => m.ModuleType))}]");

// Fetch a specific module in full — adjust module_type to one that's present
foreach (var preferred in new[] { "symptoms", "behavioral_state", "lab_results", "vitals" })
{
    if (moduleSummaries.Any(m => m.ModuleType == preferred))
    {
        var module = client.GetEventStateModule(patientId, preferred);
        Console.WriteLine($"\n  {preferred} module:");
        var payloadStr = module.Payload?.GetRawText() ?? "null";
        Console.WriteLine(
            $"    {(payloadStr.Length > 300 ? payloadStr[..300] + "…" : payloadStr)}");
        break;
    }
}

// ── Views — materialised summary snapshots ─────────────────────────────────────
Console.WriteLine("\n── Views ──");
var views = client.ListViews(patientId);
Console.WriteLine(
    $"  Available: [{string.Join(", ", views.Select(v => $"({v.ViewType}, has_blocks={v.HasBlocks})"))}]");

// Fetch the first view that has content
foreach (var viewMeta in views)
{
    if (!viewMeta.HasBlocks && !viewMeta.HasTemp)
        continue;

    var view = client.GetView(patientId, viewMeta.ViewType);
    Console.WriteLine($"\n  {viewMeta.ViewType}:");

    var blocks = GetJsonArray(view.Content, "blocks");
    foreach (var block in blocks.Take(2))
    {
        var tr = GetObject(block, "template_ref");
        var resultD = GetObject(block, "result");
        var name =
            GetString(tr, "block_id")
            ?? GetString(resultD, "id")
            ?? GetString(resultD, "name")
            ?? "?";
        var text = GetString(resultD, "content") ?? GetString(resultD, "name") ?? "";
        if (text.Length > 200)
            text = text[..200];
        Console.WriteLine($"    [{name}] {text}");
    }

    var temp = GetJsonArray(view.Content, "temp");
    if (temp.Count > 0)
    {
        var preview = string.Join(", ", temp.Take(3).Select(t => t.ToString()));
        Console.WriteLine($"    TEMP entries: [{preview}]");
    }

    break;
}

// ── Recent logs ────────────────────────────────────────────────────────────────
Console.WriteLine("\n── Recent logs (last 5) ──");
var logs = client.GetLogs(patientId, limit: 5);
Console.WriteLine($"  Total logs on record: {logs.Count}");
foreach (var entry in logs.Logs)
{
    // timestamp: when the event happened. ingested_at: when the platform received it —
    // these can differ for backfilled or delayed-sync events.
    var keys = string.Join(", ", (entry.Payload ?? new Dictionary<string, object?>()).Keys);
    Console.WriteLine(
        $"  timestamp={entry.Timestamp ?? "?"}  ingested_at={entry.IngestedAt ?? "?"}  " +
        $"{entry.Type}  payload keys: [{keys}]");
}

// ── State events ───────────────────────────────────────────────────────────────
Console.WriteLine("\n── Recent state events (last 5) ──");
var events = client.GetEvents(patientId, limit: 5);
Console.WriteLine($"  Total events: {events.Count}");
foreach (var evt in events.Events)
{
    Console.WriteLine($"  {evt.TriggeredAt ?? "?"}  {evt.LogType}  status={evt.Status}");
}

// ── Memories ───────────────────────────────────────────────────────────────────
Console.WriteLine("\n── Memories (first 5) ──");
var memories = client.ReadMemories(patientId, limit: 5);
Console.WriteLine($"  Total memories: {memories.Count}");
foreach (var mem in memories.Results)
{
    var content = mem.Content.Length > 120 ? mem.Content[..120] : mem.Content;
    Console.WriteLine($"  [{mem.MemoryId}] {content}");
}

// ── helpers for ViewResult.Content (values are often JsonElement) ──────────────

static List<JsonElement> GetJsonArray(Dictionary<string, object?> content, string key)
{
    if (!content.TryGetValue(key, out var raw) || raw is null)
        return [];

    if (raw is JsonElement el)
    {
        if (el.ValueKind != JsonValueKind.Array)
            return [];
        return el.EnumerateArray().Select(e => e.Clone()).ToList();
    }

    if (raw is IEnumerable<object?> list)
    {
        return list.Select(item =>
        {
            if (item is JsonElement je)
                return je.Clone();
            var json = JsonSerializer.Serialize(item);
            return JsonSerializer.Deserialize<JsonElement>(json);
        }).ToList();
    }

    return [];
}

static JsonElement GetObject(JsonElement parent, string key)
{
    if (parent.ValueKind == JsonValueKind.Object
        && parent.TryGetProperty(key, out var child)
        && child.ValueKind == JsonValueKind.Object)
    {
        return child;
    }

    return default;
}

static string? GetString(JsonElement obj, string key)
{
    if (obj.ValueKind != JsonValueKind.Object || !obj.TryGetProperty(key, out var prop))
        return null;
    return prop.ValueKind switch
    {
        JsonValueKind.String => prop.GetString(),
        JsonValueKind.Null or JsonValueKind.Undefined => null,
        _ => prop.ToString(),
    };
}
