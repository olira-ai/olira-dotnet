using Olira;
using Olira.Examples;

/*
 * Olira SDK — Patient Token
 *
 * Patient tokens are short-lived JWTs (15 min) scoped to a single patient.
 * Use them when an AI agent or patient-facing device needs to call the Olira
 * MCP Patient State server — pass the token as a Bearer header. The client
 * never sees your API key.
 *
 * When to use:
 *   - Agent session: mint per MCP session, pass as Bearer auth
 *   - Device/frontend: your backend mints on demand and forwards it
 *   - NOT for server-to-server: use your API key with sdk:state-read directly
 *
 * Requires: sdk:patient-token scope
 * Run: dotnet run --project 07_PatientToken -- <patient_id>
 */

ExampleEnv.Load();

var apiKey = Environment.GetEnvironmentVariable("OLIRA_API_KEY");
if (string.IsNullOrWhiteSpace(apiKey))
{
    Console.WriteLine("Error: OLIRA_API_KEY is not set.");
    Console.WriteLine("  Copy examples/.env.example to examples/.env and fill in your API key.");
    Environment.Exit(1);
}

var baseUrl = ExampleEnv.BaseUrl;
var patientId = args.Length > 0 ? args[0] : ExampleEnv.Get("PATIENT_ID", "");

if (string.IsNullOrWhiteSpace(patientId))
{
    Console.WriteLine("Usage: dotnet run --project 07_PatientToken -- <patient_id>");
    Console.WriteLine("  Or set PATIENT_ID in your .env file.");
    Environment.Exit(1);
}

using var client = new OliraClient(
    apiKey: apiKey,
    baseUrl: baseUrl,
    environment: ExampleEnv.EnvForBaseUrl(baseUrl),
    asyncFlush: false);

try
{
    // ── Mint a token ──────────────────────────────────────────────────────────────

    Console.WriteLine($"Minting patient token for {patientId}");
    var token = client.GetPatientToken(patientId);

    var preview = token.AccessToken.Length > 40 ? token.AccessToken[..40] + "…" : token.AccessToken;
    Console.WriteLine($"  access_token: {preview}");
    Console.WriteLine($"  expires_in:   {token.ExpiresIn}s ({token.ExpiresIn / 60} min)");
    Console.WriteLine($"  token_type:   {token.TokenType}");
    Console.WriteLine($"  scopes:       [{string.Join(", ", token.Scopes)}]");

    // ── Forwarding to an MCP client ───────────────────────────────────────────────
    //
    // Pass token.AccessToken as a Bearer header to the MCP Patient State server.
    // Use standard JSON-RPC 2.0 — tools are called via method="tools/call":
    //
    //   using var http = new HttpClient();
    //   http.DefaultRequestHeaders.Authorization =
    //       new AuthenticationHeaderValue("Bearer", token.AccessToken);
    //   var resp = await http.PostAsJsonAsync(
    //       "https://mcp.prod.olira.ai/mcp",
    //       new {
    //           jsonrpc = "2.0",
    //           id = 1,
    //           method = "tools/call",
    //           @params = new {
    //               name = "get_view_block",
    //               arguments = new {
    //                   view_type = "weekly_health_summary",
    //                   block_id = "symptoms_overview",
    //               },
    //           },
    //       });
    //   // patient_id is not required in arguments — it is locked to the token's patient.

    // ── Session helper with automatic refresh ────────────────────────────────────
    //
    // Tokens expire after 15 minutes. Mint a fresh one for each session, or use a
    // helper like this to refresh automatically with a safety buffer.

    var session = new PatientSession(client, patientId);
    var first = session.Bearer();
    var cached = session.Bearer(); // no network call
    Console.WriteLine($"\nBearer (first call):  {(first.Length > 40 ? first[..40] + "…" : first)}");
    Console.WriteLine($"Bearer (cached call): {(cached.Length > 40 ? cached[..40] + "…" : cached)}");

    // ── Error handling ────────────────────────────────────────────────────────────

    try
    {
        client.GetPatientToken("not-a-valid-id");
    }
    catch (AuthError e)
    {
        Console.WriteLine($"\nAuthError (invalid patient or missing scope): {e.Message}");
    }
    catch (Exception e)
    {
        Console.WriteLine($"\nError: {e.GetType().Name}: {e.Message}");
    }
}
finally
{
    // Dispose via using; Close() is an alias for Dispose.
}

/// <summary>Caches a patient token and refreshes it 30 seconds before expiry.</summary>
sealed class PatientSession
{
    private readonly OliraClient _client;
    private readonly string _patientId;
    private string? _token;
    private DateTimeOffset _expiresAt = DateTimeOffset.MinValue;

    public PatientSession(OliraClient oliraClient, string patientId)
    {
        _client = oliraClient;
        _patientId = patientId;
    }

    public string Bearer()
    {
        if (DateTimeOffset.UtcNow >= _expiresAt - TimeSpan.FromSeconds(30))
        {
            var tok = _client.GetPatientToken(_patientId);
            _token = tok.AccessToken;
            _expiresAt = DateTimeOffset.UtcNow.AddSeconds(tok.ExpiresIn);
            Console.WriteLine($"  [PatientSession] Token refreshed, valid for {tok.ExpiresIn}s");
        }

        return _token!;
    }
}
