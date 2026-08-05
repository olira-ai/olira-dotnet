using System.Text.Json;
using Olira;
using Olira.Examples;

/*
 * Olira SDK — Org Schema/Mapping Management
 *
 * Covers the self-service registration flow for org-native event subtypes:
 *   - Register a new subtype (assisted: examples + description only)
 *   - Dry-run check a candidate schema+mapping before registering it at all
 *   - Register a second subtype full_spec (schema + mapping already authored)
 *   - List every subtype you've registered
 *   - View one subtype's full version history
 *   - Edit a still-pending request
 *   - Deprecate (withdraw) a pending request
 *
 * Registering always lands as a pending request — Olira still reviews and manually
 * materializes it into a real, versioned type definition + mapping before there is
 * anything to activate. This example stops short of activation since that requires
 * Olira to have materialized a version out-of-band first.
 *
 * All operations require: api:org-config scope.
 * Run: dotnet run --project 09_OrgSchemaManagement
 */

ExampleEnv.Load();
var apiKey = ExampleEnv.Require("OLIRA_API_KEY");
var baseUrl = ExampleEnv.BaseUrl;

using var client = new OliraClient(
    apiKey: apiKey,
    baseUrl: baseUrl,
    environment: OliraEnv.Development,
    asyncFlush: false,
    timeout: 30.0);

var suffix = Guid.NewGuid().ToString("N")[..8];
var assistedSubtype = $"widget_ping_{suffix}";
var fullSpecSubtype = $"widget_pong_{suffix}";

// ── 1. Dry-run a candidate schema+mapping before registering anything ────────────
Console.WriteLine("\n── 1. Check a candidate schema+mapping (no writes) ─────────────────────");
var schema = new Dictionary<string, object?>
{
    ["type"] = "object",
    ["properties"] = new Dictionary<string, object?>
    {
        ["note"] = new Dictionary<string, object?> { ["type"] = "string" },
    },
};
var mapping = new Dictionary<string, object?>
{
    ["source_root"] = null,
    ["targets"] = new List<Dictionary<string, object?>>
    {
        new()
        {
            ["target_subtype"] = "conversation",
            ["field_mappings"] = new List<Dictionary<string, object?>>
            {
                new() { ["target"] = "channel", ["source"] = "note" },
            },
        },
    },
    ["unmapped_fields_policy"] = "drop",
};
var examples = new List<Dictionary<string, object?>>
{
    new() { ["note"] = "hello" },
};

var check = client.CheckSchema(examples, schema: schema, mapping: mapping);
Console.WriteLine($"  ok = {check.Ok}");
foreach (var exampleResult in check.Results)
{
    Console.WriteLine(
        $"  input={FormatDict(exampleResult.Input)}  ok={exampleResult.Ok}  " +
        $"errors=[{string.Join(", ", exampleResult.Errors)}]");
    foreach (var evt in exampleResult.MappedEvents)
    {
        Console.WriteLine($"    -> {DictGet(evt, "subtype")}: {DictGet(evt, "payload")}");
    }
}

// ── 2. Register a new subtype (assisted: Olira authors the schema+mapping) ──────
Console.WriteLine($"\n── 2. Register '{assistedSubtype}' (assisted) ──────────────────────────");
var registration = client.RegisterSchema(
    subtype: assistedSubtype,
    description: "Example ping event, registered by 09_OrgSchemaManagement",
    inputExamples: examples);
Console.WriteLine($"  registration_id  = {registration.RegistrationId}");
Console.WriteLine($"  submission_mode  = {registration.SubmissionMode}");
Console.WriteLine($"  status           = {registration.Status}");

// ── 3. Register a second subtype full_spec (schema+mapping already authored) ────
Console.WriteLine($"\n── 3. Register '{fullSpecSubtype}' (full_spec) ────────────────────────");
var fullSpecRegistration = client.RegisterSchema(
    subtype: fullSpecSubtype,
    description: "Example pong event, registered by 09_OrgSchemaManagement",
    inputExamples: examples,
    schema: schema,
    mapping: mapping);
Console.WriteLine($"  submission_mode  = {fullSpecRegistration.SubmissionMode}");
object? selfCheckOk = null;
if (fullSpecRegistration.SelfCheck is not null
    && fullSpecRegistration.SelfCheck.TryGetValue("ok", out var okVal))
{
    selfCheckOk = Unwrap(okVal);
}

Console.WriteLine($"  self_check.ok    = {selfCheckOk}");

// ── 4. List every subtype you've registered ──────────────────────────────────────
Console.WriteLine("\n── 4. List schemas ───────────────────────────────────────────────────────");
var schemas = client.ListSchemas();
Console.WriteLine($"  total registered subtypes: {schemas.Count}");
foreach (var summary in schemas)
{
    if (summary.Subtype is var s && (s == assistedSubtype || s == fullSpecSubtype))
    {
        Console.WriteLine(
            $"  • {summary.Subtype}  status={summary.Status}  active_version={summary.ActiveVersion}");
    }
}

// ── 5. View one subtype's full version history ───────────────────────────────────
Console.WriteLine($"\n── 5. View '{assistedSubtype}' ──────────────────────────────────────────");
var detail = client.GetSchema(assistedSubtype);
Console.WriteLine($"  status = {detail.Status}");
foreach (var version in detail.Versions)
{
    Console.WriteLine($"  • v{version.Version}  status={version.Status}  source={version.Source}");
}

// ── 6. Edit the still-pending request ────────────────────────────────────────────
Console.WriteLine($"\n── 6. Edit '{assistedSubtype}' ──────────────────────────────────────────");
var edited = client.EditSchema(
    subtype: assistedSubtype,
    description: "Updated description via EditSchema()");
Console.WriteLine($"  target_version  = {edited.TargetVersion}");
Console.WriteLine($"  status          = {edited.Status}");

// ── 7. Deprecate (withdraw) both pending requests ────────────────────────────────
Console.WriteLine("\n── 7. Deprecate (withdraw) pending requests ─────────────────────────────");
foreach (var subtype in new[] { assistedSubtype, fullSpecSubtype })
{
    var deprecateResult = client.DeprecateSchema(subtype);
    Console.WriteLine($"  {subtype}: status={deprecateResult.Status}");
}

Console.WriteLine("\nDone.");

static object? Unwrap(object? value) =>
    value is JsonElement je
        ? je.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            JsonValueKind.String => je.GetString(),
            JsonValueKind.Number when je.TryGetInt64(out var l) => l,
            JsonValueKind.Number when je.TryGetDouble(out var d) => d,
            _ => je.ToString(),
        }
        : value;

static object? DictGet(Dictionary<string, object?> dict, string key) =>
    dict.TryGetValue(key, out var v) ? Unwrap(v) : null;

static string FormatDict(Dictionary<string, object?> dict) =>
    "{" + string.Join(", ", dict.Select(kv => $"{kv.Key}: {Unwrap(kv.Value)}")) + "}";
