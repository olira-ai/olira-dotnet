#nullable enable

namespace Olira;

public sealed partial class OliraClient
{
    /// <summary>Register a new org-native event subtype. Requires api:org-config scope.</summary>
    public SchemaRegistrationResult RegisterSchema(
        string subtype,
        string description = "",
        IReadOnlyList<Dictionary<string, object?>>? inputExamples = null,
        Dictionary<string, object?>? schema = null,
        Dictionary<string, object?>? mapping = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var body = new Dictionary<string, object?>
        {
            ["subtype"] = subtype,
            ["description"] = description,
        };
        if (inputExamples is not null)
        {
            body["input_examples"] = inputExamples;
        }

        if (schema is not null)
        {
            body["payload_schema"] = schema;
        }

        if (mapping is not null)
        {
            body["mapping"] = mapping;
        }

        return _transport.RegisterSchema(body);
    }

    /// <summary>List every org-native subtype you've registered.</summary>
    public List<SchemaSummary> ListSchemas()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _transport.ListSchemas();
    }

    /// <summary>Get a subtype's full version history.</summary>
    public SchemaDetail GetSchema(string subtype)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _transport.GetSchema(subtype);
    }

    /// <summary>Dry-run a schema/mapping over sample payloads — no writes.</summary>
    public SchemaCheckResult CheckSchema(
        IReadOnlyList<Dictionary<string, object?>> examples,
        string? subtype = null,
        int? version = null,
        Dictionary<string, object?>? schema = null,
        Dictionary<string, object?>? mapping = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var body = new Dictionary<string, object?> { ["examples"] = examples };
        if (subtype is not null)
        {
            body["subtype"] = subtype;
        }

        if (version is not null)
        {
            body["version"] = version;
        }

        if (schema is not null)
        {
            body["payload_schema"] = schema;
        }

        if (mapping is not null)
        {
            body["mapping"] = mapping;
        }

        return _transport.CheckSchema(body);
    }

    /// <summary>Propose a schema/mapping change for a subtype you've already registered.</summary>
    public SchemaRegistrationResult EditSchema(
        string subtype,
        string? description = null,
        IReadOnlyList<Dictionary<string, object?>>? inputExamples = null,
        Dictionary<string, object?>? schema = null,
        Dictionary<string, object?>? mapping = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var body = new Dictionary<string, object?>();
        if (description is not null)
        {
            body["description"] = description;
        }

        if (inputExamples is not null)
        {
            body["input_examples"] = inputExamples;
        }

        if (schema is not null)
        {
            body["payload_schema"] = schema;
        }

        if (mapping is not null)
        {
            body["mapping"] = mapping;
        }

        return _transport.EditSchema(subtype, body);
    }

    /// <summary>Deprecate a materialized version, or withdraw a still-pending request.</summary>
    public SchemaActionResult DeprecateSchema(string subtype, int? version = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var parameters = new Dictionary<string, object?>();
        if (version is not null)
        {
            parameters["version"] = version;
        }

        return _transport.DeprecateSchema(subtype, parameters);
    }

    /// <summary>Activate an already-materialized version.</summary>
    public SchemaActionResult ActivateSchemaVersion(string subtype, int version)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _transport.ActivateSchemaVersion(subtype, version);
    }
}
