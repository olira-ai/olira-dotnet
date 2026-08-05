#nullable enable

using System.Collections.Frozen;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Olira;

/// <summary>
/// Local JSONL validation for historical data ingestion.
/// Validates structure and field presence entirely offline — no network calls.
/// </summary>
public static class Validation
{
    private const long DefaultMaxFileBytes = 100L * 1024 * 1024;

    private static readonly FrozenSet<string> ValidEventTypes = typeof(OliraLogType)
        .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
        .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
        .Select(f => (string)f.GetRawConstantValue()!)
        .ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> DocLogTypes =
        FrozenSet.ToFrozenSet(["unstructured_report", "clinical_note"], StringComparer.Ordinal);

    private static readonly string[] AnchorFields =
        ["external_identifiers", "email", "phone_number", "first_name", "last_name", "date_of_birth"];

    private static readonly Regex UuidRe = new(
        @"^[0-9a-f]{8}-?[0-9a-f]{4}-?[0-9a-f]{4}-?[0-9a-f]{4}-?[0-9a-f]{12}$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ObjectIdRe = new(
        @"^[0-9a-f]{24}$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Validate a JSONL ingestion file locally before uploading.
    /// Returns one <see cref="IngestionRowError"/> per problem; empty means all local checks passed.
    /// </summary>
    public static IReadOnlyList<IngestionRowError> ValidateIngestionFile(
        string path,
        long maxFileBytes = DefaultMaxFileBytes)
    {
        var errors = new List<IngestionRowError>();
        var fileInfo = new FileInfo(path);
        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException("Ingestion file not found", path);
        }

        if (fileInfo.Length > maxFileBytes)
        {
            return
            [
                new IngestionRowError
                {
                    Line = 0,
                    Code = "file_too_large",
                    Message =
                        $"File is {fileInfo.Length / (1024.0 * 1024.0):F1} MB, exceeds the " +
                        $"{maxFileBytes / (1024 * 1024)} MB limit. " +
                        "Split into smaller batches and submit as separate jobs.",
                },
            ];
        }

        var knownPatientIds = new HashSet<string>(StringComparer.Ordinal);
        var parsed = new List<(int Line, string RecordType, Dictionary<string, JsonElement> Data)>();

        var lineNum = 0;
        foreach (var rawLine in File.ReadLines(path))
        {
            lineNum++;
            var raw = rawLine.Trim();
            if (raw.Length == 0)
            {
                continue;
            }

            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(raw);
            }
            catch (JsonException ex)
            {
                errors.Add(new IngestionRowError
                {
                    Line = lineNum,
                    Code = "invalid_json",
                    Message = $"Line is not valid JSON: {ex.Message}",
                });
                continue;
            }

            using (doc)
            {
                if (doc.RootElement.ValueKind != JsonValueKind.Object)
                {
                    errors.Add(new IngestionRowError
                    {
                        Line = lineNum,
                        Code = "invalid_json",
                        Message = "Each line must be a JSON object",
                    });
                    continue;
                }

                var row = doc.RootElement;
                var recordType = row.TryGetProperty("type", out var typeEl) && typeEl.ValueKind == JsonValueKind.String
                    ? typeEl.GetString()
                    : null;

                if (recordType is not ("patient" or "log" or "document"))
                {
                    errors.Add(new IngestionRowError
                    {
                        Line = lineNum,
                        Code = "unknown_record_type",
                        Message = $"type must be 'patient', 'log', or 'document', got {JsonRepr(recordType)}",
                    });
                    continue;
                }

                if (!row.TryGetProperty("data", out var dataEl) || dataEl.ValueKind != JsonValueKind.Object)
                {
                    errors.Add(new IngestionRowError
                    {
                        Line = lineNum,
                        Code = "missing_data",
                        Message = "Record must have a 'data' object",
                    });
                    continue;
                }

                var data = CloneObject(dataEl);

                if (recordType == "patient")
                {
                    if (!HasAnchor(data))
                    {
                        errors.Add(new IngestionRowError
                        {
                            Line = lineNum,
                            Code = "missing_anchor",
                            Message =
                                "Patient record must have at least one of: " +
                                "external_identifiers, email, phone_number, first_name, last_name, date_of_birth",
                        });
                    }

                    CollectExternalIds(data, knownPatientIds);
                }

                parsed.Add((lineNum, recordType, data));
            }
        }

        foreach (var (line, recordType, data) in parsed)
        {
            if (recordType == "document")
            {
                errors.AddRange(ValidateDocumentRow(data, line, knownPatientIds));
                continue;
            }

            if (recordType != "log")
            {
                continue;
            }

            ValidateLogRow(data, line, knownPatientIds, errors);
        }

        return errors;
    }

    /// <summary>
    /// Validate a list of <see cref="IngestRecord"/> objects locally before submitting inline.
    /// Line numbers are 1-indexed positions in the list.
    /// </summary>
    public static IReadOnlyList<IngestionRowError> ValidateIngestionRecords(IReadOnlyList<IngestRecord> records)
    {
        var errors = new List<IngestionRowError>();
        var knownPatientIds = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < records.Count; i++)
        {
            var line = i + 1;
            var record = records[i];
            var data = ToElementDict(record.Data);

            if (record.Type == "patient")
            {
                if (!HasAnchor(data))
                {
                    errors.Add(new IngestionRowError
                    {
                        Line = line,
                        Code = "missing_anchor",
                        Message =
                            "Patient record must have at least one of: " +
                            "external_identifiers, email, phone_number, first_name, last_name, date_of_birth",
                    });
                }

                CollectExternalIds(data, knownPatientIds);
            }
            else if (record.Type is not ("patient" or "log" or "document"))
            {
                errors.Add(new IngestionRowError
                {
                    Line = line,
                    Code = "unknown_record_type",
                    Message = $"type must be 'patient', 'log', or 'document', got {JsonRepr(record.Type)}",
                });
            }
        }

        for (var i = 0; i < records.Count; i++)
        {
            var line = i + 1;
            var record = records[i];
            var data = ToElementDict(record.Data);

            if (record.Type == "document")
            {
                errors.AddRange(ValidateDocumentRow(data, line, knownPatientIds));
                continue;
            }

            if (record.Type != "log")
            {
                continue;
            }

            ValidateLogRow(data, line, knownPatientIds, errors);
        }

        return errors;
    }

    private static void ValidateLogRow(
        Dictionary<string, JsonElement> data,
        int line,
        HashSet<string> knownPatientIds,
        List<IngestionRowError> errors)
    {
        if (!TryGetString(data, "event_type", out var et) || string.IsNullOrEmpty(et))
        {
            if (data.TryGetValue("event_type", out var etEl) && etEl.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined
                && etEl.ValueKind != JsonValueKind.String)
            {
                errors.Add(new IngestionRowError
                {
                    Line = line,
                    Code = "invalid_event_type",
                    Message = "Log record 'event_type' must be a string",
                });
            }
            else
            {
                errors.Add(new IngestionRowError
                {
                    Line = line,
                    Code = "missing_event_type",
                    Message = "Log record must have an 'event_type' field",
                });
            }
        }
        else if (!ValidEventTypes.Contains(et) && !LooksOrgNative(et))
        {
            var suggestion = Suggest(et);
            var msg = $"Unknown event_type {JsonRepr(et)}";
            if (suggestion is not null)
            {
                msg += $" — did you mean {JsonRepr(suggestion)}?";
            }

            errors.Add(new IngestionRowError
            {
                Line = line,
                Code = "unknown_event_type",
                Message = msg,
            });
        }

        if (!TryGetString(data, "patient_id", out var pid) || string.IsNullOrEmpty(pid))
        {
            errors.Add(new IngestionRowError
            {
                Line = line,
                Code = "missing_patient_id",
                Message = "Log record must have a 'patient_id' field",
            });
        }
        else if (!knownPatientIds.Contains(pid) && !LooksLikeUuid(pid))
        {
            errors.Add(new IngestionRowError
            {
                Line = line,
                Code = "patient_id_not_in_file",
                Message =
                    $"patient_id {JsonRepr(pid)} not found in any patient record in this file. " +
                    "If the patient was created separately (e.g. via create_patients_batch) " +
                    "it will resolve server-side and is not an error.",
            });
        }

        if (!TryGetString(data, "timestamp", out var ts) || string.IsNullOrEmpty(ts))
        {
            errors.Add(new IngestionRowError
            {
                Line = line,
                Code = "missing_timestamp",
                Message = "Log record must have a 'timestamp' field",
            });
        }
        else if (!ParseIso(ts))
        {
            errors.Add(new IngestionRowError
            {
                Line = line,
                Code = "invalid_timestamp",
                Message =
                    $"timestamp {JsonRepr(ts)} is not a valid ISO 8601 datetime. " +
                    "Use format: '2025-01-15T09:00:00Z'",
            });
        }

        errors.AddRange(ValidateLogTrace(data, line));
    }

    private static IEnumerable<IngestionRowError> ValidateDocumentRow(
        Dictionary<string, JsonElement> data,
        int line,
        HashSet<string> knownPatientIds)
    {
        foreach (var field in new[] { "ref_id", "patient_id", "s3_key", "log_type", "timestamp" })
        {
            if (!TryGetString(data, field, out var value) || string.IsNullOrEmpty(value))
            {
                yield return new IngestionRowError
                {
                    Line = line,
                    Code = $"missing_{field}",
                    Message = $"Document record must have a '{field}' field",
                };
            }
        }

        TryGetString(data, "log_type", out var logType);
        if (!string.IsNullOrEmpty(logType) && !DocLogTypes.Contains(logType))
        {
            yield return new IngestionRowError
            {
                Line = line,
                Code = "invalid_log_type",
                Message = "document log_type must be 'unstructured_report' or 'clinical_note'",
            };
        }
        else if (logType == "unstructured_report"
                 && (!TryGetString(data, "document_type", out var dt) || string.IsNullOrEmpty(dt)))
        {
            yield return new IngestionRowError
            {
                Line = line,
                Code = "missing_document_type",
                Message = "document_type is required for unstructured_report",
            };
        }
        else if (logType == "clinical_note")
        {
            if (!TryGetString(data, "note_type", out var nt) || string.IsNullOrEmpty(nt))
            {
                yield return new IngestionRowError
                {
                    Line = line,
                    Code = "missing_note_type",
                    Message = "note_type is required for clinical_note",
                };
            }

            if (!data.TryGetValue("source", out var source) || source.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                yield return new IngestionRowError
                {
                    Line = line,
                    Code = "missing_source",
                    Message = "source is required for clinical_note",
                };
            }
        }

        if (TryGetString(data, "timestamp", out var ts) && !string.IsNullOrEmpty(ts) && !ParseIso(ts))
        {
            yield return new IngestionRowError
            {
                Line = line,
                Code = "invalid_timestamp",
                Message = $"timestamp {JsonRepr(ts)} is not a valid ISO 8601 datetime",
            };
        }

        if (TryGetString(data, "patient_id", out var pid)
            && !string.IsNullOrEmpty(pid)
            && !knownPatientIds.Contains(pid)
            && !LooksLikeUuid(pid))
        {
            yield return new IngestionRowError
            {
                Line = line,
                Code = "patient_id_not_in_file",
                Message =
                    $"patient_id {JsonRepr(pid)} not found in any patient record in this file. " +
                    "If the patient was created separately it will resolve server-side.",
            };
        }
    }

    private static IEnumerable<IngestionRowError> ValidateLogTrace(Dictionary<string, JsonElement> data, int line)
    {
        if (!data.TryGetValue("trace", out var trace) || trace.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            yield break;
        }

        if (trace.ValueKind != JsonValueKind.Object)
        {
            yield return new IngestionRowError
            {
                Line = line,
                Code = "invalid_trace",
                Message = "trace must be an object with object_type and object_id",
            };
            yield break;
        }

        var objectType = trace.TryGetProperty("object_type", out var ot) && ot.ValueKind == JsonValueKind.String
            ? ot.GetString()
            : null;
        var objectId = trace.TryGetProperty("object_id", out var oi) && oi.ValueKind == JsonValueKind.String
            ? oi.GetString()
            : null;

        if (string.IsNullOrWhiteSpace(objectType) || string.IsNullOrWhiteSpace(objectId))
        {
            yield return new IngestionRowError
            {
                Line = line,
                Code = "invalid_trace",
                Message = "trace requires both object_type and object_id as non-empty strings",
            };
        }
    }

    private static bool LooksOrgNative(string et)
    {
        if (et.Contains('@', StringComparison.Ordinal))
        {
            return true;
        }

        if (ValidEventTypes.Contains(et))
        {
            return false;
        }

        return et.Contains('_', StringComparison.Ordinal) && Suggest(et) is null;
    }

    private static bool HasAnchor(Dictionary<string, JsonElement> data)
    {
        if (data.TryGetValue("external_identifiers", out var ext)
            && ext.ValueKind == JsonValueKind.Array
            && ext.GetArrayLength() > 0)
        {
            return true;
        }

        foreach (var field in AnchorFields.Skip(1))
        {
            if (TryGetString(data, field, out var v) && !string.IsNullOrEmpty(v))
            {
                return true;
            }
        }

        return false;
    }

    private static void CollectExternalIds(Dictionary<string, JsonElement> data, HashSet<string> known)
    {
        if (!data.TryGetValue("external_identifiers", out var ext) || ext.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var item in ext.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object
                && item.TryGetProperty("value", out var value)
                && value.ValueKind is JsonValueKind.String or JsonValueKind.Number)
            {
                known.Add(value.ToString());
            }
        }
    }

    private static bool ParseIso(string value)
    {
        var normalized = value.EndsWith('Z')
            ? value.TrimEnd('Z') + "+00:00"
            : value;

        return DateTimeOffset.TryParse(
                   normalized,
                   CultureInfo.InvariantCulture,
                   DateTimeStyles.RoundtripKind,
                   out _)
               || DateTimeOffset.TryParse(
                   value.Replace("Z", "+00:00", StringComparison.Ordinal),
                   CultureInfo.InvariantCulture,
                   DateTimeStyles.RoundtripKind,
                   out _);
    }

    private static bool LooksLikeUuid(string value) =>
        ObjectIdRe.IsMatch(value) || UuidRe.IsMatch(value);

    private static string? Suggest(string eventType)
    {
        string? best = null;
        var bestDist = 3;
        foreach (var known in ValidEventTypes)
        {
            var d = Levenshtein(eventType, known);
            if (d < bestDist)
            {
                bestDist = d;
                best = known;
            }
        }

        return best;
    }

    private static int Levenshtein(string a, string b)
    {
        if (a.Length > b.Length)
        {
            (a, b) = (b, a);
        }

        var row = Enumerable.Range(0, a.Length + 1).ToArray();
        for (var j = 1; j <= b.Length; j++)
        {
            var prev = j;
            for (var i = 1; i <= a.Length; i++)
            {
                var curr = a[i - 1] == b[j - 1] ? row[i - 1] : 1 + Math.Min(Math.Min(row[i - 1], row[i]), prev);
                row[i - 1] = prev;
                prev = curr;
            }

            row[a.Length] = prev;
        }

        return row[a.Length];
    }

    private static Dictionary<string, JsonElement> CloneObject(JsonElement element)
    {
        var dict = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var prop in element.EnumerateObject())
        {
            dict[prop.Name] = prop.Value.Clone();
        }

        return dict;
    }

    private static Dictionary<string, JsonElement> ToElementDict(Dictionary<string, object?>? data)
    {
        if (data is null || data.Count == 0)
        {
            return new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        }

        var json = JsonSerializer.Serialize(data);
        using var doc = JsonDocument.Parse(json);
        return CloneObject(doc.RootElement);
    }

    private static bool TryGetString(Dictionary<string, JsonElement> data, string key, out string? value)
    {
        value = null;
        if (!data.TryGetValue(key, out var el))
        {
            return false;
        }

        if (el.ValueKind == JsonValueKind.String)
        {
            value = el.GetString();
            return true;
        }

        if (el.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
        {
            value = el.ToString();
            return true;
        }

        return false;
    }

    private static string JsonRepr(string? value) =>
        value is null ? "null" : JsonSerializer.Serialize(value);
}
