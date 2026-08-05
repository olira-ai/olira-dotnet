#nullable enable

using System.Text.Json;
using Olira.Internal;

namespace Olira;

/// <summary>Field expression helper for <c>Or</c> / <c>And</c> sub-conditions.</summary>
public sealed class F
{
    private readonly string _field;

    /// <summary>Creates a field expression for <paramref name="field"/>.</summary>
    public F(string field) => _field = field;

    public Dictionary<string, object?> Eq(object? v) => Node("eq", v);
    public Dictionary<string, object?> Neq(object? v) => Node("neq", v);
    public Dictionary<string, object?> Gt(object? v) => Node("gt", v);
    public Dictionary<string, object?> Gte(object? v) => Node("gte", v);
    public Dictionary<string, object?> Lt(object? v) => Node("lt", v);
    public Dictionary<string, object?> Lte(object? v) => Node("lte", v);

    public Dictionary<string, object?> In(IEnumerable<object?> values) =>
        Node("in", values.ToList());

    public Dictionary<string, object?> Nin(IEnumerable<object?> values) =>
        Node("nin", values.ToList());

    public Dictionary<string, object?> Like(string pattern) => Node("like", pattern);
    public Dictionary<string, object?> ILike(string pattern) => Node("ilike", pattern);
    public Dictionary<string, object?> Is(object? v) => Node("is", v);
    public Dictionary<string, object?> Exists(bool present = true) => Node("exists", present);
    public Dictionary<string, object?> Contains(object? v) => Node("contains", v);

    private Dictionary<string, object?> Node(string op, object? value) =>
        new()
        {
            ["field"] = _field,
            ["op"] = op,
            ["value"] = value,
        };
}

/// <summary>
/// Fluent log query builder — compiles to POST /v1/state/.../logs/query DSL.
/// Sync execute methods; use <see cref="ExecuteAsync"/> for async I/O.
/// </summary>
public sealed class LogQuery
{
    private static readonly HashSet<string> Ops = new(StringComparer.Ordinal)
    {
        "eq", "neq", "gt", "gte", "lt", "lte", "in", "nin", "like", "ilike", "is", "exists", "contains",
    };

    private readonly HttpTransport _transport;
    private readonly string? _patientId;
    private readonly bool _population;
    private readonly Dictionary<string, object?> _spec = new();

    /// <summary>Creates a query builder.</summary>
    public LogQuery(
        HttpTransport transport,
        string? patientId = null,
        IReadOnlyList<string>? patientIds = null,
        bool population = false)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _patientId = patientId;
        _population = population;
        if (population && patientIds is not null)
        {
            _spec["patient_ids"] = patientIds.ToList();
        }
    }

    public LogQuery Filter(string field, string op, object? value = null)
    {
        if (!Ops.Contains(op))
        {
            throw new ValidationError(
                $"unknown operator {JsonSerializer.Serialize(op)}; expected one of {string.Join(", ", Ops.Order())}");
        }

        EnsureFilter().Add(new Dictionary<string, object?>
        {
            ["field"] = field,
            ["op"] = op,
            ["value"] = value,
        });
        return this;
    }

    public LogQuery Eq(string field, object? value) => Filter(field, "eq", value);
    public LogQuery Neq(string field, object? value) => Filter(field, "neq", value);
    public LogQuery Gt(string field, object? value) => Filter(field, "gt", value);
    public LogQuery Gte(string field, object? value) => Filter(field, "gte", value);
    public LogQuery Lt(string field, object? value) => Filter(field, "lt", value);
    public LogQuery Lte(string field, object? value) => Filter(field, "lte", value);

    public LogQuery In(string field, IEnumerable<object?> values) =>
        Filter(field, "in", values.ToList());

    public LogQuery Nin(string field, IEnumerable<object?> values) =>
        Filter(field, "nin", values.ToList());

    public LogQuery Like(string field, string pattern) => Filter(field, "like", pattern);
    public LogQuery ILike(string field, string pattern) => Filter(field, "ilike", pattern);
    public LogQuery Is(string field, object? value) => Filter(field, "is", value);
    public LogQuery Exists(string field, bool present = true) => Filter(field, "exists", present);
    public LogQuery Contains(string field, object? value) => Filter(field, "contains", value);

    public LogQuery Or(params object[] conditions)
    {
        EnsureFilter().Add(new Dictionary<string, object?> { ["or"] = conditions.ToList() });
        return this;
    }

    public LogQuery And(params object[] conditions)
    {
        EnsureFilter().Add(new Dictionary<string, object?> { ["and"] = conditions.ToList() });
        return this;
    }

    /// <summary>Select bare field paths (Python <c>select("timestamp", "type")</c>).</summary>
    public LogQuery Select(params string[] paths)
    {
        var sel = EnsureSelect();
        foreach (var p in paths)
        {
            sel.Add(new Dictionary<string, object?> { ["path"] = p });
        }

        return this;
    }

    /// <summary>
    /// Select with aliases as a dictionary of alias → path
    /// (Python kwargs form: <c>select(severity="payload.score")</c>).
    /// </summary>
    public LogQuery Select(IReadOnlyDictionary<string, string> aliases)
    {
        ArgumentNullException.ThrowIfNull(aliases);
        var sel = EnsureSelect();
        foreach (var (alias, path) in aliases)
        {
            sel.Add(new Dictionary<string, object?> { ["path"] = path, ["alias"] = alias });
        }

        return this;
    }

    /// <summary>Select with aliases: <c>SelectAliases(("alias", "path"), ...)</c>.</summary>
    public LogQuery SelectAliases(params (string Alias, string Path)[] aliases)
    {
        var map = new Dictionary<string, string>(aliases.Length, StringComparer.Ordinal);
        foreach (var (alias, path) in aliases)
        {
            map[alias] = path;
        }

        return Select(map);
    }

    public LogQuery SelectArray(
        string path,
        object? where = null,
        string? element = null,
        bool first = false,
        string? alias = null)
    {
        var node = new Dictionary<string, object?>
        {
            ["path"] = path,
            ["first"] = first,
        };
        if (!string.IsNullOrEmpty(alias))
        {
            node["alias"] = alias;
        }

        if (where is not null)
        {
            node["where"] = where;
        }

        if (!string.IsNullOrEmpty(element))
        {
            node["element"] = element;
        }

        EnsureSelect().Add(node);
        return this;
    }

    public LogQuery Order(string field, bool desc = false)
    {
        if (!_spec.TryGetValue("order", out var orderObj) || orderObj is not List<object?> order)
        {
            order = [];
            _spec["order"] = order;
        }

        order.Add(new Dictionary<string, object?> { ["field"] = field, ["desc"] = desc });
        return this;
    }

    public LogQuery Limit(int n)
    {
        _spec["limit"] = n;
        return this;
    }

    public LogQuery Offset(int n)
    {
        _spec["offset"] = n;
        return this;
    }

    public LogQuery Range(int start, int end)
    {
        _spec["offset"] = start;
        _spec["limit"] = end - start + 1;
        return this;
    }

    public LogQuery GroupBy(params string[] fields)
    {
        if (!_spec.TryGetValue("group_by", out var gbObj) || gbObj is not List<object?> gb)
        {
            gb = [];
            _spec["group_by"] = gb;
        }

        foreach (var f in fields)
        {
            gb.Add(f);
        }

        return this;
    }

    public LogQuery Agg(string op, string? field, string alias)
    {
        var a = new Dictionary<string, object?> { ["op"] = op, ["alias"] = alias };
        if (field is not null)
        {
            a["field"] = field;
        }

        if (!_spec.TryGetValue("aggregations", out var aggObj) || aggObj is not List<object?> aggs)
        {
            aggs = [];
            _spec["aggregations"] = aggs;
        }

        aggs.Add(a);
        return this;
    }

    public LogQuery CountAgg(string alias = "count") => Agg("count", null, alias);
    public LogQuery Sum(string field, string alias) => Agg("sum", field, alias);
    public LogQuery Avg(string field, string alias) => Agg("avg", field, alias);
    public LogQuery Min(string field, string alias) => Agg("min", field, alias);
    public LogQuery Max(string field, string alias) => Agg("max", field, alias);

    /// <summary>Include total_count and has_more in the response.</summary>
    public LogQuery WithCount()
    {
        _spec["include_total"] = true;
        return this;
    }

    /// <summary>Execute the query and return all matching rows.</summary>
    public LogQueryResult Execute() => Run(count: false);

    /// <summary>Return only the total count.</summary>
    public int Count() => Run(count: true).Count;

    /// <summary>Execute and assert exactly one row is returned.</summary>
    public Dictionary<string, object?> Single()
    {
        if (!_spec.ContainsKey("limit"))
        {
            Limit(2);
        }

        var res = Run();
        if (res.Rows.Count != 1)
        {
            throw new ValidationError($"expected exactly one row, got {res.Rows.Count}");
        }

        return res.Rows[0];
    }

    /// <summary>Execute and return one row or null; raises if more than one row.</summary>
    public Dictionary<string, object?>? MaybeSingle()
    {
        if (!_spec.ContainsKey("limit"))
        {
            Limit(2);
        }

        var res = Run();
        if (res.Rows.Count > 1)
        {
            throw new ValidationError($"expected at most one row, got {res.Rows.Count}");
        }

        return res.Rows.Count == 0 ? null : res.Rows[0];
    }

    /// <summary>Execute and parse rows into typed <see cref="LogEntry"/>.</summary>
    public List<LogEntry> AsLogs() => Run().AsLogs();

    /// <summary>Async execute.</summary>
    public Task<LogQueryResult> ExecuteAsync(CancellationToken cancellationToken = default) =>
        RunAsync(count: false, cancellationToken);

    /// <summary>Async count.</summary>
    public async Task<int> CountAsync(CancellationToken cancellationToken = default) =>
        (await RunAsync(count: true, cancellationToken).ConfigureAwait(false)).Count;

    /// <summary>Async single-row assert.</summary>
    public async Task<Dictionary<string, object?>> SingleAsync(CancellationToken cancellationToken = default)
    {
        if (!_spec.ContainsKey("limit"))
        {
            Limit(2);
        }

        var res = await RunAsync(count: false, cancellationToken).ConfigureAwait(false);
        if (res.Rows.Count != 1)
        {
            throw new ValidationError($"expected exactly one row, got {res.Rows.Count}");
        }

        return res.Rows[0];
    }

    /// <summary>Async maybe-single.</summary>
    public async Task<Dictionary<string, object?>?> MaybeSingleAsync(
        CancellationToken cancellationToken = default)
    {
        if (!_spec.ContainsKey("limit"))
        {
            Limit(2);
        }

        var res = await RunAsync(count: false, cancellationToken).ConfigureAwait(false);
        if (res.Rows.Count > 1)
        {
            throw new ValidationError($"expected at most one row, got {res.Rows.Count}");
        }

        return res.Rows.Count == 0 ? null : res.Rows[0];
    }

    /// <summary>Async as-logs.</summary>
    public async Task<List<LogEntry>> AsLogsAsync(CancellationToken cancellationToken = default) =>
        (await RunAsync(count: false, cancellationToken).ConfigureAwait(false)).AsLogs();

    private Dictionary<string, object?> Build(bool count)
    {
        var spec = new Dictionary<string, object?>(_spec);
        if (count)
        {
            spec["count"] = true;
        }

        return spec;
    }

    private LogQueryResult Run(bool count = false)
    {
        var body = Build(count);
        return _population
            ? _transport.QueryPopulationLogs(body)
            : _transport.QueryLogs(_patientId ?? "", body);
    }

    private Task<LogQueryResult> RunAsync(bool count, CancellationToken cancellationToken) =>
        _population
            ? _transport.QueryPopulationLogsAsync(Build(count), cancellationToken)
            : _transport.QueryLogsAsync(_patientId ?? "", Build(count), cancellationToken);

    private List<object?> EnsureFilter()
    {
        if (!_spec.TryGetValue("filter", out var filterObj) || filterObj is not List<object?> filter)
        {
            filter = [];
            _spec["filter"] = filter;
        }

        return filter;
    }

    private List<object?> EnsureSelect()
    {
        if (!_spec.TryGetValue("select", out var selObj) || selObj is not List<object?> sel)
        {
            sel = [];
            _spec["select"] = sel;
        }

        return sel;
    }
}
