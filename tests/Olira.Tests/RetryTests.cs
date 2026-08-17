using System.Net;
using System.Net.Http.Headers;
using Olira.Internal;
using RichardSzalay.MockHttp;

namespace Olira.Tests;

public class RetryTests
{
    private const string BaseUrl = "https://api.test.olira.ai";

    [Fact]
    public void Status401_RaisesAuthError_WithoutRetry()
    {
        var calls = 0;
        var mock = new MockHttpMessageHandler();
        mock.When(HttpMethod.Post, $"{BaseUrl}/v1/logs/batch")
            .Respond(_ =>
            {
                calls++;
                return new HttpResponseMessage(HttpStatusCode.Unauthorized)
                {
                    Content = new StringContent("Unauthorized"),
                };
            });

        using var transport = new HttpTransport(BaseUrl, "olira_test_key", maxRetries: 2, handler: mock);
        var ex = Assert.Throws<AuthError>(() =>
            transport.SendBatch([new Dictionary<string, object?>
            {
                ["log_type"] = "user_login",
                ["patient_id"] = "p_1",
                ["context"] = new Dictionary<string, string>(),
            }]));

        Assert.Contains("401", ex.Message);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void Status429_ParsesRetryAfter()
    {
        var mock = new MockHttpMessageHandler();
        mock.When(HttpMethod.Post, $"{BaseUrl}/v1/logs/batch")
            .Respond(_ =>
            {
                var response = new HttpResponseMessage((HttpStatusCode)429)
                {
                    Content = new StringContent("Too Many Requests"),
                };
                response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(120));
                return response;
            });

        using var transport = new HttpTransport(BaseUrl, "olira_test_key", maxRetries: 0, handler: mock);
        var ex = Assert.Throws<RateLimitError>(() =>
            transport.SendBatch([new Dictionary<string, object?>
            {
                ["log_type"] = "user_login",
                ["patient_id"] = "p_1",
                ["context"] = new Dictionary<string, string>(),
            }]));

        Assert.Equal(120, ex.RetryAfter);
    }

    [Fact]
    public void CreateProject_DoesNotRetryOn500()
    {
        var calls = 0;
        var mock = new MockHttpMessageHandler();
        mock.When(HttpMethod.Post, $"{BaseUrl}/v1/projects")
            .Respond(_ =>
            {
                calls++;
                return new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent("Internal Server Error"),
                };
            });

        using var transport = new HttpTransport(BaseUrl, "olira_test_key", maxRetries: 3, handler: mock);
        Assert.Throws<ServerError>(() =>
            transport.CreateProject(new Dictionary<string, object?> { ["name"] = "Dev Sandbox" }));
        Assert.Equal(1, calls);
    }

    [Fact]
    public void LogFhir_WithoutIdempotencyKey_DoesNotRetryOn500()
    {
        // No key means no stable dedup anchor server-side — a lost response could
        // otherwise be replayed by the transport itself and duplicate the event.
        var calls = 0;
        var mock = new MockHttpMessageHandler();
        mock.When(HttpMethod.Post, $"{BaseUrl}/v1/fhir/resource")
            .Respond(_ =>
            {
                calls++;
                return new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent("Internal Server Error"),
                };
            });

        using var transport = new HttpTransport(BaseUrl, "olira_test_key", maxRetries: 3, handler: mock);
        Assert.Throws<ServerError>(() =>
            transport.LogFhir("p_1", new Dictionary<string, object?> { ["resourceType"] = "Patient", ["id"] = "abc" }));
        Assert.Equal(1, calls);
    }

    [Fact]
    public void LogFhir_WithIdempotencyKey_RetriesOn500()
    {
        // A caller-supplied key makes the server-side dedup anchor stable, so the
        // transport's own retry is safe — must retry the way any other idempotent call does.
        var calls = 0;
        var mock = new MockHttpMessageHandler();
        mock.When(HttpMethod.Post, $"{BaseUrl}/v1/fhir/resource")
            .Respond(_ =>
            {
                calls++;
                if (calls < 2)
                {
                    return new HttpResponseMessage(HttpStatusCode.InternalServerError)
                    {
                        Content = new StringContent("Internal Server Error"),
                    };
                }

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"accepted":1,"failed":0,"errors":[]}"""),
                };
            });

        using var transport = new HttpTransport(BaseUrl, "olira_test_key", maxRetries: 3, handler: mock);
        var result = transport.LogFhir(
            "p_1",
            new Dictionary<string, object?> { ["resourceType"] = "Patient", ["id"] = "abc" },
            idempotencyKey: "retry-key-1");

        Assert.Equal(1, result.Accepted);
        Assert.Equal(2, calls);
    }
}
