#nullable enable

namespace Olira;

public sealed partial class OliraClient
{
    /// <summary>
    /// List every log type in the platform catalog, with its full payload JSON Schema.
    /// Requires sdk:event-log scope. This is the live counterpart to the static
    /// <see cref="OliraLogType"/> constants — always current, and not limited to the
    /// subtypes known when this SDK version was released.
    /// </summary>
    public LogTypeListResult ListLogTypes()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _transport.ListLogTypes();
    }

    /// <summary>Get one log type by subtype or alias. Requires sdk:event-log scope.</summary>
    public LogType GetLogType(string subtype)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _transport.GetLogType(subtype);
    }
}
