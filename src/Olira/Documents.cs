#nullable enable

using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Olira.Internal;

namespace Olira;

/// <summary>Document log types accepted by the upload-document path.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<DocumentLogType>))]
public enum DocumentLogType
{
    /// <summary>Unstructured clinical report (requires document_type).</summary>
    [JsonStringEnumMemberName("unstructured_report")]
    UnstructuredReport,

    /// <summary>Clinical note (requires note_type + source).</summary>
    [JsonStringEnumMemberName("clinical_note")]
    ClinicalNote,
}

/// <summary>OCR / emit lifecycle for a clinical document.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<DocumentStatus>))]
public enum DocumentStatus
{
    [JsonStringEnumMemberName("pending_upload")]
    PendingUpload,

    [JsonStringEnumMemberName("uploaded")]
    Uploaded,

    [JsonStringEnumMemberName("ocr_running")]
    OcrRunning,

    [JsonStringEnumMemberName("ocr_complete")]
    OcrComplete,

    [JsonStringEnumMemberName("ocr_failed")]
    OcrFailed,

    [JsonStringEnumMemberName("log_emitted")]
    LogEmitted,
}

/// <summary>Extensions for <see cref="DocumentStatus"/>.</summary>
public static class DocumentStatusExtensions
{
    /// <summary>True when OCR finished successfully or failed.</summary>
    public static bool IsTerminal(this DocumentStatus status) =>
        status is DocumentStatus.LogEmitted or DocumentStatus.OcrFailed;

    /// <summary>Wire value for the status.</summary>
    public static string ToWireValue(this DocumentStatus status) =>
        status switch
        {
            DocumentStatus.PendingUpload => "pending_upload",
            DocumentStatus.Uploaded => "uploaded",
            DocumentStatus.OcrRunning => "ocr_running",
            DocumentStatus.OcrComplete => "ocr_complete",
            DocumentStatus.OcrFailed => "ocr_failed",
            DocumentStatus.LogEmitted => "log_emitted",
            _ => status.ToString().ToLowerInvariant(),
        };

    /// <summary>Wire value for the log type.</summary>
    public static string ToWireValue(this DocumentLogType logType) =>
        logType switch
        {
            DocumentLogType.UnstructuredReport => "unstructured_report",
            DocumentLogType.ClinicalNote => "clinical_note",
            _ => logType.ToString().ToLowerInvariant(),
        };
}

/// <summary>Document resource returned by GET /v1/documents/{id}.</summary>
public sealed class DocumentResource
{
    public string DocumentId { get; set; } = "";
    public DocumentStatus Status { get; set; }
    public string Filename { get; set; } = "";
    public string PatientId { get; set; } = "";
    public string LogType { get; set; } = "";
    public string? DocumentType { get; set; }
    public string? NoteType { get; set; }
    public string? S3Uri { get; set; }
    public string? EventLogId { get; set; }
    public string? Error { get; set; }
    public int? OcrPageCount { get; set; }
    public double? OcrConfidence { get; set; }
    public string? CreatedAt { get; set; }
    public string? UpdatedAt { get; set; }
}

/// <summary>Poll/wait handle for a document OCR job.</summary>
public sealed class DocumentHandle
{
    private DocumentResource _doc;
    private readonly Func<string, DocumentResource> _fetch;

    /// <summary>Creates a handle around an initial document snapshot.</summary>
    public DocumentHandle(DocumentResource doc, Func<string, DocumentResource> fetch)
    {
        _doc = doc ?? throw new ArgumentNullException(nameof(doc));
        _fetch = fetch ?? throw new ArgumentNullException(nameof(fetch));
    }

    /// <summary>Document id.</summary>
    public string DocumentId => _doc.DocumentId;

    /// <summary>Latest fetched document snapshot.</summary>
    public DocumentResource Document => _doc;

    /// <summary>Refresh and return the current document state.</summary>
    public DocumentResource Poll()
    {
        _doc = _fetch(_doc.DocumentId);
        return _doc;
    }

    /// <summary>Block until the document reaches a terminal status.</summary>
    public DocumentResource Wait(double timeoutSeconds = 600.0, double pollIntervalSeconds = 2.0)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (true)
        {
            var doc = Poll();
            if (doc.Status.IsTerminal())
            {
                return doc;
            }

            if (DateTime.UtcNow >= deadline)
            {
                throw new OliraError(
                    $"Timed out waiting for document {doc.DocumentId} (status={doc.Status.ToWireValue()})");
            }

            Thread.Sleep(TimeSpan.FromSeconds(pollIntervalSeconds));
        }
    }

    /// <summary>Async variant of <see cref="Wait"/>.</summary>
    public async Task<DocumentResource> WaitAsync(
        double timeoutSeconds = 600.0,
        double pollIntervalSeconds = 2.0,
        CancellationToken cancellationToken = default)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _doc = await Task.Run(() => _fetch(_doc.DocumentId), cancellationToken).ConfigureAwait(false);
            if (_doc.Status.IsTerminal())
            {
                return _doc;
            }

            if (DateTime.UtcNow >= deadline)
            {
                throw new OliraError(
                    $"Timed out waiting for document {_doc.DocumentId} (status={_doc.Status.ToWireValue()})");
            }

            await Task.Delay(TimeSpan.FromSeconds(pollIntervalSeconds), cancellationToken).ConfigureAwait(false);
        }
    }
}

/// <summary>Clinical document upload helpers (label → presigned PUT → commit).</summary>
public static class Documents
{
    /// <summary>
    /// Upload-url → PUT → commit. Returns a pollable handle.
    /// Requires the <c>sdk:event-log</c> API-key scope.
    /// </summary>
    public static DocumentHandle UploadDocumentViaTransport(
        HttpTransport transport,
        string patientId,
        string path,
        DocumentLogType logType,
        DateTimeOffset timestamp,
        string idempotencyKey,
        string? documentType = null,
        string? noteType = null,
        object? source = null,
        string? contentType = null)
    {
        if (!File.Exists(path))
        {
            throw new ValidationError($"Document file not found: {path}");
        }

        var blob = File.ReadAllBytes(path);
        if (blob.Length == 0)
        {
            throw new ValidationError("Document file is empty");
        }

        var sha = Convert.ToHexString(SHA256.HashData(blob)).ToLowerInvariant();
        var fileName = Path.GetFileName(path);
        var resolvedCt = contentType
                         ?? GuessContentType(fileName)
                         ?? "application/pdf";

        var body = new Dictionary<string, object?>
        {
            ["patient_id"] = patientId,
            ["content_type"] = resolvedCt,
            ["content_sha256"] = sha,
            ["size_bytes"] = blob.Length,
            ["filename"] = fileName,
            ["log_type"] = logType.ToWireValue(),
            ["timestamp"] = timestamp.ToString("O"),
            ["idempotency_key"] = idempotencyKey,
        };

        if (logType == DocumentLogType.UnstructuredReport)
        {
            if (string.IsNullOrEmpty(documentType))
            {
                throw new ValidationError("document_type is required for unstructured_report");
            }

            body["document_type"] = documentType;
            if (source is not null)
            {
                body["source"] = source;
            }
        }
        else
        {
            if (string.IsNullOrEmpty(noteType))
            {
                throw new ValidationError("note_type is required for clinical_note");
            }

            if (source is null)
            {
                throw new ValidationError("source is required for clinical_note");
            }

            body["note_type"] = noteType;
            body["source"] = source;
        }

        var upload = transport.GetDocumentUploadUrl(body);
        var uploadUrl = GetString(upload, "upload_url")
                        ?? throw new ServerError("Document upload response missing upload_url");
        var documentId = GetString(upload, "document_id")
                         ?? throw new ServerError("Document upload response missing document_id");

        transport.PutPresigned(
            uploadUrl,
            blob,
            new Dictionary<string, string> { ["Content-Type"] = resolvedCt });
        transport.CommitDocument(documentId);
        var doc = transport.GetDocument(documentId);
        return new DocumentHandle(doc, transport.GetDocument);
    }

    /// <summary>Parse a log_type string into <see cref="DocumentLogType"/>.</summary>
    public static DocumentLogType ParseLogType(string logType) =>
        logType switch
        {
            "unstructured_report" => DocumentLogType.UnstructuredReport,
            "clinical_note" => DocumentLogType.ClinicalNote,
            _ => throw new ValidationError(
                $"log_type must be 'unstructured_report' or 'clinical_note', got {JsonSerializer.Serialize(logType)}"),
        };

    private static string? GuessContentType(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".pdf" => "application/pdf",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            ".tif" or ".tiff" => "image/tiff",
            ".gif" => "image/gif",
            _ => null,
        };
    }

    private static string? GetString(IDictionary<string, JsonElement> dict, string key)
    {
        if (!dict.TryGetValue(key, out var el))
        {
            return null;
        }

        return el.ValueKind == JsonValueKind.String ? el.GetString() : el.ToString();
    }
}
