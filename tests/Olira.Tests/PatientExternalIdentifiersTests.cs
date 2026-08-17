using System.Text.Json;
using RichardSzalay.MockHttp;

namespace Olira.Tests;

public class PatientExternalIdentifiersTests
{
    [Fact]
    public void ExternalIdentifier_DefaultsIntegrationIdToNull()
    {
        var ident = new ExternalIdentifier { System = "qurate", Value = "Q1" };
        Assert.Null(ident.IntegrationId);
    }

    [Fact]
    public void UpdatePatient_RejectsEmptyExternalIdentifiersClientSide()
    {
        var mock = new MockHttpMessageHandler();
        using var client = TestHelpers.CreateClient(mock);

        Assert.Throws<ValidationError>(
            () => client.UpdatePatient("p1", externalIdentifiers: []));
    }

    [Fact]
    public async Task UpdatePatientAsync_RejectsEmptyExternalIdentifiersClientSide()
    {
        var mock = new MockHttpMessageHandler();
        using var client = TestHelpers.CreateClient(mock);

        await Assert.ThrowsAsync<ValidationError>(
            () => client.UpdatePatientAsync("p1", externalIdentifiers: []));
    }

    [Fact]
    public void UpdatePatient_OmitsIntegrationIdWhenUnset()
    {
        JsonElement? body = null;
        var mock = new MockHttpMessageHandler();
        mock.CaptureJson(
            HttpMethod.Put,
            $"{TestHelpers.BaseUrl}/v1/patients/p1",
            """{"id":"p1","timezone":"UTC","status":"active","external_identifiers":[]}""",
            b => body = b);

        using var client = TestHelpers.CreateClient(mock);
        client.UpdatePatient(
            "p1",
            externalIdentifiers: [new ExternalIdentifier { System = "epic", Value = "MRN1" }]);

        Assert.NotNull(body);
        var identifiers = body.Value.RequireProperty("external_identifiers");
        Assert.False(identifiers[0].TryGetProperty("integration_id", out _));
    }

    [Fact]
    public void UpdatePatient_SendsIntegrationIdWhenEchoed()
    {
        JsonElement? body = null;
        var mock = new MockHttpMessageHandler();
        mock.CaptureJson(
            HttpMethod.Put,
            $"{TestHelpers.BaseUrl}/v1/patients/p1",
            """{"id":"p1","timezone":"UTC","status":"active","external_identifiers":[]}""",
            b => body = b);

        using var client = TestHelpers.CreateClient(mock);
        client.UpdatePatient(
            "p1",
            externalIdentifiers: [new ExternalIdentifier { System = "epic", Value = "MRN1", IntegrationId = "itg_1" }]);

        Assert.NotNull(body);
        var identifiers = body.Value.RequireProperty("external_identifiers");
        Assert.Equal("itg_1", identifiers[0].GetProperty("integration_id").GetString());
    }

    [Fact]
    public void GetPatient_ParsesIntegrationIdFromResponse()
    {
        var mock = new MockHttpMessageHandler();
        mock.When(HttpMethod.Get, $"{TestHelpers.BaseUrl}/v1/patients/p1").Respond(
            "application/json",
            """{"id":"p1","timezone":"UTC","status":"active","external_identifiers":[{"system":"epic","value":"MRN1","integration_id":"itg_1"}]}""");

        using var client = TestHelpers.CreateClient(mock);
        var patient = client.GetPatient("p1");

        Assert.Equal("itg_1", patient.ExternalIdentifiers[0].IntegrationId);
    }

    [Fact]
    public void AddPatientExternalIdentifiers_StripsIntegrationIdFromBody()
    {
        JsonElement? body = null;
        var mock = new MockHttpMessageHandler();
        mock.CaptureJson(
            HttpMethod.Post,
            $"{TestHelpers.BaseUrl}/v1/patients/p1/external-identifiers",
            """{"patient_id":"p1","added":1,"removed":0,"skipped":0,"external_identifiers":[]}""",
            b => body = b);

        using var client = TestHelpers.CreateClient(mock);
        var result = client.AddPatientExternalIdentifiers(
            "p1",
            [new ExternalIdentifier { System = "epic", Value = "MRN1", IntegrationId = "itg_1" }]);

        Assert.NotNull(body);
        var identifiers = body.Value.RequireProperty("identifiers");
        Assert.False(identifiers[0].TryGetProperty("integration_id", out _));
        Assert.Equal(1, result.Added);
    }

    [Fact]
    public void RemovePatientExternalIdentifiers_SendsSystemAndValueMatcher()
    {
        JsonElement? body = null;
        var mock = new MockHttpMessageHandler();
        mock.CaptureJson(
            HttpMethod.Delete,
            $"{TestHelpers.BaseUrl}/v1/patients/p1/external-identifiers",
            """{"patient_id":"p1","added":0,"removed":1,"skipped":0,"external_identifiers":[]}""",
            b => body = b);

        using var client = TestHelpers.CreateClient(mock);
        var result = client.RemovePatientExternalIdentifiers(
            "p1",
            [new ExternalIdentifierMatcher { System = "epic", Value = "MRN1" }]);

        Assert.NotNull(body);
        var identifiers = body.Value.RequireProperty("identifiers");
        Assert.Equal("epic", identifiers[0].GetProperty("system").GetString());
        Assert.Equal("MRN1", identifiers[0].GetProperty("value").GetString());
        Assert.False(identifiers[0].TryGetProperty("integration_id", out _));
        Assert.Equal(1, result.Removed);
    }

    [Fact]
    public void RemovePatientExternalIdentifiers_SystemOnlyMatcherOmitsValueAndIntegrationId()
    {
        JsonElement? body = null;
        var mock = new MockHttpMessageHandler();
        mock.CaptureJson(
            HttpMethod.Delete,
            $"{TestHelpers.BaseUrl}/v1/patients/p1/external-identifiers",
            """{"patient_id":"p1","added":0,"removed":2,"skipped":0,"external_identifiers":[]}""",
            b => body = b);

        using var client = TestHelpers.CreateClient(mock);
        var result = client.RemovePatientExternalIdentifiers(
            "p1",
            [new ExternalIdentifierMatcher { System = "epic" }]);

        Assert.NotNull(body);
        var identifiers = body.Value.RequireProperty("identifiers");
        Assert.Equal("epic", identifiers[0].GetProperty("system").GetString());
        Assert.False(identifiers[0].TryGetProperty("value", out _));
        Assert.False(identifiers[0].TryGetProperty("integration_id", out _));
        Assert.Equal(2, result.Removed);
    }

    [Fact]
    public void RemovePatientExternalIdentifiers_IntegrationIdOnlyMatcherOmitsSystemAndValue()
    {
        JsonElement? body = null;
        var mock = new MockHttpMessageHandler();
        mock.CaptureJson(
            HttpMethod.Delete,
            $"{TestHelpers.BaseUrl}/v1/patients/p1/external-identifiers",
            """{"patient_id":"p1","added":0,"removed":2,"skipped":0,"external_identifiers":[]}""",
            b => body = b);

        using var client = TestHelpers.CreateClient(mock);
        var result = client.RemovePatientExternalIdentifiers(
            "p1",
            [new ExternalIdentifierMatcher { IntegrationId = "itg_1" }]);

        Assert.NotNull(body);
        var identifiers = body.Value.RequireProperty("identifiers");
        Assert.Equal("itg_1", identifiers[0].GetProperty("integration_id").GetString());
        Assert.False(identifiers[0].TryGetProperty("system", out _));
        Assert.False(identifiers[0].TryGetProperty("value", out _));
        Assert.Equal(2, result.Removed);
    }

    [Fact]
    public void RemovePatientExternalIdentifiers_RejectsEmptyMatcherClientSide()
    {
        var mock = new MockHttpMessageHandler();
        using var client = TestHelpers.CreateClient(mock);

        Assert.Throws<ValidationError>(
            () => client.RemovePatientExternalIdentifiers("p1", [new ExternalIdentifierMatcher()]));
    }

    [Fact]
    public void RemovePatientExternalIdentifiers_RejectsValueWithoutSystemClientSide()
    {
        var mock = new MockHttpMessageHandler();
        using var client = TestHelpers.CreateClient(mock);

        Assert.Throws<ValidationError>(
            () => client.RemovePatientExternalIdentifiers("p1", [new ExternalIdentifierMatcher { Value = "MRN1" }]));
    }

    [Fact]
    public async Task RemovePatientExternalIdentifiersAsync_RejectsEmptyMatcherClientSide()
    {
        var mock = new MockHttpMessageHandler();
        using var client = TestHelpers.CreateClient(mock);

        await Assert.ThrowsAsync<ValidationError>(
            () => client.RemovePatientExternalIdentifiersAsync("p1", [new ExternalIdentifierMatcher()]));
    }

    [Fact]
    public async Task RemovePatientExternalIdentifiersAsync_RejectsValueWithoutSystemClientSide()
    {
        var mock = new MockHttpMessageHandler();
        using var client = TestHelpers.CreateClient(mock);

        await Assert.ThrowsAsync<ValidationError>(
            () => client.RemovePatientExternalIdentifiersAsync("p1", [new ExternalIdentifierMatcher { Value = "MRN1" }]));
    }

    [Fact]
    public void ListPatients_SendsIntegrationIdFilter()
    {
        var mock = new MockHttpMessageHandler();
        mock.When(HttpMethod.Get, $"{TestHelpers.BaseUrl}/v1/patients")
            .WithQueryString("integration_id", "itg_1")
            .Respond("application/json", """{"patients":[],"total":0,"has_more":false}""");

        using var client = TestHelpers.CreateClient(mock);
        var result = client.ListPatients(integrationId: "itg_1");

        Assert.Equal(0, result.Total);
    }

    [Fact]
    public async Task AddPatientExternalIdentifiersAsync_StripsIntegrationIdFromBody()
    {
        JsonElement? body = null;
        var mock = new MockHttpMessageHandler();
        mock.CaptureJson(
            HttpMethod.Post,
            $"{TestHelpers.BaseUrl}/v1/patients/p1/external-identifiers",
            """{"patient_id":"p1","added":1,"removed":0,"skipped":0,"external_identifiers":[]}""",
            b => body = b);

        using var client = TestHelpers.CreateClient(mock);
        await client.AddPatientExternalIdentifiersAsync(
            "p1",
            [new ExternalIdentifier { System = "epic", Value = "MRN1", IntegrationId = "itg_1" }]);

        Assert.NotNull(body);
        var identifiers = body.Value.RequireProperty("identifiers");
        Assert.False(identifiers[0].TryGetProperty("integration_id", out _));
    }
}
