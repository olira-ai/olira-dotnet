#nullable enable

namespace Olira;

public sealed partial class OliraClient
{
    /// <summary>
    /// Register an outbound-actions destination (webhook or email). Requires sdk:actions scope.
    /// </summary>
    /// <remarks>
    /// Pass exactly one of <paramref name="webhookConfig"/> or <paramref name="emailConfig"/> to
    /// select the destination type. The returned destination's <c>SigningSecret</c> is shown in
    /// plaintext exactly once; store it immediately, it cannot be retrieved again (only rotated).
    /// </remarks>
    public ActionDestination CreateActionDestination(
        WebhookDestinationConfig? webhookConfig = null,
        EmailDestinationConfig? emailConfig = null,
        IReadOnlyList<string>? subscribedTriggers = null,
        string? description = null,
        IReadOnlyDictionary<string, string>? staticHeaders = null,
        int? rateLimitPerMinute = null,
        DigestSchedule? digestSchedule = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var body = BuildCreateActionDestinationBody(
            webhookConfig, emailConfig, subscribedTriggers, description, staticHeaders, rateLimitPerMinute,
            digestSchedule);
        return _transport.CreateActionDestination(body);
    }

    /// <summary>List the organisation's outbound-actions destinations. Requires sdk:actions scope.</summary>
    public ActionDestinationListResult ListActionDestinations()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _transport.ListActionDestinations();
    }

    /// <summary>Get one outbound-actions destination by id. Requires sdk:actions scope.</summary>
    public ActionDestination GetActionDestination(string destinationId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _transport.GetActionDestination(destinationId);
    }

    /// <summary>
    /// Update a destination's config, subscriptions, or status. Requires sdk:actions scope. Only
    /// the fields you pass are changed.
    /// </summary>
    /// <remarks>
    /// Simply omitting <paramref name="digestSchedule"/> means "leave it as-is," not "remove it,"
    /// so turning digest batching off needs its own flag: pass
    /// <paramref name="clearDigestSchedule"/><c> = true</c>. Passing both
    /// <paramref name="digestSchedule"/> and <paramref name="clearDigestSchedule"/> throws
    /// <see cref="ArgumentException"/>.
    /// </remarks>
    public ActionDestination UpdateActionDestination(
        string destinationId,
        string? url = null,
        string? toEmail = null,
        string? subject = null,
        string? description = null,
        IReadOnlyList<string>? subscribedTriggers = null,
        string? status = null,
        IReadOnlyDictionary<string, string>? staticHeaders = null,
        DigestSchedule? digestSchedule = null,
        bool clearDigestSchedule = false)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var body = BuildUpdateActionDestinationBody(
            url, toEmail, subject, description, subscribedTriggers, status, staticHeaders, digestSchedule,
            clearDigestSchedule);
        return _transport.UpdateActionDestination(destinationId, body);
    }

    /// <summary>
    /// Disables a destination. In-flight deliveries are stopped and will not be retried.
    /// Requires sdk:actions scope.
    /// </summary>
    public ActionDestinationDeleteResult DeleteActionDestination(string destinationId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _transport.DeleteActionDestination(destinationId);
    }

    /// <summary>
    /// Rotate a destination's signing secret. Requires sdk:actions scope. The old secret is
    /// honored for 24h (dual-signing) so in-flight rotations on the receiving end don't drop
    /// deliveries. The new <c>SigningSecret</c> is shown in plaintext exactly once.
    /// </summary>
    public ActionDestination RotateActionDestinationSecret(string destinationId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _transport.RotateActionDestinationSecret(destinationId);
    }

    /// <summary>
    /// List deliveries, newest first, cursor-paginated. Requires sdk:actions scope. Pass the
    /// previous call's <c>NextCursor</c> back in as <paramref name="cursor"/> to fetch the next
    /// page; <c>NextCursor</c> is null once you've reached the last page.
    /// </summary>
    public ActionDeliveryListResult ListActionDeliveries(
        string? destinationId = null,
        string? status = null,
        string? trigger = null,
        string? cursor = null,
        int? limit = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var parameters = BuildListActionDeliveriesParams(destinationId, status, trigger, cursor, limit);
        return _transport.ListActionDeliveries(parameters);
    }

    /// <summary>
    /// Get one delivery's full attempt history, including the exact JSON that was sent. Requires
    /// sdk:actions scope.
    /// </summary>
    public ActionDelivery GetActionDelivery(string deliveryId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _transport.GetActionDelivery(deliveryId);
    }

    /// <summary>
    /// Resends the same body as the original delivery, not a newly generated one. Requires
    /// sdk:actions scope. Throws <see cref="ServerError"/> (HTTP 409) if the destination is
    /// currently disabled; re-enable it first.
    /// </summary>
    public ActionDelivery RedeliverActionDelivery(string deliveryId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _transport.RedeliverActionDelivery(deliveryId);
    }

    private static Dictionary<string, object?> BuildCreateActionDestinationBody(
        WebhookDestinationConfig? webhookConfig,
        EmailDestinationConfig? emailConfig,
        IReadOnlyList<string>? subscribedTriggers,
        string? description,
        IReadOnlyDictionary<string, string>? staticHeaders,
        int? rateLimitPerMinute,
        DigestSchedule? digestSchedule)
    {
        if (webhookConfig is null == emailConfig is null)
        {
            throw new ArgumentException("pass exactly one of webhookConfig or emailConfig");
        }

        var body = new Dictionary<string, object?>
        {
            ["config"] = webhookConfig is not null ? webhookConfig.ToBody() : emailConfig!.ToBody(),
        };
        if (subscribedTriggers is not null)
        {
            body["subscribed_event_types"] = subscribedTriggers.ToList();
        }

        if (description is not null)
        {
            body["description"] = description;
        }

        if (staticHeaders is not null)
        {
            body["static_headers"] = staticHeaders;
        }

        if (rateLimitPerMinute is not null)
        {
            body["rate_limit_per_minute"] = rateLimitPerMinute;
        }

        if (digestSchedule is not null)
        {
            body["digest_schedule"] = digestSchedule.ToBody();
        }

        return body;
    }

    private static Dictionary<string, object?> BuildUpdateActionDestinationBody(
        string? url,
        string? toEmail,
        string? subject,
        string? description,
        IReadOnlyList<string>? subscribedTriggers,
        string? status,
        IReadOnlyDictionary<string, string>? staticHeaders,
        DigestSchedule? digestSchedule,
        bool clearDigestSchedule)
    {
        if (digestSchedule is not null && clearDigestSchedule)
        {
            throw new ArgumentException("pass digestSchedule or clearDigestSchedule, not both");
        }

        var body = new Dictionary<string, object?>();
        if (url is not null)
        {
            body["url"] = url;
        }

        if (toEmail is not null)
        {
            body["to_email"] = toEmail;
        }

        if (subject is not null)
        {
            body["subject"] = subject;
        }

        if (description is not null)
        {
            body["description"] = description;
        }

        if (subscribedTriggers is not null)
        {
            body["subscribed_event_types"] = subscribedTriggers.ToList();
        }

        if (status is not null)
        {
            body["status"] = status;
        }

        if (staticHeaders is not null)
        {
            body["static_headers"] = staticHeaders;
        }

        if (clearDigestSchedule)
        {
            body["digest_schedule"] = null;
        }
        else if (digestSchedule is not null)
        {
            body["digest_schedule"] = digestSchedule.ToBody();
        }

        return body;
    }

    private static Dictionary<string, object?> BuildListActionDeliveriesParams(
        string? destinationId,
        string? status,
        string? trigger,
        string? cursor,
        int? limit)
    {
        var parameters = new Dictionary<string, object?>();
        if (destinationId is not null)
        {
            parameters["destination_id"] = destinationId;
        }

        if (status is not null)
        {
            parameters["status"] = status;
        }

        if (trigger is not null)
        {
            parameters["event_type"] = trigger;
        }

        if (cursor is not null)
        {
            parameters["cursor"] = cursor;
        }

        if (limit is not null)
        {
            parameters["limit"] = limit;
        }

        return parameters;
    }
}
