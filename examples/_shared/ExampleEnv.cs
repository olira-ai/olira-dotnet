namespace Olira.Examples;

/// <summary>Loads examples/.env (and process env) for runnable sample programs.</summary>
internal static class ExampleEnv
{
    public static void Load(string? examplesDir = null)
    {
        var dir = examplesDir ?? FindExamplesDir();
        var envPath = Path.Combine(dir, ".env");
        if (!File.Exists(envPath)) return;
        foreach (var line in File.ReadAllLines(envPath))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#')) continue;
            var i = trimmed.IndexOf('=');
            if (i <= 0) continue;
            var key = trimmed[..i].Trim();
            var value = trimmed[(i + 1)..].Trim().Trim('"');
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
                Environment.SetEnvironmentVariable(key, value);
        }
    }

    public static string Require(string key)
    {
        var v = Environment.GetEnvironmentVariable(key);
        if (string.IsNullOrWhiteSpace(v))
            throw new InvalidOperationException($"Set {key} in examples/.env (copy from .env.example).");
        return v;
    }

    public static string Get(string key, string fallback) =>
        Environment.GetEnvironmentVariable(key) is { Length: > 0 } v ? v : fallback;

    public static string BaseUrl => Get("OLIRA_BASE_URL", OliraClient.DefaultBaseUrl);

    public static OliraEnv EnvForBaseUrl(string baseUrl) =>
        baseUrl.Contains("localhost", StringComparison.OrdinalIgnoreCase)
            ? OliraEnv.Development
            : OliraEnv.Production;

    private static string FindExamplesDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, ".env.example");
            if (File.Exists(candidate)) return dir.FullName;
            // walk up from bin/Debug/net8.0 → project → examples/
            var parentExamples = Path.Combine(dir.FullName, "..", "..", "..", "..");
            var resolved = Path.GetFullPath(parentExamples);
            if (File.Exists(Path.Combine(resolved, ".env.example"))) return resolved;
            dir = dir.Parent;
        }
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
    }
}
