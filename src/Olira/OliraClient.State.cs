#nullable enable

using System.Text.Json;
using Olira.Json;

namespace Olira;

public sealed partial class OliraClient
{
    /// <summary>Get stable patient data. Requires sdk:state-read scope.</summary>
    public StableDataResult GetStableData(string patientId, IReadOnlyList<string>? modules = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var parameters = new Dictionary<string, object?>();
        if (modules is { Count: > 0 })
        {
            parameters["modules"] = string.Join(",", modules);
        }

        return _transport.GetStableData(patientId, parameters);
    }

    /// <summary>List event state module types present for the patient.</summary>
    public List<EventStateModuleSummary> ListEventStateModules(string patientId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var raw = _transport.ListEventStateModules(patientId);
        return raw.Select(DeserializeRequired<EventStateModuleSummary>).ToList();
    }

    /// <summary>Get a specific event state module by type.</summary>
    public EventStateModuleResult GetEventStateModule(string patientId, string moduleType)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _transport.GetEventStateModule(patientId, moduleType);
    }

    /// <summary>List available views for the patient.</summary>
    public List<ViewMeta> ListViews(string patientId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var raw = _transport.ListViews(patientId);
        return raw.Select(DeserializeRequired<ViewMeta>).ToList();
    }

    /// <summary>List blocks within a specific view.</summary>
    public ViewBlocksListResult ListViewBlocks(string patientId, string viewType)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _transport.ListViewBlocks(patientId, viewType);
    }

    /// <summary>Get a view snapshot.</summary>
    public ViewResult GetView(string patientId, string viewType)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _transport.GetView(patientId, viewType);
    }

    /// <summary>Get a specific block from a view.</summary>
    public ViewBlockResult GetViewBlock(string patientId, string viewType, string blockId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _transport.GetViewBlock(patientId, viewType, blockId);
    }

    /// <summary>Get recent TEMP events for a view type.</summary>
    public ViewRecentEventsResult GetViewRecentEvents(string patientId, string viewType, int limit = 50)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _transport.GetViewRecentEvents(
            patientId,
            viewType,
            new Dictionary<string, object?> { ["limit"] = limit });
    }

    /// <summary>Get logs for the patient.</summary>
    public LogsResult GetLogs(
        string patientId,
        string? since = null,
        int limit = 50,
        IReadOnlyList<string>? logTypes = null,
        string? traceType = null,
        string? traceId = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var parameters = new Dictionary<string, object?> { ["limit"] = limit };
        if (!string.IsNullOrEmpty(since))
        {
            parameters["since"] = since;
        }

        if (logTypes is { Count: > 0 })
        {
            parameters["event_types"] = string.Join(",", logTypes);
        }

        if (!string.IsNullOrEmpty(traceType))
        {
            parameters["trace_type"] = traceType;
        }

        if (!string.IsNullOrEmpty(traceId))
        {
            parameters["trace_id"] = traceId;
        }

        return _transport.GetLogs(patientId, parameters);
    }

    /// <summary>Build a structured query over one patient's logs.</summary>
    public LogQuery Logs(string patientId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return new LogQuery(_transport, patientId: patientId);
    }

    /// <summary>Build a structured query across the org (or a cohort).</summary>
    public LogQuery PopulationLogs(IReadOnlyList<string>? patientIds = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return new LogQuery(_transport, patientIds: patientIds, population: true);
    }

    /// <summary>Get events for the patient.</summary>
    public EventsResult GetEvents(
        string patientId,
        string? since = null,
        string? logType = null,
        string? traceType = null,
        string? traceId = null,
        string status = "complete",
        int limit = 50)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var parameters = new Dictionary<string, object?>
        {
            ["status"] = status,
            ["limit"] = limit,
        };
        if (!string.IsNullOrEmpty(since))
        {
            parameters["since"] = since;
        }

        if (!string.IsNullOrEmpty(logType))
        {
            parameters["log_type"] = logType;
        }

        if (!string.IsNullOrEmpty(traceType))
        {
            parameters["trace_type"] = traceType;
        }

        if (!string.IsNullOrEmpty(traceId))
        {
            parameters["trace_id"] = traceId;
        }

        return _transport.GetEvents(patientId, parameters);
    }

    /// <summary>Read memories for the patient.</summary>
    public MemoriesResult ReadMemories(string patientId, string? query = null, int limit = 100)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var parameters = new Dictionary<string, object?> { ["limit"] = limit };
        if (!string.IsNullOrEmpty(query))
        {
            parameters["query"] = query;
        }

        return _transport.ReadMemories(patientId, parameters);
    }

    private static T DeserializeRequired<T>(JsonElement element)
    {
        return element.Deserialize<T>(OliraJson.Default)
               ?? throw new ServerError($"Failed to deserialize {typeof(T).Name}");
    }
}
