#nullable enable

using System.Text;
using System.Text.Json;
using Olira.Json;

namespace Olira;

public sealed partial class OliraClient
{
    /// <summary>
    /// Create a historical data ingestion job. Requires sdk:historical-ingest scope.
    /// Provide <paramref name="file"/>, <paramref name="records"/>, and/or <paramref name="documents"/>.
    /// </summary>
    public IngestionJob CreateIngestionJob(
        string? file = null,
        IReadOnlyList<IngestRecord>? records = null,
        IReadOnlyList<IngestDocument>? documents = null,
        string? idempotencyKey = null,
        bool requireConfirmation = true,
        IReadOnlyList<string>? summaryTypes = null,
        int? maxEventLogs = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (file is null && records is null && (documents is null || documents.Count == 0))
        {
            throw new ValidationError("Provide 'file', 'records', and/or 'documents'");
        }

        if (file is not null && records is not null)
        {
            throw new ValidationError("Provide either 'file' or 'records', not both");
        }

        if (file is not null && documents is { Count: > 0 })
        {
            throw new ValidationError("Document packages use records=… + documents=… (not file=)");
        }

        var body = new Dictionary<string, object?>
        {
            ["require_confirmation"] = requireConfirmation,
        };
        if (!string.IsNullOrEmpty(idempotencyKey))
        {
            body["idempotency_key"] = idempotencyKey;
        }

        if (summaryTypes is not null)
        {
            body["summary_types"] = summaryTypes.ToList();
        }

        if (maxEventLogs is not null)
        {
            body["max_event_logs"] = maxEventLogs;
        }

        if (documents is { Count: > 0 })
        {
            return CreateDocumentPackageJob(body, records ?? [], documents);
        }

        if (file is not null)
        {
            long maxBytes;
            try
            {
                var sdkCfg = _transport.GetSdkConfig();
                maxBytes = sdkCfg.TryGetValue("ingestion_max_file_bytes", out var mb)
                           && mb.ValueKind == JsonValueKind.Number
                           && mb.TryGetInt64(out var n)
                    ? n
                    : 100L * 1024 * 1024;
            }
            catch
            {
                maxBytes = 100L * 1024 * 1024;
            }

            var urlData = _transport.GetUploadUrl();
            var allIssues = Validation.ValidateIngestionFile(file, maxBytes);
            var blocking = allIssues.Where(e => e.Code != "patient_id_not_in_file").ToList();
            if (blocking.Count > 0)
            {
                throw ValidationFailed("JSONL validation failed", blocking, "line");
            }

            var uploadUrl = urlData["upload_url"].GetString()
                            ?? throw new ServerError("Upload URL response missing upload_url");
            var s3Key = urlData["s3_key"].GetString()
                        ?? throw new ServerError("Upload URL response missing s3_key");
            var content = File.ReadAllBytes(file);
            PutBytes(uploadUrl, content, timeoutSeconds: 300);
            body["s3_key"] = s3Key;
        }
        else
        {
            var inline = records ?? [];
            var allIssues = Validation.ValidateIngestionRecords(inline);
            var blocking = allIssues.Where(e => e.Code != "patient_id_not_in_file").ToList();
            if (blocking.Count > 0)
            {
                throw ValidationFailed("Records validation failed", blocking, "record");
            }

            body["records"] = inline.Select(r => new Dictionary<string, object?>
            {
                ["type"] = r.Type,
                ["data"] = r.Data,
            }).ToList();
        }

        return _transport.CreateIngestionJob(body);
    }

    private IngestionJob CreateDocumentPackageJob(
        Dictionary<string, object?> body,
        IReadOnlyList<IngestRecord> records,
        IReadOnlyList<IngestDocument> documents)
    {
        foreach (var rec in records)
        {
            if (rec.Type == "document")
            {
                throw new ValidationError(
                    "Pass document binaries via documents=, not IngestRecord.document in records");
            }
        }

        var beginDocs = new List<Dictionary<string, object?>>();
        var resolved = new List<(IngestDocument Doc, string RefId, string ContentType, string Path)>();
        var seenRefIds = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < documents.Count; i++)
        {
            var doc = documents[i];
            if (!File.Exists(doc.Path))
            {
                throw new ValidationError($"Document path not found: {doc.Path}");
            }

            var refId = doc.RefId ?? $"d{i + 1}";
            if (!seenRefIds.Add(refId))
            {
                throw new ValidationError($"Duplicate document ref_id: {JsonSerializer.Serialize(refId)}");
            }

            var contentType = doc.ContentType ?? GuessContentType(doc.Path);
            var filename = doc.Filename ?? Path.GetFileName(doc.Path);
            beginDocs.Add(new Dictionary<string, object?>
            {
                ["ref_id"] = refId,
                ["content_type"] = contentType,
                ["filename"] = filename,
                ["size_bytes"] = new FileInfo(doc.Path).Length,
            });
            resolved.Add((doc, refId, contentType, doc.Path));
        }

        var begin = _transport.BeginIngestionJob(new Dictionary<string, object?> { ["documents"] = beginDocs });
        if (!begin.TryGetValue("documents", out var docsEl) || docsEl.ValueKind != JsonValueKind.Array)
        {
            throw new ServerError("begin_ingestion_job response missing documents");
        }

        var uploadsByRef = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var d in docsEl.EnumerateArray())
        {
            var rid = d.GetProperty("ref_id").GetString()
                      ?? throw new ServerError("begin document missing ref_id");
            uploadsByRef[rid] = d.Clone();
        }

        foreach (var (_, refId, contentType, path) in resolved)
        {
            var upload = uploadsByRef[refId];
            var uploadUrl = upload.GetProperty("upload_url").GetString()
                            ?? throw new ServerError("begin document missing upload_url");
            _transport.PutPresigned(
                uploadUrl,
                File.ReadAllBytes(path),
                new Dictionary<string, string> { ["Content-Type"] = contentType });
        }

        var manifestRows = records.ToList();
        foreach (var (doc, refId, contentType, _) in resolved)
        {
            var upload = uploadsByRef[refId];
            var s3Key = upload.GetProperty("s3_key").GetString()
                        ?? throw new ServerError("begin document missing s3_key");
            var parts = s3Key.Split('/');
            var relKey = parts.Length > 2 ? string.Join('/', parts.Skip(2)) : s3Key;
            var patched = new IngestDocument(
                path: doc.Path,
                patientId: doc.PatientId,
                logType: doc.LogType,
                timestamp: doc.Timestamp,
                refId: refId,
                documentType: doc.DocumentType,
                noteType: doc.NoteType,
                source: doc.Source,
                idempotencyKey: doc.IdempotencyKey,
                contentType: contentType,
                filename: doc.Filename ?? Path.GetFileName(doc.Path));
            manifestRows.Add(IngestRecord.Document(patched, relKey, refId));
        }

        var allIssues = Validation.ValidateIngestionRecords(manifestRows);
        var blocking = allIssues.Where(e => e.Code != "patient_id_not_in_file").ToList();
        if (blocking.Count > 0)
        {
            throw ValidationFailed("Records validation failed", blocking, "record");
        }

        var manifestBytes = Encoding.UTF8.GetBytes(
            string.Join("\n", manifestRows.Select(r => JsonSerializer.Serialize(r, OliraJson.Default))) + "\n");
        var manifestUploadUrl = begin["manifest_upload_url"].GetString()
                                ?? throw new ServerError("begin response missing manifest_upload_url");
        _transport.PutPresigned(manifestUploadUrl, manifestBytes);

        body["job_id"] = begin["job_id"].GetString();
        body["s3_key"] = begin["manifest_s3_key"].GetString();
        body["has_documents"] = true;
        body["documents_total"] = documents.Count;
        return _transport.CreateIngestionJob(body);
    }

    /// <summary>Poll the status of an ingestion job.</summary>
    public IngestionJob GetIngestionJob(string jobId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _transport.GetIngestionJob(jobId);
    }

    /// <summary>List ingestion jobs for the org.</summary>
    public IngestionJobListResult ListIngestionJobs(
        string? idempotencyKey = null,
        int page = 1,
        int pageSize = 20)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var parameters = new Dictionary<string, object?>
        {
            ["page"] = page,
            ["pageSize"] = pageSize,
        };
        if (!string.IsNullOrEmpty(idempotencyKey))
        {
            parameters["idempotency_key"] = idempotencyKey;
        }

        return _transport.ListIngestionJobs(parameters);
    }

    /// <summary>
    /// Confirm a job in AWAITING_CONFIRMATION to start Phase 2 (replay + backfill).
    /// Tolerates retried confirm after the server already transitioned (HTTP 409).
    /// </summary>
    public IngestionJob ConfirmIngestionJob(
        string jobId,
        bool initializeMissingTemplates = false,
        bool skipBackfill = false)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return IngestionConfirm.ConfirmIngestionJobResilient(
            skipBackfill,
            () => PatchIngestionJob(jobId, skipBackfill: true),
            () => GetIngestionJob(jobId),
            () => _transport.ConfirmIngestionJob(jobId, initializeMissingTemplates));
    }

    /// <summary>Cancel an ingestion job.</summary>
    public IngestionJob CancelIngestionJob(string jobId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _transport.CancelIngestionJob(jobId);
    }

    /// <summary>Remove a patient and their STALE logs while AWAITING_CONFIRMATION.</summary>
    public void DeleteIngestionJobPatient(string jobId, string patientId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _transport.DeleteIngestionJobPatient(jobId, patientId);
    }

    /// <summary>Update mutable fields while AWAITING_CONFIRMATION.</summary>
    public IngestionJob PatchIngestionJob(
        string jobId,
        IReadOnlyList<string>? summaryTypes = null,
        bool? skipBackfill = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var body = new Dictionary<string, object?>();
        if (summaryTypes is not null)
        {
            body["summary_types"] = summaryTypes.ToList();
        }

        if (skipBackfill is not null)
        {
            body["skip_backfill"] = skipBackfill;
        }

        return _transport.PatchIngestionJob(jobId, body);
    }

    /// <summary>Retry a failed view backfill on a COMPLETED_WITH_ERRORS job.</summary>
    public IngestionJob RetryViewBackfill(string jobId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _transport.RetryViewBackfill(jobId);
    }

    /// <summary>
    /// Upload a PDF/image for OCR → EventLog (upload-url + PUT + commit).
    /// </summary>
    public DocumentHandle UploadDocument(
        string patientId,
        string path,
        DocumentLogType logType,
        DateTimeOffset timestamp,
        string idempotencyKey,
        string? documentType = null,
        string? noteType = null,
        object? source = null,
        string? contentType = null,
        bool wait = false,
        double waitTimeoutSeconds = 600.0)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var handle = Documents.UploadDocumentViaTransport(
            _transport,
            patientId,
            path,
            logType,
            timestamp,
            idempotencyKey,
            documentType,
            noteType,
            source,
            contentType);
        if (wait)
        {
            handle.Wait(timeoutSeconds: waitTimeoutSeconds);
        }

        return handle;
    }

    /// <summary>Upload with a string log_type.</summary>
    public DocumentHandle UploadDocument(
        string patientId,
        string path,
        string logType,
        DateTimeOffset timestamp,
        string idempotencyKey,
        string? documentType = null,
        string? noteType = null,
        object? source = null,
        string? contentType = null,
        bool wait = false,
        double waitTimeoutSeconds = 600.0) =>
        UploadDocument(
            patientId,
            path,
            Documents.ParseLogType(logType),
            timestamp,
            idempotencyKey,
            documentType,
            noteType,
            source,
            contentType,
            wait,
            waitTimeoutSeconds);

    /// <summary>Poll document OCR status.</summary>
    public DocumentResource GetDocument(string documentId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _transport.GetDocument(documentId);
    }

    /// <summary>Send a batch of passive sensor data (accelerometer / gyroscope / gps).</summary>
    public SignalJobHandle SendSignals(
        string patientId,
        SignalSensorType sensorType,
        string sourceDevice,
        IReadOnlyList<Dictionary<string, object?>>? records = null,
        byte[]? parquet = null,
        string? schemaVersion = null,
        double? sampleRateHz = null,
        IReadOnlyDictionary<string, string>? units = null,
        string? timestampUnit = null,
        string? deviceTimezone = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return Signals.SendSignalsViaTransport(
            _transport,
            patientId,
            sensorType,
            sourceDevice,
            records,
            parquet,
            schemaVersion,
            sampleRateHz,
            units,
            timestampUnit,
            deviceTimezone);
    }

    /// <summary>Send signals with a string sensor type.</summary>
    public SignalJobHandle SendSignals(
        string patientId,
        string sensorType,
        string sourceDevice,
        IReadOnlyList<Dictionary<string, object?>>? records = null,
        byte[]? parquet = null,
        string? schemaVersion = null,
        double? sampleRateHz = null,
        IReadOnlyDictionary<string, string>? units = null,
        string? timestampUnit = null,
        string? deviceTimezone = null) =>
        SendSignals(
            patientId,
            Signals.ParseSensorType(sensorType),
            sourceDevice,
            records,
            parquet,
            schemaVersion,
            sampleRateHz,
            units,
            timestampUnit,
            deviceTimezone);

    /// <summary>Poll a signal ingestion job.</summary>
    public SignalJob GetSignalJob(string jobId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _transport.GetSignalJob(jobId);
    }

    private static ValidationError ValidationFailed(
        string prefix,
        IReadOnlyList<IngestionRowError> blocking,
        string lineLabel)
    {
        var summary = string.Join(
            "; ",
            blocking.Take(5).Select(e => $"{lineLabel} {e.Line} [{e.Code}] {e.Message}"));
        var suffix = blocking.Count > 5 ? $" … and {blocking.Count - 5} more" : "";
        return new ValidationError($"{prefix} ({blocking.Count} error(s)): {summary}{suffix}");
    }

    /// <summary>
    /// PUT JSONL bytes to a presigned URL. Matches the Python SDK: no Content-Type header
    /// (upload URLs are typically signed without ContentType; setting one causes S3 403).
    /// </summary>
    private static void PutBytes(string url, byte[] content, int timeoutSeconds)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(timeoutSeconds) };
        using var request = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = new ByteArrayContent(content),
        };
        // Intentionally omit Content-Type — parity with Python httpx.put(url, content=…).
        using var response = client.Send(request);
        var status = (int)response.StatusCode;
        if (status >= 300)
        {
            throw new ServerError($"Presigned upload failed (HTTP {status})", statusCode: status);
        }
    }
}
