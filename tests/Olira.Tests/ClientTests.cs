using System.Net;
using System.Text.Json;
using RichardSzalay.MockHttp;

namespace Olira.Tests;

public class ClientTests
{
    [Fact]
    public void Constructor_RequiresApiKey()
    {
        Assert.ThrowsAny<ArgumentException>(() => new OliraClient(apiKey: " "));
    }

    [Fact]
    public void ClientLog_BuildsEvent()
    {
        JsonElement? body = null;
        var mock = new MockHttpMessageHandler();
        mock.CaptureJson(
            HttpMethod.Post,
            $"{TestHelpers.BaseUrl}/v1/logs/batch",
            """{"accepted":1,"failed":0,"errors":[]}""",
            b => body = b);

        using var client = TestHelpers.CreateClient(mock, OliraEnv.Development, asyncFlush: false);
        client.Log(logType: OliraLogType.UserLogin, patientId: "p_123");

        Assert.NotNull(body);
        var logs = body.Value.RequireProperty("logs");
        Assert.Equal(1, logs.GetArrayLength());
        Assert.Equal("user_login", logs[0].GetProperty("log_type").GetString());
        Assert.Equal("p_123", logs[0].GetProperty("patient_id").GetString());
        Assert.Equal("development", logs[0].GetProperty("context").GetProperty("environment").GetString());
        Assert.Equal("csharp", logs[0].GetProperty("context").GetProperty("sdk_language").GetString());
        // Queued/sync-emit path includes null optional fields (Python model_dump parity).
        Assert.Equal(JsonValueKind.Null, logs[0].GetProperty("timestamp").ValueKind);
        Assert.Equal(JsonValueKind.Null, logs[0].GetProperty("metadata").ValueKind);
        Assert.Equal(JsonValueKind.Null, logs[0].GetProperty("trace").ValueKind);
    }

    [Fact]
    public void LogBatch_OmitsNullOptionalFields()
    {
        JsonElement? body = null;
        var mock = new MockHttpMessageHandler();
        mock.CaptureJson(
            HttpMethod.Post,
            $"{TestHelpers.BaseUrl}/v1/logs/batch",
            """{"accepted":1,"failed":0,"errors":[]}""",
            b => body = b);

        using var client = TestHelpers.CreateClient(mock);
        client.LogBatch([new LogSpec(OliraLogType.UserLogin, "p_123")]);

        Assert.NotNull(body);
        var log = body.Value.RequireProperty("logs")[0];
        Assert.False(log.TryGetProperty("timestamp", out _));
        Assert.False(log.TryGetProperty("metadata", out _));
        Assert.False(log.TryGetProperty("trace", out _));
    }

    [Fact]
    public void ClientLog_WithTrace()
    {
        JsonElement? body = null;
        var mock = new MockHttpMessageHandler();
        mock.CaptureJson(
            HttpMethod.Post,
            $"{TestHelpers.BaseUrl}/v1/logs/batch",
            """{"accepted":1,"failed":0,"errors":[]}""",
            b => body = b);

        using var client = TestHelpers.CreateClient(mock, asyncFlush: false);
        client.Log(
            logType: OliraLogType.ConversationCompleted,
            patientId: "p_abc",
            payload: new Dictionary<string, object?> { ["duration_seconds"] = 142 },
            trace: new OliraTrace { ObjectType = "conversation", ObjectId = "conv_789" });

        Assert.NotNull(body);
        var log = body.Value.RequireProperty("logs")[0];
        Assert.Equal("conversation_completed", log.GetProperty("log_type").GetString());
        Assert.Equal(142, log.GetProperty("payload").GetProperty("duration_seconds").GetInt32());
        Assert.Equal("conversation", log.GetProperty("trace").GetProperty("object_type").GetString());
        Assert.Equal("conv_789", log.GetProperty("trace").GetProperty("object_id").GetString());
    }

    [Fact]
    public void Flush_NoopWhenNoWorker()
    {
        using var client = TestHelpers.CreateClient(new MockHttpMessageHandler(), asyncFlush: false);
        client.Flush();
    }

    [Fact]
    public void CreatePatient_HappyPath()
    {
        var mock = new MockHttpMessageHandler();
        mock.When(HttpMethod.Post, $"{TestHelpers.BaseUrl}/v1/patients")
            .Respond(
                "application/json",
                """
                {
                  "id": "pat_abc",
                  "first_name": "Jane",
                  "last_name": "Smith",
                  "timezone": "America/New_York",
                  "status": "active"
                }
                """);

        using var client = TestHelpers.CreateClient(mock);
        var patient = client.CreatePatient(
            firstName: "Jane",
            lastName: "Smith",
            timezone: "America/New_York");

        Assert.Equal("pat_abc", patient.Id);
        Assert.Equal("Jane", patient.FirstName);
        Assert.Equal("Smith", patient.LastName);
    }

    [Fact]
    public void LogBatch_HappyPath()
    {
        JsonElement? body = null;
        var mock = new MockHttpMessageHandler();
        mock.CaptureJson(
            HttpMethod.Post,
            $"{TestHelpers.BaseUrl}/v1/logs/batch",
            """{"accepted":2,"failed":0,"errors":[]}""",
            b => body = b);

        using var client = TestHelpers.CreateClient(mock);
        var result = client.LogBatch(
        [
            new LogSpec(OliraLogType.UserLogin, "p_123"),
            new LogSpec(
                OliraLogType.SymptomReport,
                "p_123",
                payload: new Dictionary<string, object?>
                {
                    ["instrument"] = "esas_r",
                    ["symptoms"] = new List<object>
                    {
                        new Dictionary<string, object?> { ["name"] = "pain", ["score"] = 4 },
                    },
                }),
        ]);

        Assert.Equal(2, result.Accepted);
        Assert.Equal(0, result.Failed);
        Assert.NotNull(body);
        Assert.Equal(2, body.Value.RequireProperty("logs").GetArrayLength());
    }

    [Fact]
    public void GetPatient_HappyPath()
    {
        var mock = new MockHttpMessageHandler();
        mock.When(HttpMethod.Get, $"{TestHelpers.BaseUrl}/v1/patients/pat_1")
            .Respond(
                "application/json",
                """{"id":"pat_1","first_name":"Ada","last_name":"Lovelace","timezone":"UTC","status":"active"}""");

        using var client = TestHelpers.CreateClient(mock);
        var patient = client.GetPatient("pat_1");
        Assert.Equal("pat_1", patient.Id);
        Assert.Equal("Ada", patient.FirstName);
    }

    [Fact]
    public void DeletePatient_HappyPath()
    {
        var mock = new MockHttpMessageHandler();
        mock.When(HttpMethod.Delete, $"{TestHelpers.BaseUrl}/v1/patients/pat_1")
            .Respond(HttpStatusCode.NoContent);

        using var client = TestHelpers.CreateClient(mock);
        client.DeletePatient("pat_1");
    }

    [Fact]
    public void ListPatients_HappyPath()
    {
        var mock = new MockHttpMessageHandler();
        mock.When(HttpMethod.Get, $"{TestHelpers.BaseUrl}/v1/patients*")
            .Respond(
                "application/json",
                """
                {
                  "patients": [
                    {"id":"pat_1","first_name":"Jane","last_name":"Smith","timezone":"UTC","status":"active"}
                  ],
                  "total": 1,
                  "has_more": false
                }
                """);

        using var client = TestHelpers.CreateClient(mock);
        var result = client.ListPatients(limit: 50, offset: 0);
        Assert.Single(result.Patients);
        Assert.Equal(1, result.Total);
        Assert.False(result.HasMore);
    }
}
