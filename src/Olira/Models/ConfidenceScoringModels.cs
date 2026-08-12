using System.Text.Json;

namespace Olira;

/// <summary>Result of get/set confidence scoring at org, view, or block scope.</summary>
public sealed class ConfidenceScoringResult
{
    /// <summary>Config scope: org, view, or block.</summary>
    public string Scope { get; set; } = "";

    /// <summary>View summary_type when scope is view or block.</summary>
    public string? SummaryType { get; set; }

    /// <summary>Block id when scope is block.</summary>
    public string? BlockId { get; set; }

    /// <summary>Confidence scoring config payload (scorers / weights), or null when unset.</summary>
    public Dictionary<string, object?>? ConfidenceScoring { get; set; }
}

/// <summary>Builtin confidence scorer ids and client-side patch helpers.</summary>
public static class ConfidenceScorers
{
    public const string Coverage = "builtin.coverage";
    public const string Freshness = "builtin.freshness";
    public const string Certainty = "builtin.certainty";
    public const string Consistency = "builtin.consistency";
    public const string EvidenceDensity = "builtin.evidence_density";

    /// <summary>Normalize to scorers-primary shape (drops deprecated flat params).</summary>
    public static Dictionary<string, object?> Normalize(Dictionary<string, object?>? raw)
    {
        if (raw is null)
        {
            return new Dictionary<string, object?>
            {
                ["scorers"] = null,
                ["weights"] = null,
                ["params"] = null,
            };
        }

        object? scorers = raw.TryGetValue("scorers", out var s) ? s : null;
        object? weights = raw.TryGetValue("weights", out var w) ? w : null;
        object? paramsObj = raw.TryGetValue("params", out var p) ? p : null;

        if (scorers is null && paramsObj is Dictionary<string, object?> legacy)
        {
            scorers = LegacyParamsToScorers(legacy);
            if (weights is null && legacy.TryGetValue("weights", out var lw))
            {
                weights = lw;
            }
        }

        return new Dictionary<string, object?>
        {
            ["scorers"] = scorers,
            ["weights"] = weights,
            ["params"] = null,
        };
    }

    /// <summary>Replace one scorer's params (pass null params to remove).</summary>
    public static Dictionary<string, object?> PatchScorer(
        Dictionary<string, object?>? raw,
        string scorerId,
        Dictionary<string, object?>? @params)
    {
        var cfg = Normalize(raw);
        var kept = new List<Dictionary<string, object?>>();
        if (cfg["scorers"] is IEnumerable<object?> list)
        {
            foreach (var item in list)
            {
                var entry = AsDict(item);
                if (entry is null)
                {
                    continue;
                }

                if (!string.Equals(AsString(entry.GetValueOrDefault("scorer_id")), scorerId, StringComparison.Ordinal))
                {
                    kept.Add(entry);
                }
            }
        }

        if (@params is not null)
        {
            kept.Add(new Dictionary<string, object?>
            {
                ["scorer_id"] = scorerId,
                ["params"] = @params,
            });
        }

        return new Dictionary<string, object?>
        {
            ["scorers"] = kept.Count > 0 ? kept : null,
            ["weights"] = cfg["weights"],
            ["params"] = null,
        };
    }

    /// <summary>Set overall weights (first-order; not a scorer).</summary>
    public static Dictionary<string, object?> SetWeights(
        Dictionary<string, object?>? raw,
        Dictionary<string, object?>? weights)
    {
        var cfg = Normalize(raw);
        return new Dictionary<string, object?>
        {
            ["scorers"] = cfg["scorers"],
            ["weights"] = weights,
            ["params"] = null,
        };
    }

    /// <summary>Read params for one scorer, or null if unset.</summary>
    public static Dictionary<string, object?>? GetScorerParams(
        Dictionary<string, object?>? raw,
        string scorerId)
    {
        var cfg = Normalize(raw);
        if (cfg["scorers"] is not IEnumerable<object?> list)
        {
            return null;
        }

        foreach (var item in list)
        {
            var entry = AsDict(item);
            if (entry is null)
            {
                continue;
            }

            if (string.Equals(AsString(entry.GetValueOrDefault("scorer_id")), scorerId, StringComparison.Ordinal))
            {
                return AsDict(entry.GetValueOrDefault("params")) ?? new Dictionary<string, object?>();
            }
        }

        return null;
    }

    private static List<Dictionary<string, object?>>? LegacyParamsToScorers(Dictionary<string, object?> @params)
    {
        var outList = new List<Dictionary<string, object?>>();
        if (@params.TryGetValue("freshness_zero_days", out var fz) && fz is not null)
        {
            outList.Add(new Dictionary<string, object?>
            {
                ["scorer_id"] = Freshness,
                ["params"] = new Dictionary<string, object?> { ["freshness_zero_days"] = fz },
            });
        }

        if (@params.TryGetValue("evidence_density_divisor", out var ed) && ed is not null)
        {
            outList.Add(new Dictionary<string, object?>
            {
                ["scorer_id"] = EvidenceDensity,
                ["params"] = new Dictionary<string, object?> { ["evidence_density_divisor"] = ed },
            });
        }

        var cert = new Dictionary<string, object?>();
        if (@params.TryGetValue("certainty_rubric", out var cr) && cr is not null)
        {
            cert["certainty_rubric"] = cr;
        }

        if (@params.TryGetValue("agentic_trajectory_alpha", out var alpha) && alpha is not null)
        {
            cert["agentic_trajectory_alpha"] = alpha;
        }

        if (cert.Count > 0)
        {
            outList.Add(new Dictionary<string, object?> { ["scorer_id"] = Certainty, ["params"] = cert });
        }

        var cons = new Dictionary<string, object?>();
        if (@params.TryGetValue("consistency_rubric", out var csr) && csr is not null)
        {
            cons["consistency_rubric"] = csr;
        }

        if (alpha is not null)
        {
            cons["agentic_trajectory_alpha"] = alpha;
        }

        if (cons.Count > 0)
        {
            outList.Add(new Dictionary<string, object?> { ["scorer_id"] = Consistency, ["params"] = cons });
        }

        return outList.Count > 0 ? outList : null;
    }

    private static Dictionary<string, object?>? AsDict(object? value)
    {
        if (value is Dictionary<string, object?> d)
        {
            return d;
        }

        if (value is JsonElement je && je.ValueKind == JsonValueKind.Object)
        {
            var dict = new Dictionary<string, object?>();
            foreach (var prop in je.EnumerateObject())
            {
                dict[prop.Name] = prop.Value.ValueKind switch
                {
                    JsonValueKind.Null => null,
                    JsonValueKind.Object or JsonValueKind.Array => prop.Value.Clone(),
                    JsonValueKind.String => prop.Value.GetString(),
                    JsonValueKind.Number => prop.Value.TryGetInt64(out var l) ? l : prop.Value.GetDouble(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    _ => prop.Value.ToString(),
                };
            }

            return dict;
        }

        return null;
    }

    private static string? AsString(object? value) =>
        value switch
        {
            null => null,
            string s => s,
            JsonElement je when je.ValueKind == JsonValueKind.String => je.GetString(),
            _ => value.ToString(),
        };
}
