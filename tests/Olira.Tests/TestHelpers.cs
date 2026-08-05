using System.Net;
using System.Text;
using System.Text.Json;
using Olira.Internal;
using Olira.Json;
using RichardSzalay.MockHttp;

namespace Olira.Tests;

internal static class TestHelpers
{
    public const string BaseUrl = "https://api.test.olira.ai";

    public static HttpTransport CreateTransport(
        MockHttpMessageHandler mock,
        int maxRetries = 3) =>
        new(BaseUrl, "olira_test_key", maxRetries: maxRetries, handler: mock);

    public static OliraClient CreateClient(
        MockHttpMessageHandler mock,
        OliraEnv environment = OliraEnv.Development,
        bool asyncFlush = false) =>
        new(
            apiKey: "olira_test_key",
            environment: environment,
            baseUrl: BaseUrl,
            asyncFlush: asyncFlush,
            httpHandler: mock);

    /// <summary>
    /// Captures the JSON body of the first matching request and returns a fixed response.
    /// </summary>
    public static MockedRequest CaptureJson(
        this MockHttpMessageHandler mock,
        HttpMethod method,
        string url,
        string responseJson,
        Action<JsonElement> onBody,
        HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        return mock.When(method, url).Respond(async request =>
        {
            if (request.Content is not null)
            {
                var text = await request.Content.ReadAsStringAsync().ConfigureAwait(false);
                using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(text) ? "{}" : text);
                onBody(doc.RootElement.Clone());
            }
            else
            {
                onBody(default);
            }

            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json"),
            };
        });
    }

    public static string EmptyQueryResultJson(int count = 0, string? rowsJson = null) =>
        $$"""{"count":{{count}},"rows":{{rowsJson ?? "[]"}}}""";

    public static JsonElement RequireProperty(this JsonElement element, string name)
    {
        Assert.True(element.TryGetProperty(name, out var prop), $"Missing property '{name}'");
        return prop;
    }
}
