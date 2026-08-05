namespace Olira;

/// <summary>Base exception for all Olira SDK errors.</summary>
public class OliraError : Exception
{
    /// <summary>Creates an <see cref="OliraError"/> with the given message.</summary>
    public OliraError(string message) : base(message) { }

    /// <summary>Creates an <see cref="OliraError"/> with the given message and inner exception.</summary>
    public OliraError(string message, Exception inner) : base(message, inner) { }
}

/// <summary>Raised on 401 Unauthorized or 403 Forbidden — invalid or revoked API key.</summary>
public class AuthError : OliraError
{
    /// <summary>Creates an <see cref="AuthError"/> with the given message.</summary>
    public AuthError(string message) : base(message) { }
}

/// <summary>Raised on 429 Too Many Requests. Includes retry_after from Retry-After header.</summary>
public class RateLimitError : OliraError
{
    /// <summary>Seconds to wait before retrying, from the Retry-After header.</summary>
    public int RetryAfter { get; }

    /// <summary>Creates a <see cref="RateLimitError"/>.</summary>
    public RateLimitError(string message, int retryAfter = 0) : base(message)
    {
        RetryAfter = retryAfter;
    }
}

/// <summary>
/// Raised on 422 or client-side validation failure (malformed event, PII in patient_id, etc.).
/// </summary>
public class ValidationError : OliraError
{
    /// <summary>Creates a <see cref="ValidationError"/> with the given message.</summary>
    public ValidationError(string message) : base(message) { }
}

/// <summary>Raised on 409 Conflict or 5xx server-side failure after retries exhausted.</summary>
public class ServerError : OliraError
{
    /// <summary>HTTP status code associated with the failure.</summary>
    public int StatusCode { get; }

    /// <summary>Creates a <see cref="ServerError"/>.</summary>
    public ServerError(string message, int statusCode = 0) : base(message)
    {
        StatusCode = statusCode;
    }
}

/// <summary>
/// Raised on connection timeout, DNS failure, or other network error after retries exhausted.
/// </summary>
public class NetworkError : OliraError
{
    /// <summary>Creates a <see cref="NetworkError"/> with the given message.</summary>
    public NetworkError(string message) : base(message) { }

    /// <summary>Creates a <see cref="NetworkError"/> with the given message and inner exception.</summary>
    public NetworkError(string message, Exception inner) : base(message, inner) { }
}
