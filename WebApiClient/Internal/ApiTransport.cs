using Newtonsoft.Json;

namespace AccordionQ2.WebApiClient.Internal;

/// <summary>
/// The one way every group sends a request: it resolves the request's path against the station's base
/// address and adds the API key and the lease id to each request message, never to the
/// <see cref="HttpClient"/>'s <c>BaseAddress</c> or default headers, so a caller's shared HttpClient stays as it was.
/// </summary>
internal sealed class ApiTransport
{
    private readonly Uri _baseAddress;
    private readonly string? _apiKey;
    private volatile string? _leaseId;

    internal ApiTransport(HttpClient http, Uri baseAddress, string? apiKey)
    {
        Http = http;
        _baseAddress = baseAddress;
        _apiKey = ValidateApiKey(apiKey);
    }

    internal HttpClient Http { get; }

    internal bool HasApiKey => _apiKey is not null;

    /// <summary>The lease id sent as <c>X-Lease-Id</c>; null sends none.</summary>
    internal string? LeaseId
    {
        get => _leaseId;
        set => _leaseId = value;
    }

    internal Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
        SendAsync(request, HttpCompletionOption.ResponseContentRead, ct);

    internal Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, HttpCompletionOption completion, CancellationToken ct)
    {
        if (request.RequestUri is { IsAbsoluteUri: false } relative)
            request.RequestUri = new Uri(_baseAddress, relative);
        if (_apiKey is not null)
            request.Headers.TryAddWithoutValidation(AccordionQ2Client.ApiKeyHeader, _apiKey);
        if (_leaseId is { } lease)
            request.Headers.TryAddWithoutValidation(Groups.LeaseGroup.HeaderName, lease);
        return Http.SendAsync(request, completion, ct);
    }

    /// <summary>
    /// The exception for a refused request: <see cref="AccordionQ2SecurityException"/> for 401, and for a 403
    /// that names the role it needs, with the server's error after the client's own text; otherwise <see cref="AccordionQ2ApiException"/> with the server's error,
    /// or <paramref name="fallback"/>, or the body.
    /// </summary>
    internal AccordionQ2ApiException CreateError(int status, string body, string? fallback = null)
    {
        ErrorBody? error = null;
        try { error = JsonConvert.DeserializeObject<ErrorBody>(body); } catch { }
        var serverMessage = Scrub(error?.Error);

        if (status == 401)
        {
            var reason = HasApiKey ? AccordionQ2SecurityReason.InvalidApiKey : AccordionQ2SecurityReason.MissingApiKey;
            var message = HasApiKey
                ? "The station doesn't accept this client's API key (X-Api-Key): it is wrong, or was removed on the station"
                : "The station needs an API key (X-Api-Key) and this client has none; set AccordionQ2ClientOptions.ApiKey";
            // The server's own reason too: the 401 may come from something else, e.g. a proxy.
            if (serverMessage is not null)
                message += $" (station: {serverMessage})";
            return new AccordionQ2SecurityException(status, reason, error?.Requires, message);
        }
        if (status == 403 && !string.IsNullOrEmpty(error?.Requires))
        {
            var message = $"This needs the {error!.Requires} role, which this client's API key doesn't have";
            if (serverMessage is not null)
                message += $" (station: {serverMessage})";
            return new AccordionQ2SecurityException(status, AccordionQ2SecurityReason.RoleTooLow, error.Requires, message);
        }
        if (status is >= 300 and < 400 && serverMessage is null)
            // Not followed (see AccordionQ2Client.CreateHandler), and the Location isn't repeated.
            return new AccordionQ2ApiException(status,
                $"The server answered with a redirect (HTTP {status}), which the station's API never does; the client doesn't follow it. Check the base URL");
        return new AccordionQ2ApiException(status, serverMessage ?? fallback ?? Scrub(body) ?? string.Empty);
    }

    /// <summary>Text from the server, unless it would repeat the key.</summary>
    private string? Scrub(string? text) =>
        text is null || _apiKey is null || text.IndexOf(_apiKey, StringComparison.Ordinal) < 0 ? text : "(withheld: it contained the API key)";

    private static string? ValidateApiKey(string? apiKey)
    {
        if (apiKey is null)
            return null;
        if (apiKey.Length == 0)
            throw new ArgumentException("The API key is empty; use null for none", nameof(AccordionQ2ClientOptions.ApiKey));
        foreach (var c in apiKey)
            if (c < '!' || c > '~')
                // Not the key itself: an exception message must never carry it.
                throw new ArgumentException("The API key contains characters a header can't carry (only visible ASCII, no spaces)", nameof(AccordionQ2ClientOptions.ApiKey));
        return apiKey;
    }

    private sealed class ErrorBody
    {
        public string? Error { get; set; }
        public string? Requires { get; set; }
    }
}
