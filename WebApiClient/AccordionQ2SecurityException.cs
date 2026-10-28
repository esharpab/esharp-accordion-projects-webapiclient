namespace AccordionQ2.WebApiClient;

/// <summary>Why a station refused a request (security mode <c>On</c>).</summary>
public enum AccordionQ2SecurityReason
{
    /// <summary>401: the station needs an API key and this client has none.</summary>
    MissingApiKey,

    /// <summary>401: the station doesn't know this client's API key (wrong, or removed).</summary>
    InvalidApiKey,

    /// <summary>403: the key is known but its role is too low for this request; see <see cref="AccordionQ2SecurityException.RequiredRole"/>.</summary>
    RoleTooLow,
}

/// <summary>
/// Thrown when a station refuses a request for want of a valid API key (401) or of a high enough role (403).
/// The message names the cause and never contains the key.
/// </summary>
public sealed class AccordionQ2SecurityException : AccordionQ2ApiException
{
    /// <summary>Why the station refused.</summary>
    public AccordionQ2SecurityReason Reason { get; }

    /// <summary>The role the request needs (<c>Viewer</c>, <c>Operator</c> or <c>Admin</c>), when the station said.</summary>
    public string? RequiredRole { get; }

    /// <inheritdoc/>
    public AccordionQ2SecurityException(int statusCode, AccordionQ2SecurityReason reason, string? requiredRole, string message)
        : base(statusCode, message)
    {
        Reason = reason;
        RequiredRole = requiredRole;
    }
}
