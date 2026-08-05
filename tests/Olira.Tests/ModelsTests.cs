using System.Text.Json;
using Olira.Internal;
using Olira.Json;

namespace Olira.Tests;

public class ModelsTests
{
    [Fact]
    public void DefaultBaseUrl_MatchesProduction()
    {
        Assert.Equal("https://app-api.prod.olira.ai/app-api", OliraClient.DefaultBaseUrl);
    }

    [Fact]
    public void LogsResult_AcceptsNullTraceFields()
    {
        const string json = """
            {
              "patient_id": "p_123",
              "count": 2,
              "logs": [
                {
                  "id": "log_1",
                  "type": "symptom_report",
                  "timestamp": "2026-03-18T10:00:00+00:00",
                  "payload": {},
                  "trace": {"object_type": "conversation", "object_id": "conv-abc"}
                },
                {
                  "id": "log_2",
                  "type": "user_login",
                  "timestamp": "2026-03-18T10:01:00+00:00",
                  "payload": {},
                  "trace": {"object_type": null, "object_id": null}
                }
              ]
            }
            """;

        var result = JsonSerializer.Deserialize<LogsResult>(json, OliraJson.Default);
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.NotNull(result.Logs[1].Trace);
        Assert.Null(result.Logs[1].Trace!.ObjectType);
        Assert.Null(result.Logs[1].Trace!.ObjectId);
    }

    [Fact]
    public void LogsResult_ParsesIngestedAt()
    {
        const string json = """
            {
              "patient_id": "p_123",
              "count": 1,
              "logs": [
                {
                  "id": "log_1",
                  "type": "symptom_report",
                  "timestamp": "2026-03-18T10:00:00+00:00",
                  "ingested_at": "2026-03-18T10:00:05+00:00",
                  "payload": {}
                }
              ]
            }
            """;

        var result = JsonSerializer.Deserialize<LogsResult>(json, OliraJson.Default);
        Assert.NotNull(result);
        Assert.Equal("2026-03-18T10:00:05+00:00", result.Logs[0].IngestedAt);
    }

    [Fact]
    public void LogsResult_IngestedAt_DefaultsToNull()
    {
        const string json = """
            {
              "patient_id": "p_123",
              "count": 1,
              "logs": [
                {
                  "id": "log_1",
                  "type": "symptom_report",
                  "timestamp": "2026-03-18T10:00:00+00:00",
                  "payload": {}
                }
              ]
            }
            """;

        var result = JsonSerializer.Deserialize<LogsResult>(json, OliraJson.Default);
        Assert.NotNull(result);
        Assert.Null(result.Logs[0].IngestedAt);
    }

    [Fact]
    public void LogWire_RequiresCompleteTrace()
    {
        var ex = Assert.Throws<ValidationError>(() =>
            LogWire.FromSpec(new LogSpec(
                OliraLogType.ConversationCompleted,
                "p_abc",
                trace: new OliraTrace { ObjectType = null, ObjectId = "conv_789" })));

        Assert.Contains("trace requires both object_type and object_id", ex.Message);
    }

    [Fact]
    public void LogWire_AcceptsCompleteTrace()
    {
        var wire = LogWire.FromSpec(new LogSpec(
            OliraLogType.ConversationCompleted,
            "p_abc",
            trace: new OliraTrace { ObjectType = "conversation", ObjectId = "conv_789" }));

        Assert.NotNull(wire.Trace);
        Assert.Equal("conversation", wire.Trace!.ObjectType);
        Assert.Equal("conv_789", wire.Trace.ObjectId);
    }

    [Fact]
    public void PatientIdValidation_RejectsEmail()
    {
        var ex = Assert.Throws<ValidationError>(() => PatientIdValidation.Validate("user@example.com"));
        Assert.Contains("email", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreatePatientRequest_RequiresAnchorField()
    {
        var req = new CreatePatientRequest();
        var ex = Assert.Throws<ValidationError>(() => req.Validate());
        Assert.Contains("at least one of", ex.Message);
    }

    [Fact]
    public void CreatePatientRequest_AcceptsName()
    {
        var req = new CreatePatientRequest { FirstName = "Jane", LastName = "Smith" };
        req.Validate();
        Assert.Equal("Jane", req.FirstName);
    }
}
