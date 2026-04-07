# AccordionQ2.WebApiClient

A .NET HTTP client library for the **AccordionQ2 Hardware Management REST API**.  
Provides a strongly-typed, async-first interface for reading/writing resource values, configuring channels, managing modules, and controlling application lifecycle over HTTP.

## Installation

```shell
dotnet add package AccordionQ2.WebApiClient
```

## Requirements

- .NET Standard 2.0 compatible runtime (.NET 5+, .NET Framework 4.6.1+)
- An AccordionQ2 WebApi host reachable over HTTP (e.g. `http://raspberrypi:5000`)

---

## Quick Start

```csharp
using AccordionQ2.WebApiClient;
using AccordionQ2.WebApiClient.Models;

using var client = new AccordionQ2Client("http://raspberrypi:5000");

// Check hardware manager connection
var status = await client.Connection.GetStatusAsync();

// Read a resource value
string voltage = await client.Resources.GetValueAsync("Voltage.VDD");

// Configure a channel
await client.Channels.ConfigureAsync(new ChannelConfigRequest
{
    NetName   = "MPIO00",
    Enabled   = true,
    Direction = DirectionTypes.IN
});
```

---

## API Reference

`AccordionQ2Client` exposes six operation groups:

### `client.Resources` — Hardware resource values

Resources are identified by name (e.g. `"Voltage.VDD"`, `"Temperature.Ambient"`).

| Method | Description |
|---|---|
| `GetNamesAsync()` | Returns the names of all available resources |
| `GetValueAsync(name)` | Reads the current value of a single resource |
| `SetValueAsync(name, value)` | Writes a value to a single resource |
| `GetValuesAsync(names[])` | Batch read — returns `Dictionary<string, string>` |
| `SetValuesAsync(dict)` | Batch write |
| `TransactAsync(name, value)` | Write-then-read transaction (command/response pattern, e.g. EEPROM, register access) |

```csharp
// Batch read
var values = await client.Resources.GetValuesAsync(new[] { "Voltage.VDD", "Temperature.Ambient" });

// Write-then-read transaction
string response = await client.Resources.TransactAsync("Eeprom.Read", "0x0010");
```

---

### `client.Channels` — Channel configuration

| Method | Description |
|---|---|
| `GetAllAsync()` | Returns all configured channels as `List<ChannelDto>` |
| `GetChannelAsync(alias, netName)` | Looks up a single channel by alias or net name |
| `ConfigureAsync(request)` | Partial update for a single channel |
| `ConfigureManyAsync(requests)` | Partial update for multiple channels in one round-trip |

Channel configuration uses partial updates — only non-null properties in `ChannelConfigRequest` are applied; everything else is left unchanged.

```csharp
// Set a single property without touching anything else
await client.Channels.ConfigureAsync(new ChannelConfigRequest
{
    NetName   = "MPIO00",
    Direction = DirectionTypes.OUT,
    Value     = "2.5"
});

// Batch configure
await client.Channels.ConfigureManyAsync(new List<ChannelConfigRequest>
{
    new() { NetName = "MPIO00", Enabled = true, Direction = DirectionTypes.IN },
    new() { NetName = "MPIO01", Enabled = true, Direction = DirectionTypes.OUT }
});
```

**`ChannelConfigRequest` properties:**

| Property | Type | Description |
|---|---|---|
| `NetName` | `string?` | Net name for lookup (takes priority over `Alias`) |
| `Alias` | `string?` | Alias for lookup, or new alias value when `NetName` is also set |
| `Enabled` | `bool?` | Enable or disable the channel |
| `Direction` | `DirectionTypes?` | `IN` or `OUT` — must be within the channel's capability flags |
| `ChannelType` | `ChannelTypes?` | Active channel type — must be within the channel's type capability flags |
| `Description` | `string?` | Human-readable description |
| `Unit` | `string?` | Unit of measurement (e.g. `"V"`, `"°C"`, `"A"`) |
| `GroupName` | `string?` | Logical group name |
| `DeviceName` | `string?` | Name of the providing device |

---

### `client.Modules` — Module management

| Method | Description |
|---|---|
| `GetAllAsync()` | All modules (loaded and unloaded) |
| `GetLoadedAsync()` | Currently loaded modules only |
| `LoadAsync(module)` | Load a module |
| `UnloadAsync(module)` | Unload a module |
| `ConfigureAsync(module)` | Configure a module |
| `GetPhysicalSystemAsync()` | Hardware topology description |
| `GetLicensedAppsAsync()` | Licensed applications only |
| `GetAllAppsAsync()` | All applications (licensed and unlicensed) |

---

### `client.Application` — Application lifecycle & configuration files

| Method | Description |
|---|---|
| `GetNameAsync()` | Application module name |
| `GetIdentificationAsync()` | Application identification string |
| `GetStatusAsync()` | Current `ModuleStatus` (`OK`, `Warning`, `Error`, …) |
| `ResetAsync()` | Send a reset command to the application engine |
| `ListConfigFilesAsync()` | List all configuration files on the device |
| `GetLoadedConfigFilesAsync()` | Names of currently loaded configuration files |
| `LoadConfigFileAsync(fileName)` | Load a configuration file by name |
| `SaveConfigFileAsync(fileName)` | Save current configuration to a named file |
| `DownloadConfigFileAsync(fileName)` | Download a configuration file as `byte[]` |
| `UploadConfigFileAsync(fileName, data)` | Upload a configuration file |
| `DeleteConfigFileAsync(fileName)` | Delete a configuration file |

```csharp
// Download config, modify, re-upload
byte[] data = await client.Application.DownloadConfigFileAsync("default.cfg");
// ... modify data ...
await client.Application.UploadConfigFileAsync("default.cfg", data);
await client.Application.LoadConfigFileAsync("default.cfg");
```

---

### `client.Media` — Media files

| Method | Description |
|---|---|
| `ListFilesAsync()` | List all media files on the device |
| `DownloadFileAsync(fileName)` | Download a file as `byte[]` |
| `UploadFileAsync(fileName, data)` | Upload a file |
| `DeleteFileAsync(fileName)` | Delete a file |

---

### `client.Connection` — Connection status

| Method | Description |
|---|---|
| `GetStatusAsync()` | Returns `ConnectionStatusDto` with the hardware manager connection state |

---

## Error Handling

All methods throw `AccordionQ2ApiException` on non-success HTTP responses. The exception exposes the HTTP status code alongside the error message returned by the API.

```csharp
try
{
    await client.Channels.ConfigureAsync(request);
}
catch (AccordionQ2ApiException ex) when (ex.StatusCode == 404)
{
    Console.WriteLine($"Channel not found: {ex.Message}");
}
catch (AccordionQ2ApiException ex)
{
    Console.WriteLine($"API error {ex.StatusCode}: {ex.Message}");
}
```

---

## Dependency Injection (`IHttpClientFactory`)

For long-running applications, pass an externally managed `HttpClient` to avoid socket exhaustion:

```csharp
// Program.cs
builder.Services.AddHttpClient("accordion", c =>
    c.BaseAddress = new Uri("http://raspberrypi:5000"));

builder.Services.AddSingleton<IAccordionService, AccordionService>();

// AccordionService.cs
public class AccordionService
{
    private readonly AccordionQ2Client _client;

    public AccordionService(IHttpClientFactory factory)
    {
        var http = factory.CreateClient("accordion");
        _client = new AccordionQ2Client("http://raspberrypi:5000", http);
    }
}
```

When using `IHttpClientFactory`, the caller owns the `HttpClient` lifetime — `AccordionQ2Client.Dispose()` will not dispose it.

---

## License

Copyright © 2026 E-Sharp AB. See [LICENSE](LICENSE) for details.
