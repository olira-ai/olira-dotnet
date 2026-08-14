using System.Net;
using System.Text.Json;
using RichardSzalay.MockHttp;

namespace Olira.Tests;

public class ActionsTests
{
    private const string DestinationJson = """
        {
          "id": "dest_1",
          "project_id": null,
          "destination_type": "webhook",
          "status": "active",
          "description": null,
          "subscribed_event_types": ["patient.state.changed"],
          "config": {"destination_type": "webhook", "url": "https://hooks.example.com/olira", "api_version": "2026-08-01"},
          "signing_secret_last4": "wxlA",
          "rate_limit_per_minute": 600,
          "digest_schedule": null,
          "consecutive_failures": 0,
          "failure_streak_started_at": null,
          "auto_disabled_at": null,
          "rotated_at": null,
          "signing_secret": "whsec_abc123"
        }
        """;

    private const string DestinationWithDigestJson = """
        {
          "id": "dest_2",
          "project_id": null,
          "destination_type": "webhook",
          "status": "active",
          "description": null,
          "subscribed_event_types": ["patient.state.changed"],
          "config": {"destination_type": "webhook", "url": "https://hooks.example.com/olira", "api_version": "2026-08-01"},
          "signing_secret_last4": "wxlA",
          "rate_limit_per_minute": 600,
          "digest_schedule": {"time_of_day": "09:00", "timezone": "America/New_York", "event_types": ["patient.state.changed"], "last_sent_date": null},
          "consecutive_failures": 0,
          "failure_streak_started_at": null,
          "auto_disabled_at": null,
          "rotated_at": null,
          "signing_secret": null
        }
        """;

    private const string DeliveryJson = """
        {
          "id": "del_1",
          "project_id": null,
          "destination_id": "dest_1",
          "destination_type": "webhook",
          "event_type": "patient.state.changed",
          "event_id": "evt_1",
          "status": "delivered",
          "attempts": [],
          "next_attempt_at": null,
          "first_attempted_at": null,
          "delivered_at": "2026-08-12T09:14:05Z",
          "dead_lettered_at": null,
          "last_error": null,
          "redelivery_of": null,
          "requested_by": "dispatcher",
          "batched_into": null,
          "payload": {"id": "del_1", "type": "patient.state.changed"}
        }
        """;

    [Fact]
    public void CreateActionDestination_DelegatesFullBody()
    {
        JsonElement? body = null;
        var mock = new MockHttpMessageHandler();
        mock.CaptureJson(HttpMethod.Post, $"{TestHelpers.BaseUrl}/v1/actions/destinations", DestinationJson, b => body = b);

        using var client = TestHelpers.CreateClient(mock);
        var destination = client.CreateActionDestination(
            webhookConfig: new WebhookDestinationConfig { Url = "https://hooks.example.com/olira" },
            subscribedTriggers: ["patient.state.changed", "log.no_state_change"],
            description: "Acme webhook",
            staticHeaders: new Dictionary<string, string> { ["X-Api-Key"] = "secret" },
            rateLimitPerMinute: 600,
            digestSchedule: new DigestSchedule { TimeOfDay = "09:00", Timezone = "America/New_York", Triggers = ["patient.state.changed"] });

        Assert.Equal("whsec_abc123", destination.SigningSecret);
        Assert.NotNull(body);
        var config = body.Value.RequireProperty("config");
        Assert.Equal("webhook", config.GetProperty("destination_type").GetString());
        Assert.Equal("https://hooks.example.com/olira", config.GetProperty("url").GetString());

        var triggers = body.Value.RequireProperty("subscribed_event_types");
        Assert.Equal(2, triggers.GetArrayLength());
        Assert.Equal("patient.state.changed", triggers[0].GetString());
        Assert.Equal("log.no_state_change", triggers[1].GetString());

        Assert.Equal("Acme webhook", body.Value.GetProperty("description").GetString());
        Assert.Equal(600, body.Value.GetProperty("rate_limit_per_minute").GetInt32());

        var digest = body.Value.RequireProperty("digest_schedule");
        Assert.Equal("09:00", digest.GetProperty("time_of_day").GetString());
        Assert.Equal("America/New_York", digest.GetProperty("timezone").GetString());
        Assert.Equal("patient.state.changed", digest.GetProperty("event_types")[0].GetString());
    }

    [Fact]
    public void CreateActionDestination_OmitsUnsetOptionals()
    {
        JsonElement? body = null;
        var mock = new MockHttpMessageHandler();
        mock.CaptureJson(HttpMethod.Post, $"{TestHelpers.BaseUrl}/v1/actions/destinations", DestinationJson, b => body = b);

        using var client = TestHelpers.CreateClient(mock);
        client.CreateActionDestination(webhookConfig: new WebhookDestinationConfig { Url = "https://hooks.example.com/olira" });

        Assert.NotNull(body);
        Assert.False(body.Value.TryGetProperty("subscribed_event_types", out _));
        Assert.False(body.Value.TryGetProperty("description", out _));
        Assert.False(body.Value.TryGetProperty("digest_schedule", out _));
    }

    [Fact]
    public void CreateActionDestination_AcceptsEmailConfig()
    {
        JsonElement? body = null;
        var mock = new MockHttpMessageHandler();
        mock.CaptureJson(HttpMethod.Post, $"{TestHelpers.BaseUrl}/v1/actions/destinations", DestinationJson, b => body = b);

        using var client = TestHelpers.CreateClient(mock);
        client.CreateActionDestination(emailConfig: new EmailDestinationConfig { ToEmail = "ops@acme.example" });

        Assert.NotNull(body);
        var config = body.Value.RequireProperty("config");
        Assert.Equal("email", config.GetProperty("destination_type").GetString());
        Assert.Equal("ops@acme.example", config.GetProperty("to_email").GetString());
    }

    [Fact]
    public void CreateActionDestination_RequiresExactlyOneConfig()
    {
        using var client = TestHelpers.CreateClient(new MockHttpMessageHandler());
        Assert.Throws<ArgumentException>(() => client.CreateActionDestination());
        Assert.Throws<ArgumentException>(() => client.CreateActionDestination(
            webhookConfig: new WebhookDestinationConfig { Url = "https://hooks.example.com/olira" },
            emailConfig: new EmailDestinationConfig { ToEmail = "ops@acme.example" }));
    }

    [Fact]
    public void UpdateActionDestination_SendsOnlySuppliedFields()
    {
        JsonElement? body = null;
        var mock = new MockHttpMessageHandler();
        mock.CaptureJson(HttpMethod.Patch, $"{TestHelpers.BaseUrl}/v1/actions/destinations/dest_1", DestinationJson, b => body = b);

        using var client = TestHelpers.CreateClient(mock);
        client.UpdateActionDestination("dest_1", status: "disabled");

        Assert.NotNull(body);
        Assert.Equal("disabled", body.Value.GetProperty("status").GetString());
        Assert.False(body.Value.TryGetProperty("url", out _));
        Assert.False(body.Value.TryGetProperty("digest_schedule", out _));
    }

    [Fact]
    public void UpdateActionDestination_ClearDigestScheduleSendsExplicitNull()
    {
        JsonElement? body = null;
        var mock = new MockHttpMessageHandler();
        mock.CaptureJson(HttpMethod.Patch, $"{TestHelpers.BaseUrl}/v1/actions/destinations/dest_1", DestinationJson, b => body = b);

        using var client = TestHelpers.CreateClient(mock);
        client.UpdateActionDestination("dest_1", clearDigestSchedule: true);

        Assert.NotNull(body);
        Assert.True(body.Value.TryGetProperty("digest_schedule", out var digest));
        Assert.Equal(JsonValueKind.Null, digest.ValueKind);
    }

    [Fact]
    public void UpdateActionDestination_OmittedDigestScheduleKeyAbsent()
    {
        JsonElement? body = null;
        var mock = new MockHttpMessageHandler();
        mock.CaptureJson(HttpMethod.Patch, $"{TestHelpers.BaseUrl}/v1/actions/destinations/dest_1", DestinationJson, b => body = b);

        using var client = TestHelpers.CreateClient(mock);
        client.UpdateActionDestination("dest_1", description: "new desc");

        Assert.NotNull(body);
        Assert.False(body.Value.TryGetProperty("digest_schedule", out _));
    }

    [Fact]
    public void UpdateActionDestination_RejectsDigestScheduleAndClearFlagTogether()
    {
        using var client = TestHelpers.CreateClient(new MockHttpMessageHandler());
        Assert.Throws<ArgumentException>(() => client.UpdateActionDestination(
            "dest_1",
            digestSchedule: new DigestSchedule { TimeOfDay = "09:00" },
            clearDigestSchedule: true));
    }

    [Fact]
    public void GetActionDestination_HappyPath()
    {
        var mock = new MockHttpMessageHandler();
        mock.When(HttpMethod.Get, $"{TestHelpers.BaseUrl}/v1/actions/destinations/dest_1")
            .Respond("application/json", DestinationJson);

        using var client = TestHelpers.CreateClient(mock);
        var destination = client.GetActionDestination("dest_1");

        Assert.Equal("dest_1", destination.Id);
        Assert.Equal(["patient.state.changed"], destination.SubscribedTriggers);
    }

    [Fact]
    public void DeleteActionDestination_HappyPath()
    {
        var mock = new MockHttpMessageHandler();
        mock.When(HttpMethod.Delete, $"{TestHelpers.BaseUrl}/v1/actions/destinations/dest_1")
            .Respond("application/json", """{"message":"Destination disabled","dead_lettered_deliveries":2}""");

        using var client = TestHelpers.CreateClient(mock);
        var result = client.DeleteActionDestination("dest_1");

        Assert.Equal("Destination disabled", result.Message);
        Assert.Equal(2, result.DeadLetteredDeliveries);
    }

    [Fact]
    public void RotateActionDestinationSecret_HappyPath()
    {
        var mock = new MockHttpMessageHandler();
        mock.When(HttpMethod.Post, $"{TestHelpers.BaseUrl}/v1/actions/destinations/dest_1/rotate-secret")
            .Respond("application/json", DestinationJson);

        using var client = TestHelpers.CreateClient(mock);
        var destination = client.RotateActionDestinationSecret("dest_1");

        Assert.Equal("whsec_abc123", destination.SigningSecret);
    }

    [Fact]
    public void ListActionDestinations_HappyPath()
    {
        var mock = new MockHttpMessageHandler();
        mock.When(HttpMethod.Get, $"{TestHelpers.BaseUrl}/v1/actions/destinations")
            .Respond("application/json", $$"""{"data":[{{DestinationJson}}],"total":1}""");

        using var client = TestHelpers.CreateClient(mock);
        var result = client.ListActionDestinations();

        Assert.Equal(1, result.Total);
        Assert.Single(result.Data);
    }

    [Fact]
    public void ListActionDeliveries_ParamsPassthroughAndOmission()
    {
        Uri? requestUri = null;
        var mock = new MockHttpMessageHandler();
        mock.When(HttpMethod.Get, $"{TestHelpers.BaseUrl}/v1/actions/deliveries*")
            .Respond(request =>
            {
                requestUri = request.RequestUri;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"data":[],"next_cursor":null}""", System.Text.Encoding.UTF8, "application/json"),
                });
            });

        using var client = TestHelpers.CreateClient(mock);
        client.ListActionDeliveries(destinationId: "dest_1", status: "delivered", trigger: "patient.state.changed", cursor: "abc", limit: 10);

        Assert.NotNull(requestUri);
        var query = requestUri!.Query;
        Assert.Contains("destination_id=dest_1", query);
        Assert.Contains("status=delivered", query);
        Assert.Contains("event_type=patient.state.changed", query);
        Assert.Contains("cursor=abc", query);
        Assert.Contains("limit=10", query);
    }

    [Fact]
    public void GetActionDelivery_ParsesTriggerAliasAndPayload()
    {
        var mock = new MockHttpMessageHandler();
        mock.When(HttpMethod.Get, $"{TestHelpers.BaseUrl}/v1/actions/deliveries/del_1")
            .Respond("application/json", DeliveryJson);

        using var client = TestHelpers.CreateClient(mock);
        var delivery = client.GetActionDelivery("del_1");

        Assert.Equal("patient.state.changed", delivery.Trigger);
        Assert.NotNull(delivery.Payload);
        Assert.Equal("del_1", delivery.Payload!["id"].GetString());
    }

    [Fact]
    public void RedeliverActionDelivery_HappyPath()
    {
        var mock = new MockHttpMessageHandler();
        mock.When(HttpMethod.Post, $"{TestHelpers.BaseUrl}/v1/actions/deliveries/del_1/redeliver")
            .Respond("application/json", DeliveryJson.Replace("\"id\": \"del_1\"", "\"id\": \"del_2\"").Replace("\"redelivery_of\": null", "\"redelivery_of\": \"del_1\""));

        using var client = TestHelpers.CreateClient(mock);
        var redelivered = client.RedeliverActionDelivery("del_1");

        Assert.Equal("del_1", redelivered.RedeliveryOf);
    }

    [Fact]
    public void ActionTrigger_RecommendedDigestTriggersMatchesConsole()
    {
        Assert.Equal(new HashSet<string> { ActionTrigger.PatientStateChanged }, ActionTrigger.RecommendedDigestTriggers);
        Assert.Equal("integration.sync.failed", ActionTrigger.IntegrationSyncFailed);
    }

    [Fact]
    public void GetActionDestination_ParsesDigestScheduleTriggersFromEventTypesAlias()
    {
        // Regression: DigestSchedule.Triggers must round-trip through the server's
        // "event_types" wire key, not the naming-policy-derived "triggers".
        var mock = new MockHttpMessageHandler();
        mock.When(HttpMethod.Get, $"{TestHelpers.BaseUrl}/v1/actions/destinations/dest_2")
            .Respond("application/json", DestinationWithDigestJson);

        using var client = TestHelpers.CreateClient(mock);
        var destination = client.GetActionDestination("dest_2");

        Assert.NotNull(destination.DigestSchedule);
        Assert.Equal(["patient.state.changed"], destination.DigestSchedule!.Triggers);
        Assert.Equal("09:00", destination.DigestSchedule.TimeOfDay);
        Assert.Equal("America/New_York", destination.DigestSchedule.Timezone);
    }
}
