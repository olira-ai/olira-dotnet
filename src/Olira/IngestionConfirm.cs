#nullable enable

using System.Text.Json;
using Olira.Json;

namespace Olira;

/// <summary>
/// Retry-safe helpers for ingestion job confirm (PATCH skip_backfill + POST confirm).
/// </summary>
public static class IngestionConfirm
{
    // Phase 1 only — 409 here means "too early", not a successful confirm retry.
    private static readonly HashSet<string> Phase1BeforeReviewStatuses = new(StringComparer.Ordinal)
    {
        "queued",
        "validating",
        "inserting_patients",
        "inserting_logs",
    };

    /// <summary>Normalize an ingestion status (string or enum) to its wire value.</summary>
    public static string StatusValue(object? status)
    {
        if (status is null)
        {
            return string.Empty;
        }

        if (status is string s)
        {
            return s;
        }

        // Enums serialized with OliraJson use snake_case member names.
        return JsonSerializer.Serialize(status, OliraJson.Default).Trim('"');
    }

    /// <summary>
    /// True when a PATCH/confirm HTTP 409 means the job already left the review gate.
    /// </summary>
    public static bool Is409PastReviewGate(object? status)
    {
        var s = StatusValue(status);
        if (Phase1BeforeReviewStatuses.Contains(s))
        {
            return false;
        }

        if (string.Equals(s, "awaiting_confirmation", StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }

    /// <summary>Backward-compatible alias for <see cref="Is409PastReviewGate"/>.</summary>
    public static bool IsPostConfirmationStatus(object? status) => Is409PastReviewGate(status);

    /// <summary>
    /// PATCH <c>skip_backfill=True</c>, tolerating 409 if the job already advanced past review.
    /// </summary>
    public static void EnsureSkipBackfillBeforeConfirm(
        Func<IngestionJob> patchSkipBackfill,
        Func<IngestionJob> getJob)
    {
        try
        {
            patchSkipBackfill();
        }
        catch (ServerError ex) when (ex.StatusCode == 409)
        {
            var job = getJob();
            if (!Is409PastReviewGate(job.Status))
            {
                throw;
            }
        }
    }

    /// <summary>Async variant of <see cref="EnsureSkipBackfillBeforeConfirm"/>.</summary>
    public static async Task EnsureSkipBackfillBeforeConfirmAsync(
        Func<Task<IngestionJob>> patchSkipBackfill,
        Func<Task<IngestionJob>> getJob)
    {
        try
        {
            await patchSkipBackfill().ConfigureAwait(false);
        }
        catch (ServerError ex) when (ex.StatusCode == 409)
        {
            var job = await getJob().ConfigureAwait(false);
            if (!Is409PastReviewGate(job.Status))
            {
                throw;
            }
        }
    }

    /// <summary>
    /// Confirm a job; tolerate retried PATCH/confirm after the server already transitioned.
    /// </summary>
    public static IngestionJob ConfirmIngestionJobResilient(
        bool skipBackfill,
        Func<IngestionJob> patchSkipBackfill,
        Func<IngestionJob> getJob,
        Func<IngestionJob> confirm)
    {
        if (skipBackfill)
        {
            EnsureSkipBackfillBeforeConfirm(patchSkipBackfill, getJob);
        }

        try
        {
            return confirm();
        }
        catch (ServerError ex) when (ex.StatusCode == 409)
        {
            var job = getJob();
            if (Is409PastReviewGate(job.Status))
            {
                return job;
            }

            throw;
        }
    }

    /// <summary>Async variant of <see cref="ConfirmIngestionJobResilient"/>.</summary>
    public static async Task<IngestionJob> ConfirmIngestionJobResilientAsync(
        bool skipBackfill,
        Func<Task<IngestionJob>> patchSkipBackfill,
        Func<Task<IngestionJob>> getJob,
        Func<Task<IngestionJob>> confirm)
    {
        if (skipBackfill)
        {
            await EnsureSkipBackfillBeforeConfirmAsync(patchSkipBackfill, getJob).ConfigureAwait(false);
        }

        try
        {
            return await confirm().ConfigureAwait(false);
        }
        catch (ServerError ex) when (ex.StatusCode == 409)
        {
            var job = await getJob().ConfigureAwait(false);
            if (Is409PastReviewGate(job.Status))
            {
                return job;
            }

            throw;
        }
    }
}
