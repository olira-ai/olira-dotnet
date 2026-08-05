#nullable enable

using System.Globalization;
using System.Text.Json;
using Parquet.Schema;
using Parquet.Serialization;

namespace Olira;

/// <summary>Parquet serialization helpers for signal records (mirrors Python pyarrow path).</summary>
public static partial class Signals
{
    /// <summary>
    /// Serialize measurement rows (each typically with a <c>ts</c> key) to a Parquet blob.
    /// Column types are inferred from the first non-null value in each column, matching
    /// <c>pyarrow.Table.from_pylist</c> behaviour used by the Python SDK.
    /// </summary>
    public static byte[] SerializeSignalRecords(IReadOnlyList<Dictionary<string, object?>> records)
    {
        if (records is null || records.Count == 0)
        {
            throw new ValidationError("records must be a non-empty list");
        }

        var columnOrder = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in records)
        {
            foreach (var key in row.Keys)
            {
                if (seen.Add(key))
                {
                    columnOrder.Add(key);
                }
            }
        }

        if (columnOrder.Count == 0)
        {
            throw new ValidationError("records must contain at least one column");
        }

        var normalized = new List<IDictionary<string, object?>>(records.Count);
        foreach (var row in records)
        {
            var dict = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var col in columnOrder)
            {
                dict[col] = row.TryGetValue(col, out var value) ? NormalizeCell(value) : null;
            }

            normalized.Add(dict);
        }

        var fields = new Field[columnOrder.Count];
        for (var i = 0; i < columnOrder.Count; i++)
        {
            var col = columnOrder[i];
            var sample = normalized.Select(r => r[col]).FirstOrDefault(v => v is not null);
            fields[i] = InferField(col, sample);
        }

        // Coerce every cell to the inferred CLR type so Parquet.Net does not see mixed types.
        for (var i = 0; i < columnOrder.Count; i++)
        {
            var col = columnOrder[i];
            var field = (DataField)fields[i];
            var clr = Nullable.GetUnderlyingType(field.ClrNullableIfHasNullsType) ?? field.ClrType;
            foreach (var row in normalized)
            {
                if (row[col] is null)
                {
                    continue;
                }

                row[col] = Coerce(row[col], clr);
            }
        }

        var schema = new ParquetSchema(fields);
        using var ms = new MemoryStream();
        ParquetSerializer.SerializeUntypedAsync(normalized, schema, ms).GetAwaiter().GetResult();
        return ms.ToArray();
    }

    private static object? NormalizeCell(object? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value is JsonElement el)
        {
            return NormalizeJsonElement(el);
        }

        if (value is DateTimeOffset dto)
        {
            return dto.UtcDateTime;
        }

        if (value is DateTime dt)
        {
            return dt.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(dt, DateTimeKind.Utc)
                : dt.ToUniversalTime();
        }

        if (value is float f)
        {
            return (double)f;
        }

        if (value is decimal m)
        {
            return (double)m;
        }

        if (value is byte or sbyte or short or ushort or int or uint)
        {
            return Convert.ToInt64(value, CultureInfo.InvariantCulture);
        }

        return value;
    }

    private static object? NormalizeJsonElement(JsonElement el) =>
        el.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            JsonValueKind.String => TryParseDateTime(el.GetString(), out var dt) ? dt : el.GetString(),
            JsonValueKind.Number => el.TryGetInt64(out var l)
                ? l
                : el.TryGetDouble(out var d)
                    ? d
                    : el.GetRawText(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => el.GetRawText(),
        };

    private static bool TryParseDateTime(string? s, out DateTime dt)
    {
        dt = default;
        if (string.IsNullOrWhiteSpace(s))
        {
            return false;
        }

        if (DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dto))
        {
            dt = dto.UtcDateTime;
            return true;
        }

        return false;
    }

    private static DataField InferField(string name, object? sample)
    {
        return sample switch
        {
            null => new DataField<string>(name),
            DateTime => new DateTimeDataField(name, DateTimeFormat.DateAndTimeMicros, isAdjustedToUTC: true),
            bool => new DataField<bool>(name),
            byte or sbyte or short or ushort or int or uint or long or ulong => new DataField<long>(name),
            float or double or decimal => new DataField<double>(name),
            string => new DataField<string>(name),
            _ => new DataField<string>(name),
        };
    }

    private static object? Coerce(object? value, Type clr)
    {
        if (value is null)
        {
            return null;
        }

        if (clr == typeof(DateTime))
        {
            return value switch
            {
                DateTime dt => dt.Kind == DateTimeKind.Unspecified
                    ? DateTime.SpecifyKind(dt, DateTimeKind.Utc)
                    : dt.ToUniversalTime(),
                DateTimeOffset dto => dto.UtcDateTime,
                string s when TryParseDateTime(s, out var parsed) => parsed,
                _ => Convert.ToDateTime(value, CultureInfo.InvariantCulture).ToUniversalTime(),
            };
        }

        if (clr == typeof(bool))
        {
            return Convert.ToBoolean(value, CultureInfo.InvariantCulture);
        }

        if (clr == typeof(long))
        {
            return Convert.ToInt64(value, CultureInfo.InvariantCulture);
        }

        if (clr == typeof(double))
        {
            return Convert.ToDouble(value, CultureInfo.InvariantCulture);
        }

        if (clr == typeof(string))
        {
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        return value;
    }
}
