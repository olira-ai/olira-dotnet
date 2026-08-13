using System.Text.Json;
using System.Text.Json.Serialization;

namespace Olira;

/// <summary>
/// Triggers accepted by <c>SubscribedTriggers</c> / <c>DigestSchedule.Triggers</c>. A plain
/// string also works everywhere one of these constants is accepted (nothing validates it
/// client-side, so a typo'd string still reaches the server as a 422).
/// </summary>
public static class ActionTrigger
{
    /// <summary>
    /// Subscribe to every currently available trigger listed below. Because this value is
    /// evaluated by the platform rather than by this list, a destination subscribed to it could
    /// start receiving additional trigger types later without another call on your part.
    /// </summary>
    public const string All = "*";

    /// <summary>Something changed about a patient, such as new symptoms, lab results, or medications.</summary>
    public const string PatientStateChanged = "patient.state.changed";

    /// <summary>Olira received a log for a patient, but it didn't change anything known about them.</summary>
    public const string LogNoStateChange = "log.no_state_change";

    /// <summary>One of your incoming logs could not be translated into Olira's data model.</summary>
    public const string OrgMappingFailed = "org.mapping.failed";

    /// <summary>A historical ingestion job you started finished successfully.</summary>
    public const string IngestionCompleted = "ingestion.completed";

    /// <summary>A historical ingestion job you started did not finish successfully.</summary>
    public const string IngestionFailed = "ingestion.failed";

    /// <summary>
    /// Triggers frequent enough that sending every one immediately risks flooding the
    /// destination. The Olira Console defaults this trigger to digest batching when
    /// subscribed; every other currently available trigger defaults to send-immediately.
    /// A suggested starting point, not a hard rule.
    /// </summary>
    public static readonly IReadOnlySet<string> RecommendedDigestTriggers = new HashSet<string>(StringComparer.Ordinal)
    {
        PatientStateChanged,
    };
}

/// <summary>
/// Base type for an outbound-actions destination config. Pass a <see cref="WebhookDestinationConfig"/>
/// or <see cref="EmailDestinationConfig"/> to <c>CreateActionDestination</c>. Sealed subclasses
/// model each destination type this SDK version supports.
/// </summary>
public abstract class ActionDestinationConfig
{
    internal abstract Dictionary<string, object?> ToBody();
}

/// <summary>Config for a webhook destination.</summary>
/// <remarks>
/// <see cref="Url"/> must be public HTTPS; <c>http://</c>, <c>localhost</c>, and private/internal
/// addresses are rejected, both when you set the URL and again every time Olira sends to it.
/// </remarks>
public sealed class WebhookDestinationConfig : ActionDestinationConfig
{
    /// <summary>Destination URL. Must be public HTTPS.</summary>
    public required string Url { get; set; }

    /// <summary>Payload envelope API version. Defaults to the current version.</summary>
    public string? ApiVersion { get; set; }

    internal override Dictionary<string, object?> ToBody()
    {
        var body = new Dictionary<string, object?> { ["destination_type"] = "webhook", ["url"] = Url };
        if (ApiVersion is not null)
        {
            body["api_version"] = ApiVersion;
        }

        return body;
    }
}

/// <summary>Config for an email destination.</summary>
public sealed class EmailDestinationConfig : ActionDestinationConfig
{
    /// <summary>Recipient address.</summary>
    public required string ToEmail { get; set; }

    /// <summary>Optional subject override.</summary>
    public string? Subject { get; set; }

    /// <summary>Optional sender display name.</summary>
    public string? FromName { get; set; }

    internal override Dictionary<string, object?> ToBody()
    {
        var body = new Dictionary<string, object?> { ["destination_type"] = "email", ["to_email"] = ToEmail };
        if (Subject is not null)
        {
            body["subject"] = Subject;
        }

        if (FromName is not null)
        {
            body["from_name"] = FromName;
        }

        return body;
    }
}

/// <summary>
/// Opt-in daily batching for high-frequency triggers on a destination. Pass to
/// <c>CreateActionDestination</c> / <c>UpdateActionDestination</c>; also returned on
/// <see cref="ActionDestination.DigestSchedule"/> when enabled.
/// </summary>
public sealed class DigestSchedule
{
    /// <summary><c>"HH:MM"</c>, must land on a <c>:00</c> or <c>:30</c> boundary. Defaults to <c>"09:00"</c>.</summary>
    public string TimeOfDay { get; set; } = "09:00";

    /// <summary>IANA timezone name. Defaults to <c>"UTC"</c>.</summary>
    public string? Timezone { get; set; }

    /// <summary>
    /// Which subscribed triggers batch. Must be a subset of the destination's
    /// <see cref="ActionDestination.SubscribedTriggers"/>.
    /// </summary>
    [JsonPropertyName("event_types")]
    public List<string>? Triggers { get; set; }

    /// <summary>Server-managed, ignored on write.</summary>
    public string? LastSentDate { get; set; }

    internal Dictionary<string, object?> ToBody()
    {
        var body = new Dictionary<string, object?> { ["time_of_day"] = TimeOfDay };
        if (Timezone is not null)
        {
            body["timezone"] = Timezone;
        }

        if (Triggers is not null)
        {
            body["event_types"] = Triggers;
        }

        return body;
    }
}

/// <summary>A registered outbound-actions destination (webhook or email).</summary>
public sealed class ActionDestination
{
    /// <summary>Olira-assigned destination id.</summary>
    public string Id { get; set; } = "";

    /// <summary>Project this destination watches. Null means the org's default project.</summary>
    public string? ProjectId { get; set; }

    /// <summary><c>"webhook"</c> or <c>"email"</c>.</summary>
    public string DestinationType { get; set; } = "";

    /// <summary><c>"active"</c>, <c>"disabled"</c>, or <c>"auto_disabled"</c>.</summary>
    public string Status { get; set; } = "";

    /// <summary>Free-text description.</summary>
    public string? Description { get; set; }

    /// <summary>Triggers this destination receives.</summary>
    [JsonPropertyName("subscribed_event_types")]
    public List<string> SubscribedTriggers { get; set; } = [];

    /// <summary>
    /// Type-specific config, as returned by the server (URL, api_version, etc). Left untyped
    /// for forward-compatibility with destination types not yet modeled by this SDK version.
    /// </summary>
    public Dictionary<string, JsonElement> Config { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Last 4 characters of the current signing secret.</summary>
    public string? SigningSecretLast4 { get; set; }

    /// <summary>Per-destination delivery rate cap.</summary>
    public int? RateLimitPerMinute { get; set; }

    /// <summary>Digest batching config, if enabled.</summary>
    public DigestSchedule? DigestSchedule { get; set; }

    /// <summary>Running failure streak. Resets to 0 on any successful delivery.</summary>
    public int ConsecutiveFailures { get; set; }

    /// <summary>When the current failure streak began.</summary>
    public string? FailureStreakStartedAt { get; set; }

    /// <summary>
    /// When the destination was auto-disabled (20+ consecutive failures over 72h+), if
    /// applicable.
    /// </summary>
    public string? AutoDisabledAt { get; set; }

    /// <summary>When the signing secret was last rotated.</summary>
    public string? RotatedAt { get; set; }

    /// <summary>
    /// Plaintext signing secret, present only on <c>CreateActionDestination</c> /
    /// <c>RotateActionDestinationSecret</c> responses. Store it immediately; it is never
    /// returned again.
    /// </summary>
    public string? SigningSecret { get; set; }
}

/// <summary>Result of <c>ListActionDestinations</c>.</summary>
public sealed class ActionDestinationListResult
{
    /// <summary>Destinations.</summary>
    public List<ActionDestination> Data { get; set; } = [];

    /// <summary>Total destination count.</summary>
    public int Total { get; set; }
}

/// <summary>Result of <c>DeleteActionDestination</c>: disables the destination.</summary>
public sealed class ActionDestinationDeleteResult
{
    /// <summary>Human-readable confirmation.</summary>
    public string Message { get; set; } = "";

    /// <summary>How many in-flight deliveries were stopped and will not be retried, as a result.</summary>
    public int DeadLetteredDeliveries { get; set; }
}

/// <summary>One delivery attempt within an <see cref="ActionDelivery"/>.</summary>
public sealed class DeliveryAttempt
{
    /// <summary>1-based attempt number.</summary>
    public int Attempt { get; set; }

    /// <summary>Timestamp of this attempt.</summary>
    public string? At { get; set; }

    /// <summary><c>"delivered"</c>, <c>"retryable_error"</c>, or <c>"terminal_error"</c>.</summary>
    public string Outcome { get; set; } = "";

    /// <summary>HTTP status code received, if any.</summary>
    public int? HttpStatus { get; set; }

    /// <summary>Error classification, if the attempt failed.</summary>
    public string? ErrorCode { get; set; }

    /// <summary>First 512 characters of the response body.</summary>
    public string? ResponseSnippet { get; set; }

    /// <summary>Attempt duration in milliseconds.</summary>
    public int? DurationMs { get; set; }
}

/// <summary>One delivery ledger record: one send of one trigger to one destination.</summary>
public sealed class ActionDelivery
{
    /// <summary>Olira-assigned delivery id.</summary>
    public string Id { get; set; } = "";

    /// <summary>Project the source event belongs to.</summary>
    public string? ProjectId { get; set; }

    /// <summary>The destination this delivery targets.</summary>
    public string DestinationId { get; set; } = "";

    /// <summary><c>"webhook"</c> or <c>"email"</c>.</summary>
    public string DestinationType { get; set; } = "";

    /// <summary>The trigger that produced this delivery.</summary>
    [JsonPropertyName("event_type")]
    public string Trigger { get; set; } = "";

    /// <summary>Id of the occurrence that produced this delivery.</summary>
    public string EventId { get; set; } = "";

    /// <summary>
    /// <c>pending</c>/<c>mapping</c>/<c>sending</c> (in flight), <c>delivered</c>,
    /// <c>skipped</c> (nothing to send), <c>retrying</c>, <c>dead_letter</c>, or
    /// <c>buffered</c> (parked for this destination's daily digest; it can sit here for up to
    /// a day, not just a few minutes).
    /// </summary>
    public string Status { get; set; } = "";

    /// <summary>Every attempt made so far, in order.</summary>
    public List<DeliveryAttempt> Attempts { get; set; } = [];

    /// <summary>When the next retry is scheduled, if <c>Status == "retrying"</c>.</summary>
    public string? NextAttemptAt { get; set; }

    /// <summary>Timestamp of the first attempt.</summary>
    public string? FirstAttemptedAt { get; set; }

    /// <summary>Timestamp of successful delivery.</summary>
    public string? DeliveredAt { get; set; }

    /// <summary>Timestamp when delivery stopped being retried, if applicable.</summary>
    public string? DeadLetteredAt { get; set; }

    /// <summary>The most recent error, if any.</summary>
    public string? LastError { get; set; }

    /// <summary>The original delivery id, if this record was created by <c>RedeliverActionDelivery</c>.</summary>
    public string? RedeliveryOf { get; set; }

    /// <summary>
    /// Who initiated the delivery: <c>"redeliver:&lt;actor&gt;"</c> when a user manually resent
    /// it via <c>RedeliverActionDelivery</c>, some other value for an automatic delivery.
    /// </summary>
    public string? RequestedBy { get; set; }

    /// <summary>The daily digest this was included in, if any.</summary>
    public string? BatchedInto { get; set; }

    /// <summary>
    /// The exact JSON sent to the destination, present only on <c>GetActionDelivery</c> responses.
    /// </summary>
    public Dictionary<string, JsonElement>? Payload { get; set; }
}

/// <summary>Result of <c>ListActionDeliveries</c>, cursor-paginated, newest first.</summary>
public sealed class ActionDeliveryListResult
{
    /// <summary>Deliveries (no <see cref="ActionDelivery.Payload"/> on list rows).</summary>
    public List<ActionDelivery> Data { get; set; } = [];

    /// <summary>
    /// Pass back as the cursor to fetch the next page. Null once the last page is reached.
    /// </summary>
    public string? NextCursor { get; set; }
}
