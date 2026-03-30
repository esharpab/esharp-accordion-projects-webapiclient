namespace AccordionQ2.WebApiClient;

/// <summary>
/// Thrown when the AccordionQ2 API returns a non-success HTTP status code.
/// </summary>
public sealed class AccordionQ2ApiException : Exception
{
    /// <summary>HTTP status code returned by the API.</summary>
    public int StatusCode { get; }

    /// <inheritdoc/>
    public AccordionQ2ApiException(int statusCode, string message)
        : base(message) => StatusCode = statusCode;
}
