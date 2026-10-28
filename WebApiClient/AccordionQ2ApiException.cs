namespace AccordionQ2.WebApiClient;

/// <summary>
/// Thrown when the AccordionQ2 API returns a non-success HTTP status code. A refusal for want of an API key
/// or role is the subclass <see cref="AccordionQ2SecurityException"/>.
/// </summary>
public class AccordionQ2ApiException : Exception
{
    /// <summary>HTTP status code returned by the API.</summary>
    public int StatusCode { get; }

    /// <inheritdoc/>
    public AccordionQ2ApiException(int statusCode, string message)
        : base(message) => StatusCode = statusCode;
}
