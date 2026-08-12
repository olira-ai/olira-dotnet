#nullable enable

using System.Globalization;

namespace Olira;

public sealed partial class OliraClient
{
    /// <summary>
    /// Start a batch export job. Requires <c>sdk:state-read</c> scope.
    /// Provide exactly one of <paramref name="patientIds"/>, <paramref name="cohortId"/>,
    /// or <paramref name="scope"/> = <c>"project"</c>.
    /// Poll with <see cref="GetExport"/> until <see cref="ExportJob.Downloadable"/> is true,
    /// then <see cref="DownloadExport"/> for a presigned URL.
    /// </summary>
    public ExportJob CreateExport(
        DateTimeOffset start,
        DateTimeOffset end,
        ExportInclude include,
        IReadOnlyList<string>? patientIds = null,
        string? cohortId = null,
        string? scope = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(include);

        var selectors = 0;
        if (patientIds is { Count: > 0 }) selectors++;
        if (!string.IsNullOrEmpty(cohortId)) selectors++;
        if (!string.IsNullOrEmpty(scope)) selectors++;
        if (selectors != 1)
        {
            throw new ValidationError(
                "Provide exactly one of patientIds, cohortId, or scope=\"project\"");
        }

        if (!string.IsNullOrEmpty(scope) &&
            !string.Equals(scope, "project", StringComparison.Ordinal))
        {
            throw new ValidationError("scope must be \"project\" when provided");
        }

        var body = new Dictionary<string, object?>
        {
            ["start"] = start.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture),
            ["end"] = end.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture),
            ["include"] = include,
        };
        if (patientIds is { Count: > 0 })
        {
            body["patient_ids"] = patientIds.ToList();
        }

        if (!string.IsNullOrEmpty(cohortId))
        {
            body["cohort_id"] = cohortId;
        }

        if (!string.IsNullOrEmpty(scope))
        {
            body["scope"] = scope;
        }

        return _transport.CreateExport(body);
    }

    /// <summary>Poll export job status. Requires <c>sdk:state-read</c> scope.</summary>
    public ExportJob GetExport(string exportId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (string.IsNullOrWhiteSpace(exportId))
        {
            throw new ValidationError("exportId is required");
        }

        return _transport.GetExport(exportId);
    }

    /// <summary>List historical export jobs for the org/project. Requires <c>sdk:state-read</c>.</summary>
    public ExportJobListResult ListExports(
        int limit = 50,
        int offset = 0,
        string? status = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var parameters = new Dictionary<string, object?>
        {
            ["limit"] = limit,
            ["offset"] = offset,
        };
        if (!string.IsNullOrEmpty(status))
        {
            parameters["status"] = status;
        }

        return _transport.ListExports(parameters);
    }

    /// <summary>
    /// Get a presigned download URL for a completed export. Requires <c>sdk:state-read</c>.
    /// </summary>
    public ExportDownload DownloadExport(string exportId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (string.IsNullOrWhiteSpace(exportId))
        {
            throw new ValidationError("exportId is required");
        }

        return _transport.DownloadExport(exportId);
    }
}
