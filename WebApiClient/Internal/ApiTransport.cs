using System.Text;
using Newtonsoft.Json;

namespace AccordionQ2.WebApiClient.Internal;

/// <summary>Which of the client's credentials a request carries.</summary>
internal enum Credentials
{
    /// <summary>The API key and the login token, whichever are set (the station uses the key when both are sent).</summary>
    All,
    /// <summary>Neither: a login or a claim, where a stale credential would only count as a failure.</summary>
    None,
    /// <summary>Only the login token: renew, logout and the own password act on a login, and a key sent along would win.</summary>
    TokenOnly,
}

/// <summary>
/// The one way every group sends a request: it resolves the request's path against the station's base
/// address and adds the API key, the login token and the lease id to each request message, never to the
/// <see cref="HttpClient"/>'s <c>BaseAddress</c> or default headers, so a caller's shared HttpClient stays as it was.
/// It also waits out a 429's <c>Retry-After</c> and resends, when the caller opted in (<see cref="MaxThrottleWait"/>).
/// </summary>
internal sealed class ApiTransport
{
    /// <summary>The most times one request is resent after a 429 (FW7 CLI-04 follow-up): a few, so a client never loops.</summary>
    internal const int MaxThrottleRetries = 3;

    private readonly Uri _baseAddress;
    private readonly string? _apiKey;
    private volatile string? _leaseId;
    private volatile string? _token;

    internal ApiTransport(HttpClient http, Uri baseAddress, string? apiKey, TimeSpan maxThrottleWait = default)
    {
        Http = http;
        _baseAddress = baseAddress;
        _apiKey = ValidateApiKey(apiKey);
        if (maxThrottleWait < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(AccordionQ2ClientOptions.MaxThrottleWait), "MaxThrottleWait can't be negative; zero turns the wait off");
        MaxThrottleWait = maxThrottleWait;
    }

    internal HttpClient Http { get; }

    internal bool HasApiKey => _apiKey is not null;

    /// <summary>
    /// Whether a request would go over plain HTTP to another host (not <c>localhost</c> or a loopback address): there a
    /// password, setup code or private key would cross the network in clear text before the station could refuse it, so
    /// the calls that carry one stop here instead (FW7 CLI-04 follow-up, as SEC-03 refuses them on the station).
    /// </summary>
    internal bool IsPlainHttpToAnotherHost => _baseAddress.Scheme == Uri.UriSchemeHttp && !_baseAddress.IsLoopback;

    /// <summary>The station's base address every request path is resolved against.</summary>
    internal Uri BaseAddress => _baseAddress;

    /// <summary>How long a 429's Retry-After may be for the transport to wait and resend; zero never waits.</summary>
    internal TimeSpan MaxThrottleWait { get; }

    /// <summary>The lease id sent as <c>X-Lease-Id</c>; null sends none.</summary>
    internal string? LeaseId
    {
        get => _leaseId;
        set => _leaseId = value;
    }

    /// <summary>The login token sent as <c>Authorization: Bearer</c> (FW7 SEC-03); null sends none.</summary>
    internal string? Token
    {
        get => _token;
        set => _token = value is null ? null : ValidateToken(value);
    }

    /// <summary>
    /// Sends the request <paramref name="create"/> makes. A 429 whose Retry-After is at most <see cref="MaxThrottleWait"/>
    /// is waited out and the request made and sent again, at most <see cref="MaxThrottleRetries"/> times, when
    /// <paramref name="resendable"/>: the station answers 429 before it acts on anything, so resending changes nothing
    /// twice, but a stream that was read once can't be sent again. The answer's <c>RequestMessage</c> is the request that
    /// was sent, so <see cref="CreateError(HttpResponseMessage, string, string?, bool)"/> knows which credential it carried.
    /// </summary>
    internal async Task<HttpResponseMessage> SendAsync(Func<HttpRequestMessage> create, HttpCompletionOption completion, CancellationToken ct,
        bool resendable = true, Credentials credentials = Credentials.All)
    {
        for (var attempt = 0; ; attempt++)
        {
            var request = create();
            Prepare(request, credentials);
            var response = await Http.SendAsync(request, completion, ct).ConfigureAwait(false);
            response.RequestMessage ??= request;
            // MaxThrottleWait zero is off: not even a Retry-After of 0 (or a date already past) is resent
            if ((int)response.StatusCode != 429 || !resendable || attempt >= MaxThrottleRetries || MaxThrottleWait <= TimeSpan.Zero
                || RetryAfterOf(response, DateTimeOffset.UtcNow) is not { } wait || wait > MaxThrottleWait)
                return response;
            response.Dispose();
            request.Dispose();
            await Task.Delay(wait, ct).ConfigureAwait(false);
        }
    }

    internal Task<HttpResponseMessage> SendAsync(Func<HttpRequestMessage> create, CancellationToken ct, bool resendable = true, Credentials credentials = Credentials.All) =>
        SendAsync(create, HttpCompletionOption.ResponseContentRead, ct, resendable, credentials);

    private void Prepare(HttpRequestMessage request, Credentials credentials)
    {
        if (request.RequestUri is { IsAbsoluteUri: false } relative)
            request.RequestUri = new Uri(_baseAddress, relative);
        if (_apiKey is not null && credentials == Credentials.All)
            request.Headers.TryAddWithoutValidation(AccordionQ2Client.ApiKeyHeader, _apiKey);
        if (_token is { } token && credentials != Credentials.None)
            request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + token);
        if (_leaseId is { } lease)
            request.Headers.TryAddWithoutValidation(Groups.LeaseGroup.HeaderName, lease);
    }

    /// <summary>
    /// A 429's (or 503's) Retry-After as a wait from <paramref name="now"/>: seconds, or an HTTP date (never below zero);
    /// null without the header.
    /// </summary>
    internal static TimeSpan? RetryAfterOf(HttpResponseMessage response, DateTimeOffset now)
    {
        if (response.Headers.RetryAfter is not { } header)
            return null;
        if (header.Delta is { } delta)
            return delta < TimeSpan.Zero ? TimeSpan.Zero : delta;
        if (header.Date is { } date)
            return date <= now ? TimeSpan.Zero : date - now;
        return null;
    }

    /// <summary>The most of an error body <see cref="ReadErrorBodyAsync(HttpResponseMessage, CancellationToken)"/> reads: 64 KiB.</summary>
    internal const int MaxErrorBody = 64 * 1024;

    /// <summary>
    /// How long <see cref="ReadErrorBodyAsync(HttpResponseMessage, CancellationToken)"/> waits for an error body:
    /// 5 s. A streaming call reads it after the headers, where <see cref="HttpClient.Timeout"/> no longer applies.
    /// </summary>
    internal static readonly TimeSpan ErrorBodyTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Up to <see cref="MaxErrorBody"/> bytes of an error response's body as UTF-8 text, for
    /// <see cref="CreateError(HttpResponseMessage, string, string?, bool)"/> on a call that streams (whose body isn't read already). A body that can't be
    /// read, or doesn't end within <see cref="ErrorBodyTimeout"/>, gives an empty string, so the status still shows.
    /// A truncated JSON body doesn't parse, and CreateError then uses its fallback. Cancelling <paramref name="ct"/>
    /// still throws.
    /// </summary>
    internal static Task<string> ReadErrorBodyAsync(HttpResponseMessage response, CancellationToken ct) =>
        ReadErrorBodyAsync(response, ErrorBodyTimeout, ct);

    internal static async Task<string> ReadErrorBodyAsync(HttpResponseMessage response, TimeSpan limit, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(limit);
        try
        {
            using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            // A stream that ignores the token (.NET Framework's) ends its read when it is closed.
            using var close = timeout.Token.Register(() => stream.Dispose());
            var buffer = new byte[MaxErrorBody];
            var length = 0;
            while (length < buffer.Length)
            {
                var read = await stream.ReadAsync(buffer, length, buffer.Length - length, timeout.Token).ConfigureAwait(false);
                if (read == 0)
                    break;
                length += read;
            }
            return Encoding.UTF8.GetString(buffer, 0, length);
        }
        catch (Exception) when (!ct.IsCancellationRequested && timeout.IsCancellationRequested)
        {
            return string.Empty; // the body stalled: its status is enough
        }
        catch (ObjectDisposedException) when (ct.IsCancellationRequested)
        {
            throw new OperationCanceledException(ct); // closed by the caller's cancel, as a read that honours it throws
        }
        catch (IOException)
        {
            return string.Empty;
        }
        catch (HttpRequestException)
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// The exception for a refused request: <see cref="AccordionQ2SecurityException"/> for 401, and for a 403
    /// that names the role it needs (other than Anonymous), with the server's error after the client's own text;
    /// <see cref="AccordionQ2ThrottledException"/> for a 429 that is a slow-down (with Retry-After or the station's
    /// SecurityError), with its Retry-After; otherwise <see cref="AccordionQ2ApiException"/> with the server's error,
    /// or <paramref name="fallback"/>, or the body.
    /// </summary>
    internal AccordionQ2ApiException CreateError(HttpResponseMessage response, string body, string? fallback = null, bool passwordCheck = false)
    {
        var sent = response.RequestMessage;
        return CreateError((int)response.StatusCode, body, fallback,
            sentKey: sent?.Headers.Contains(AccordionQ2Client.ApiKeyHeader) ?? HasApiKey,
            sentToken: sent?.Headers.Contains("Authorization") ?? _token is not null,
            challenged: response.Headers.WwwAuthenticate.Count > 0,
            retryAfter: RetryAfterOf(response, DateTimeOffset.UtcNow),
            passwordCheck: passwordCheck);
    }

    /// <summary>
    /// As above, from the parts: which credential was sent, whether a 401 carried the middleware's
    /// <c>WWW-Authenticate</c> challenge, the Retry-After. For a call that checks a password (<c>passwordCheck</c>: login,
    /// own password) its own 401, without that challenge, is the password's refusal, not the credential's.
    /// </summary>
    internal AccordionQ2ApiException CreateError(int status, string body, string? fallback = null, bool? sentKey = null, bool? sentToken = null,
        bool challenged = true, TimeSpan? retryAfter = null, bool passwordCheck = false)
    {
        ErrorBody? error = null;
        try { error = JsonConvert.DeserializeObject<ErrorBody>(body); } catch { }
        var serverMessage = Scrub(error?.Error);
        var key = sentKey ?? HasApiKey;
        var token = !key && (sentToken ?? _token is not null); // the station uses the key when both are sent

        if (status == 401 && passwordCheck && !challenged)
            return new AccordionQ2SecurityException(status, AccordionQ2SecurityReason.WrongPassword, error?.Requires,
                serverMessage ?? "The station refused the user name or password");
        if (status == 401)
        {
            var reason = key ? AccordionQ2SecurityReason.InvalidApiKey
                : token ? AccordionQ2SecurityReason.InvalidToken
                : AccordionQ2SecurityReason.MissingApiKey;
            var message = reason switch
            {
                AccordionQ2SecurityReason.InvalidApiKey => "The station doesn't accept this client's API key (X-Api-Key): it is wrong, or was removed on the station",
                AccordionQ2SecurityReason.InvalidToken => "The station doesn't accept this client's login token: it expired, or the login ended (logout, a password change, the user removed); log in again",
                _ => "The station needs an API key (X-Api-Key) and this client has none; set AccordionQ2ClientOptions.ApiKey, or log in with Security.LoginAsync",
            };
            // The server's own reason too: the 401 may come from something else, e.g. a proxy.
            if (serverMessage is not null)
                message += $" (station: {serverMessage})";
            return new AccordionQ2SecurityException(status, reason, error?.Requires, message);
        }
        // An endpoint everyone may call can't refuse for a role: its 403 has another cause (the claim's: not from the
        // station's own network segment), which the station's own message says
        if (status == 403 && !string.IsNullOrEmpty(error?.Requires) && !string.Equals(error!.Requires, "Anonymous", StringComparison.Ordinal))
        {
            var message = $"This needs the {error!.Requires} role, which this client's {(token ? "login" : "API key")} doesn't have";
            if (serverMessage is not null)
                message += $" (station: {serverMessage})";
            return new AccordionQ2SecurityException(status, AccordionQ2SecurityReason.RoleTooLow, error.Requires, message);
        }
        // Not every 429 is a slow-down: the station's slow-downs carry Retry-After and a SecurityError (with requires);
        // another 429, e.g. a stream's subscription limit, is a hard limit that waiting won't lift, so it stays a plain
        // AccordionQ2ApiException with the station's own text
        if (status == 429 && (retryAfter is not null || !string.IsNullOrEmpty(error?.Requires)))
        {
            // Named as the slow-down it is (FW7 SEC-06, CRA-08), with when to try again
            var message = "The station is slowing this client down (HTTP 429: too many failed credentials or requests from this address)"
                + (retryAfter is { } wait ? $"; try again in {Math.Ceiling(wait.TotalSeconds):0} s" : "");
            if (serverMessage is not null)
                message += $" (station: {serverMessage})";
            return new AccordionQ2ThrottledException(status, retryAfter, serverMessage, error?.Requires, message);
        }
        if (status is >= 300 and < 400 && serverMessage is null)
            // Not followed (see AccordionQ2Client.CreateHandler), and the Location isn't repeated.
            return new AccordionQ2ApiException(status,
                $"The server answered with a redirect (HTTP {status}), which the station's API never does; the client doesn't follow it. Check the base URL");
        return new AccordionQ2ApiException(status, serverMessage ?? fallback ?? Scrub(body) ?? string.Empty);
    }

    /// <summary>Text from the server, unless it would repeat the key or the login token.</summary>
    private string? Scrub(string? text)
    {
        if (text is null)
            return null;
        if (_apiKey is not null && text.IndexOf(_apiKey, StringComparison.Ordinal) >= 0)
            return "(withheld: it contained the API key)";
        if (_token is { } token && text.IndexOf(token, StringComparison.Ordinal) >= 0)
            return "(withheld: it contained the login token)";
        return text;
    }

    /// <summary>A token a header can carry; the message never repeats it.</summary>
    internal static string ValidateToken(string token)
    {
        if (token.Length == 0)
            throw new ArgumentException("The login token is empty; use null for none", nameof(token));
        foreach (var c in token)
            if (c < '!' || c > '~')
                throw new ArgumentException("The login token contains characters a header can't carry (only visible ASCII, no spaces)", nameof(token));
        return token;
    }

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
