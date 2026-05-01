# Error Handling

All API errors throw `AccordionQ2ApiException`, importable from the top-level namespace. The exception carries the HTTP status code and the server's error message.

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
| 404 | Not Found | Channel, resource, or config file does not exist |
| 500 | Internal Server Error | Hardware manager encountered an error |
| — | `HttpRequestException` | WebApi host is unreachable (connection refused, DNS failure, timeout) |

> **Note:** Network-level errors (host unreachable, DNS failure, timeout) are thrown as `HttpRequestException` rather than `AccordionQ2ApiException`. Catch both when robustness is required.

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
