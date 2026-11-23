# Error Handling

All API errors throw `AccordionQ2ApiException`, importable from the top-level namespace. The exception carries the HTTP status code and the server's error message. A refusal for want of an API key, login or role is its subclass `AccordionQ2SecurityException` ([below](#security-errors-401-and-403)), and a slow-down (429) is `AccordionQ2ThrottledException` ([below](#slowed-down-429)).

```csharp
using AccordionQ2.WebApiClient;
```

## Basic Usage

```csharp
try
{
    var ch = await client.Channels.GetChannelAsync(alias: "does.not.exist");
}
catch (AccordionQ2ApiException ex)
{
    Console.WriteLine($"HTTP {ex.StatusCode}: {ex.Message}");
}
```

## Exception Properties

| Property | Type | Description |
|----------|------|-------------|
| `StatusCode` | `int` | HTTP status code returned by the API |
| `Message` | `string` | Error message from the server (or raw response body) |

## Common Error Codes

| HTTP Status | Meaning | Typical Cause |
|-------------|---------|---------------|
| 400 | Bad Request | Invalid parameters, or `GetSamplesAsync()` called after `ReducedSet = true` |
| 401 | Unauthorized | The station is in security mode `On` and the client sent no API key or login token, or one it doesn't accept; or a login's wrong password: `AccordionQ2SecurityException` |
| 403 | Forbidden | The API key's role is too low (`AccordionQ2SecurityException`); or a change in a read-only folder ([Files](api/files.md)) or a claim from outside the station's own network segment, a plain `AccordionQ2ApiException` |
| 404 | Not Found | Channel, resource, or config file does not exist; or a newer endpoint on older firmware |
| 409 | Conflict | Someone else holds the [lease](api/lease.md); a codec version mismatch; a file that exists; boot.config changed since it was read; an update already running |
| 423 | Locked | Another client holds the [lease](api/lease.md): changes and forced reads are refused |
| 429 | Too Many Requests | Security mode `On` slows this client's address down: too many failed keys, tokens, passwords or setup codes, or too many requests without a key: `AccordionQ2ThrottledException`. Also a stream's subscription limit (`Subscriptions.CreateAsync`), a plain `AccordionQ2ApiException` |
| 500 | Internal Server Error | Hardware manager encountered an error |
| 501 | Not Implemented | System and firmware calls on a WebApi that isn't running on a station |
| 503 | Service Unavailable | The WebApi isn't connected to the hardware app |
| — | `HttpRequestException` | WebApi host is unreachable (connection refused, DNS failure, timeout); or, over HTTPS, its certificate isn't the pinned one or isn't trusted (inner `AuthenticationException`) |

> **Note:** Network-level errors (host unreachable, DNS failure, timeout, a refused certificate) are thrown as `HttpRequestException` rather than `AccordionQ2ApiException`. Catch both when robustness is required.

## Security Errors (401 and 403)

A station in security mode `On` refuses a request without a valid API key (401) or from a key whose role is too low (403). The client throws `AccordionQ2SecurityException`, a subclass of `AccordionQ2ApiException`, for both, on the [event stream](api/events.md) too. Its message names the cause, followed by the server's own text as `(station: ...)` when it sent one, and never contains the key. The server's text matters when a 401 doesn't come from the station, e.g. from a proxy in front of it. See [Security](security.md) for keys and roles.

| Property | Type | Description |
|----------|------|-------------|
| `Reason` | `AccordionQ2SecurityReason` | `MissingApiKey` (401, the client has no key), `InvalidApiKey` (401, the station doesn't know the key: wrong, or removed), `InvalidToken` (401, the login token expired or its login ended: log in again), `WrongPassword` (401 from `Security.LoginAsync` or `ChangeOwnPasswordAsync`: the name or password is wrong) or `RoleTooLow` (403) |
| `RequiredRole` | `string?` | The role the request needs: `Viewer`, `Operator` or `Admin`, as the station said |
| `StatusCode` | `int` | 401 or 403 |

```csharp
try
{
    await client.System.RebootAsync();
}
catch (AccordionQ2SecurityException ex) when (ex.Reason == AccordionQ2SecurityReason.RoleTooLow)
{
    Console.WriteLine($"This key can't do that; it needs {ex.RequiredRole}");
}
catch (AccordionQ2SecurityException ex)
{
    Console.WriteLine($"Check the API key: {ex.Message}");
}
```

A 403 that doesn't name a role, such as a change in a read-only folder, stays a plain `AccordionQ2ApiException`, and so does one whose `requires` is `Anonymous`: an endpoint everyone may call doesn't refuse for a role. The claim's 403 (`A station is claimed from its own network segment only`) is one.

## Slowed down (429)

A station in security mode `On` (firmware 7.1.0) slows an address down after failed keys, login tokens, passwords or setup codes, and limits requests without a key to its anonymous endpoints. It answers 429 with `Retry-After` **before** it acts, so nothing was changed. The client throws `AccordionQ2ThrottledException`, a subclass of `AccordionQ2ApiException`; its message says it is a slow-down and when to try again, with the station's text as `(station: ...)`.

| Property | Type | Description |
|----------|------|-------------|
| `RetryAfter` | `TimeSpan?` | From `Retry-After` (seconds or an HTTP date); null without one |
| `StationMessage` | `string?` | The station's `error`, e.g. `Too many failed keys from 192.168.0.58; try again in 2 s` |
| `RequiredRole` | `string?` | The station's `requires` |
| `StatusCode` | `int` | 429 |

To let the client wait by itself, set `AccordionQ2ClientOptions.MaxThrottleWait`: a 429 whose `Retry-After` is at most that long is waited out and the request sent again, at most 3 times; a longer wait, or a fourth 429, throws. A stream upload (`Channels.UploadByteStreamAsync`) is never resent, as its stream may be read already. The default, zero, never waits, not even for a `Retry-After` of 0 or a date already past.

```csharp
using var client = new AccordionQ2Client("https://station-12:5001", new AccordionQ2ClientOptions
{
    ApiKey          = key,
    MaxThrottleWait = TimeSpan.FromSeconds(10),
});

try
{
    await client.Resources.GetValueAsync("Voltage.VDD");
}
catch (AccordionQ2ThrottledException ex)
{
    Console.WriteLine($"Slowed down; try again in {ex.RetryAfter?.TotalSeconds ?? 60} s");
}
```

Only a 429 that carries `Retry-After` or the station's `SecurityError` (with `requires`) is a slow-down. The subscription limit per stream (`Subscriptions.CreateAsync`: `A stream can have at most N subscriptions`) is a 429 with neither: a hard limit that waiting doesn't lift. It is thrown as a plain `AccordionQ2ApiException` with the station's text as the message, and never resent.

A blocked address stays blocked while it keeps failing: waiting helps a burst of requests, not a wrong key. The [event stream](api/events.md) doesn't reconnect by itself; a reconnect loop should wait `RetryAfter` before it opens again.

## Pattern: Retry on Transient Errors

```csharp
async Task<string> ReadWithRetryAsync(AccordionQ2Client client, string name,
    int retries = 3, CancellationToken ct = default)
{
    for (int attempt = 0; attempt < retries; attempt++)
    {
        try
        {
            return await client.Resources.GetValueAsync(name, ct);
        }
        catch (AccordionQ2ApiException ex) when (ex.StatusCode >= 500 && attempt < retries - 1)
        {
            await Task.Delay(1000, ct);
        }
    }
    // Final attempt — let exceptions propagate
    return await client.Resources.GetValueAsync(name, ct);
}
```

## Pattern: Graceful Connection Check

```csharp
async Task<bool> IsAvailableAsync(AccordionQ2Client client)
{
    try
    {
        var status = await client.Connection.GetStatusAsync();
        return status.IsConnected;
    }
    catch (HttpRequestException)
    {
        return false; // host unreachable
    }
    catch (AccordionQ2ApiException)
    {
        return false; // API returned an error
    }
}
```

## ASP.NET Core Integration

When using `IHttpClientFactory`, configure resilience policies on the `HttpClient` rather than wrapping individual calls:

```csharp
// Program.cs (.NET 8+)
builder.Services.AddHttpClient("accordion", c =>
    c.BaseAddress = new Uri("http://agent64.local:5000"))
    .AddStandardResilienceHandler(); // Microsoft.Extensions.Http.Resilience

builder.Services.AddScoped(sp =>
{
    var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient("accordion");
    return new AccordionQ2Client("http://agent64.local:5000", http);
});
```
