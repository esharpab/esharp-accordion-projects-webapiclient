namespace AccordionQ2.WebApiClient;

/// <summary>
/// Thrown when a station slows this client down (429, firmware 7.1.0 in security mode <c>On</c>; FW7 SEC-06, CRA-08):
/// too many failed keys, tokens, passwords from this address, or too many requests without a key to the
/// anonymous endpoints. The station answers 429 before it acts, so nothing was changed; try again after
/// <see cref="RetryAfter"/>. With <see cref="AccordionQ2ClientOptions.MaxThrottleWait"/> set, the client waits and
/// resends by itself first, and throws this only when the wait would be longer or a few resends were refused too.
/// </summary>
public sealed class AccordionQ2ThrottledException : AccordionQ2ApiException
{
    /// <summary>How long to wait before trying again, from the answer's <c>Retry-After</c> (seconds or an HTTP date); null without one.</summary>
    public TimeSpan? RetryAfter { get; }

    /// <summary>The station's own message (its <c>error</c>), e.g. <c>Too many failed keys from 192.168.0.58; try again in 2 s</c>; null when it gave none.</summary>
    public string? StationMessage { get; }

    /// <summary>The role the endpoint needs (the station's <c>requires</c>), when it said.</summary>
    public string? RequiredRole { get; }

    /// <inheritdoc/>
    public AccordionQ2ThrottledException(int statusCode, TimeSpan? retryAfter, string? stationMessage, string? requiredRole, string message)
        : base(statusCode, message)
    {
        RetryAfter = retryAfter;
        StationMessage = stationMessage;
        RequiredRole = requiredRole;
    }
}
