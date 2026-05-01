# Quick Start

## Creating a Client

The main entry point is `AccordionQ2Client`. Pass the base URL of the AccordionQ2 WebApi:

```csharp
using AccordionQ2.WebApiClient;

var client = new AccordionQ2Client("http://agent64.local:5000");
```

### Constructor Overloads

```csharp
// Let the library manage its own HttpClient
var client = new AccordionQ2Client("http://agent64.local:5000");

// Bring your own HttpClient (e.g. from IHttpClientFactory)
var client = new AccordionQ2Client("http://agent64.local:5000", httpClient);
```

| Parameter | Type | Description |
|-----------|------|-------------|
| `baseUrl` | `string` | Base URL of the AccordionQ2 WebApi, e.g. `"http://raspberrypi:5000"` |
| `httpClient` | `HttpClient?` | Pre-configured HTTP client. Pass `null` to create one internally. |

### Disposing the Client

`AccordionQ2Client` implements `IDisposable`. When managing its own `HttpClient` it will dispose it on `Dispose()`. Use a `using` statement for automatic cleanup:

```csharp
using var client = new AccordionQ2Client("http://agent64.local:5000");
// client and its HttpClient are disposed when the using block exits
```

When you supply an external `HttpClient`, the client does **not** dispose it — the caller is responsible for the `HttpClient` lifetime:

```csharp
// ASP.NET Core — inject a typed or named client via IHttpClientFactory
public class MyService(IHttpClientFactory factory)
{
    public async Task DoWorkAsync()
    {
        var http = factory.CreateClient("accordion");
        var client = new AccordionQ2Client("http://agent64.local:5000", http);
        // Do not dispose http here; IHttpClientFactory manages it
    }
}
```

## Cancellation Tokens

Every method accepts an optional `CancellationToken` as its last parameter:

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
string value = await client.Resources.GetValueAsync("Voltage.VDD", cts.Token);
```

## Checking the Connection

```csharp
var status = await client.Connection.GetStatusAsync();
if (status.IsConnected)
    Console.WriteLine("Connected to hardware manager");
else
    Console.WriteLine($"Not connected: {status.LastError}");
```

## Reading and Writing Values

Resources are identified by dotted name strings (e.g. `"TempRegulator.CPU_TEMP"`):

```csharp
// Read a single value
string temp = await client.Resources.GetValueAsync("TempRegulator.CPU_TEMP");
Console.WriteLine($"CPU temperature: {temp}");

// Read multiple values at once
var values = await client.Resources.GetValuesAsync(new[]
{
    "TempRegulator.CPU_TEMP",
    "Engine.Uptime",
});
foreach (var (name, val) in values)
    Console.WriteLine($"{name} = {val}");

// Write a value
await client.Resources.SetValueAsync("MyOutput", "2.5");
```

## Working with Channels

```csharp
// List all channels
var channels = await client.Channels.GetAllAsync();
foreach (var ch in channels)
    Console.WriteLine($"  {ch.Alias}: type={ch.ChannelType}, unit={ch.Unit}");

// Look up a specific channel
var ch = await client.Channels.GetChannelAsync(alias: "0.1.ESH10000158.MON_3V3");
Console.WriteLine($"Type: {ch.ChannelType}, Direction: {ch.Direction}");
```

## Configuring a Channel

Use partial updates — only the non-null properties in `ChannelConfigRequest` are applied:

```csharp
using AccordionQ2.WebApiClient.Models;

await client.Channels.ConfigureAsync(new ChannelConfigRequest
{
    Alias       = "0.1.ESH10000158.MON_3V3",
    Description = "Main 3.3 V rail monitor",
    Unit        = "V",
});
```

## Handling Errors

All API errors throw `AccordionQ2ApiException`:

```csharp
using AccordionQ2.WebApiClient;

try
{
    var ch = await client.Channels.GetChannelAsync(alias: "does.not.exist");
}
catch (AccordionQ2ApiException ex)
{
    Console.WriteLine($"HTTP {ex.StatusCode}: {ex.Message}");
}
```

## Next Steps

Explore the full [API Reference](../api/overview.md) for detailed documentation of all 8 operation groups.
