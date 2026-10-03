# AccordionQ2.WebApiClient

A .NET HTTP client library for the **AccordionQ2 Hardware Management REST API**.  
Provides a strongly-typed, async-first interface for reading/writing resource values, configuring channels, managing modules, and controlling application lifecycle over HTTP, as well as the event stream, value subscriptions, the control lease, the station's services and start-up configuration, its files, and signed firmware updates.

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

## Documentation

Comprehensive API documentation is available in the [`docs/`](https://github.com/esharpab/esharp-accordion-projects-webapiclient/blob/main/docs/index.md) folder:

- [Getting Started / Installation](https://github.com/esharpab/esharp-accordion-projects-webapiclient/blob/main/docs/getting-started/installation.md)
- [Quick Start](https://github.com/esharpab/esharp-accordion-projects-webapiclient/blob/main/docs/getting-started/quickstart.md)
- [API Overview](https://github.com/esharpab/esharp-accordion-projects-webapiclient/blob/main/docs/api/overview.md) — all 18 operation groups at a glance
- Group pages: [Resources](https://github.com/esharpab/esharp-accordion-projects-webapiclient/blob/main/docs/api/resources.md), [Channels](https://github.com/esharpab/esharp-accordion-projects-webapiclient/blob/main/docs/api/channels.md), [Modules](https://github.com/esharpab/esharp-accordion-projects-webapiclient/blob/main/docs/api/modules.md), [Application](https://github.com/esharpab/esharp-accordion-projects-webapiclient/blob/main/docs/api/application.md), [Media](https://github.com/esharpab/esharp-accordion-projects-webapiclient/blob/main/docs/api/media.md), [Connection](https://github.com/esharpab/esharp-accordion-projects-webapiclient/blob/main/docs/api/connection.md), [Comm](https://github.com/esharpab/esharp-accordion-projects-webapiclient/blob/main/docs/api/comm.md), [Numeric Results](https://github.com/esharpab/esharp-accordion-projects-webapiclient/blob/main/docs/api/numeric-results.md), [Calibration](https://github.com/esharpab/esharp-accordion-projects-webapiclient/blob/main/docs/api/calibration.md), [Audit](https://github.com/esharpab/esharp-accordion-projects-webapiclient/blob/main/docs/api/audit.md), [Instruments](https://github.com/esharpab/esharp-accordion-projects-webapiclient/blob/main/docs/api/instruments.md), [Capabilities](https://github.com/esharpab/esharp-accordion-projects-webapiclient/blob/main/docs/api/capabilities.md), [Events and Subscriptions](https://github.com/esharpab/esharp-accordion-projects-webapiclient/blob/main/docs/api/events.md), [Lease](https://github.com/esharpab/esharp-accordion-projects-webapiclient/blob/main/docs/api/lease.md), [System](https://github.com/esharpab/esharp-accordion-projects-webapiclient/blob/main/docs/api/system.md), [Files](https://github.com/esharpab/esharp-accordion-projects-webapiclient/blob/main/docs/api/files.md), [Firmware](https://github.com/esharpab/esharp-accordion-projects-webapiclient/blob/main/docs/api/firmware.md)
- [Error Handling](https://github.com/esharpab/esharp-accordion-projects-webapiclient/blob/main/docs/error-handling.md)
- [Models Reference](https://github.com/esharpab/esharp-accordion-projects-webapiclient/blob/main/docs/reference/models.md)
- [Enums Reference](https://github.com/esharpab/esharp-accordion-projects-webapiclient/blob/main/docs/reference/enums.md)

---

## API Reference

`AccordionQ2Client` exposes eighteen operation groups: `Resources`, `Channels`, `Modules`, `Application`, `Media`, `Connection`, `Comm`, `NumericResults`, `Calibration`, `Audit`, `Instruments`, `Capabilities`, `Events`, `Subscriptions`, `Lease`, `System`, `Files` and `Firmware`. The groups from `Instruments` on need a WebApi that has them; older firmware answers 404 (`Capabilities.GetAsync` returns `CapabilitiesDto.None` instead).

### `client.Resources` — Hardware resource values

Resources are identified by name (e.g. `"Voltage.VDD"`, `"Temperature.Ambient"`).  
Both **NetName** (e.g. `"0.23.ESH10000517.READ_TEMPERATURE"`) and **Alias** (e.g. `"FRONT_AIR READ TEMPERATURE"`) are accepted interchangeably by all methods.

| Method | Description |
|---|---|
| `GetNamesAsync()` | Returns the names of all available resources |
| `GetValueAsync(name)` | Reads the current value of a single resource |
| `SetValueAsync(name, value)` | Writes a value to a single resource |
| `GetValuesAsync(names[])` | Batch read — returns `Dictionary<string, string>` |
| `ReadValuesAsync(names[], maxAgeMs)` | Batch read accepting cached values up to `maxAgeMs` old; returns `ResourceValuesDto` with each value's age |
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
| `GetEncodedAsync(netName)` | Every channel, or one, in the byte protocol's binary encoding (`EncodedChannelsDto`) |
| `ConfigureEncodedAsync(codecVersion, data)` | Configure whole channels sent in that encoding; returns how many were configured |

Channel configuration uses partial updates — only non-null properties in `ChannelConfigRequest` are applied; everything else is left unchanged.

```csharp
// Set a single property without touching anything else
await client.Channels.ConfigureAsync(new ChannelConfigRequest
{
    NetName   = "MPIO00",
    Direction = DirectionTypes.OUT
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
| `Details` | `ChannelDetailsDto?` | Type-specific fields to change, e.g. `new ChannelDetailsDto { Gain = 2 }` |

Each `ChannelDto` from `Channels.GetAllAsync()` has `Details` with the fields of its concrete type
(null from firmware whose WebApi predates it). For example a multiplexer's choices:

```csharp
var wave = (await client.Channels.GetAllAsync()).First(c => c.NetName == "0.2.ESH10000560.GEN1_WAVE");
Console.WriteLine(string.Join(", ", wave.Details?.DestinationNets ?? [])); // SINE, SQUARE, TRIANGLE, NOISE
```

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

### `client.Comm` — Raw bus transactions (I2C, UART, SPI, Socket)

All byte data is **hex-encoded** (uppercase, no separator) on the wire. `DataToSend` and `Received` are plain hex strings (e.g. `"AABB"` = two bytes `0xAA 0xBB`). The I2C `Address` and the Socket `TerminationByte` are two-digit hex strings (e.g. `"50"` for 0x50).

| Method | Description |
|---|---|
| `I2cAsync(request)` | Send, Receive, SendReceive, or Scan on an I2C bus |
| `UartAsync(request)` | Send, Receive, SendReceive, or ClearBuffers on a UART port |
| `SpiAsync(request)` | Send, Receive, or SendReceive on a SPI bus |
| `SocketAsync(request)` | Send, Receive, or SendReceive over a TCP socket |

```csharp
// I2C: scan the bus for connected devices
var scan = await client.Comm.I2cAsync(new I2cTransactionRequest
{
    DeviceName = "0.ESH10000597.I2C00",
    Address    = "00",
    Action     = BusActions.Scan,
});
foreach (var addr in Convert.FromHexString(scan.Received))
    Console.WriteLine($"Found device at 0x{addr:X2}");

// I2C: write two bytes to address 0x50
var write = await client.Comm.I2cAsync(new I2cTransactionRequest
{
    DeviceName = "0.ESH10000597.I2C00",
    Address    = "50",
    Action     = BusActions.Send,
    DataToSend = "0010",   // register 0x00, value 0x10
});

// I2C: read 4 bytes from address 0x50
var read = await client.Comm.I2cAsync(new I2cTransactionRequest
{
    DeviceName             = "0.ESH10000597.I2C00",
    Address                = "50",
    Action                 = BusActions.Receive,
    NumberOfBytesToReceive = 4,
});
byte[] bytes = Convert.FromHexString(read.Received);

// UART: send a SCPI query and read the response
var uart = await client.Comm.UartAsync(new UartTransactionRequest
{
    DeviceName             = "MyUartDevice",
    Action                 = BusActions.SendReceive,
    DataToSend             = Convert.ToHexString(System.Text.Encoding.ASCII.GetBytes("*IDN?\n")),
    NumberOfBytesToReceive = 64,
    TimeoutMs              = 2000,
});
```

---

### `client.NumericResults` — Fast numeric sampling

| Method | Description |
|---|---|
| `AcquireAsync(channel, target, samples, lsl, usl)` | Acquire and return every sample with its statistics in one call (`NumericAcquisitionDto`) |
| `GetChannelsAsync()` / `GetTargetsAsync(channel)` | NumericResult channels and what each can sample |
| `MeasureAsync(request)` | Acquire, keeping the result on the server |
| `GetMeanAsync` / `GetMinAsync` / `GetMaxAsync` / `GetStdDevAsync` / `GetSamplesAsync` | Read the kept result |

---

### `client.Audit` — WebApi audit log

| Method | Description |
|---|---|
| `GetAuditLogAsync(tail = 100)` | The last `tail` lines of the request audit log; `0` for all |

---

### `client.Calibration` — Calibration channel read/write

Calibration channels carry a `CalibrationTable` encoded as a Base64 binary payload. The server
transparently decodes/encodes that payload, so you work with plain objects.
Both **NetName** and **Alias** are accepted for the channel name.

| Method | Description |
|---|---|
| `GetChannelsAsync()` | Returns all Calibration channels as `List<CalibrationChannelDto>` |
| `GetTableAsync(channelName)` | Reads and decodes the CalibrationTable from a channel |
| `SetTableAsync(channelName, table)` | Encodes and writes a CalibrationTable to a channel |

```csharp
// Read calibration data
var table = await client.Calibration.GetTableAsync("0.8.ESH10000590.CAL0");
foreach (var row in table.CalData)
    Console.WriteLine($"{row.Key}: gain={row.Gain:F6}, offset={row.Offset:F6}");

// Modify a row and write back
var updated = new CalibrationTableDto
{
    ProductId    = table.ProductId,
    Revision     = table.Revision,
    SerialNumber = table.SerialNumber,
    CalData      = table.CalData.Select(r =>
        r.Key == "ADC0" ? new CalibrationRowDto { Key = r.Key, Gain = 1.0012, Offset = 0.001 } : r
    ).ToList()
};
await client.Calibration.SetTableAsync("0.8.ESH10000590.CAL0", updated);
```

---

### `client.Instruments` — Instrument channels

Instruments (power supplies, meters and others) are Instrument channels whose **function map**
names the channel behind each capability, for example `OUTPUT_VOLTAGE` → `0.4.ESH10000662.VSET1`.
`Channels.GetAllAsync()` doesn't include the map; this does. Every key is optional.
Needs firmware with `GET /api/instruments`; older firmware answers 404.

| Method | Description |
|---|---|
| `GetAllAsync()` | Returns every Instrument channel as `List<InstrumentDto>`: type, instrument name and function map |

```csharp
// Set a power-supply output to 12 V with a 100 mA limit, then turn it on
var supply = (await client.Instruments.GetAllAsync()).First(i => i.Type == "PowerSupply");
await client.Resources.SetValuesAsync(new Dictionary<string, string>
{
    [supply.FunctionMap["OUTPUT_VOLTAGE"]] = "12",
    [supply.FunctionMap["OUTPUT_CURRENTLIMIT"]] = "100",
});
await client.Resources.SetValueAsync(supply.FunctionMap["OUTPUT_ENABLE"], "True");
```

---

### `client.Capabilities` — What the WebApi supports

| Method | Description |
|---|---|
| `GetAsync()` | `CapabilitiesDto`: API, WebApi and codec versions and `Features`; `CapabilitiesDto.None` from a WebApi that predates it |

```csharp
var caps = await client.Capabilities.GetAsync();
if (caps.Has(CapabilitiesDto.Events)) { /* the event stream is available */ }
```

---

### `client.Events` and `client.Subscriptions` — Event stream and pushed values

`Events.ListenAsync(onEvent, ct)` reads the server-sent event stream (`hello`, `connection`,
`configuration`, `values`, `lease`) until cancelled; it throws `TimeoutException` or `IOException`
when the stream dies, and reconnecting is the caller's. `Subscriptions` asks for channel values to
arrive as `values` events on that stream.

| Method | Description |
|---|---|
| `Events.ListenAsync(onEvent, ct)` | Calls `onEvent` for each `ServerEventDto`, in order |
| `Subscriptions.CreateAsync(streamId, channels, intervalMs)` | Subscribe the stream (id from its `hello`) to channels, every 100 to 60 000 ms |
| `Subscriptions.UpdateAsync(id, channels, intervalMs)` | Replace and renew; a subscription not renewed within `ExpiresInMs` ends |
| `Subscriptions.DeleteAsync(id)` | End it |

See [Events and Subscriptions](https://github.com/esharpab/esharp-accordion-projects-webapiclient/blob/main/docs/api/events.md) for a reconnecting listener.

---

### `client.Lease` — Control lease

One client at a time may change the station. While another client holds the lease, changes and forced
reads get `AccordionQ2ApiException` with 423. The holder sends its lease id with every request automatically.

| Method | Description |
|---|---|
| `GetAsync()` | Who holds the lease (`LeaseStateDto`) |
| `AcquireAsync(owner, ttlMs = 30000)` | Take it; 409 when someone else holds it |
| `RenewAsync(ttlMs)` | Renew it before it runs out |
| `ReleaseAsync()` | Release it |

---

### `client.System` — Services, reboot, clock and boot.config

| Method | Description |
|---|---|
| `GetServicesAsync()` | The hardware app, the WebApi and the dashboard, with their systemd state |
| `ServiceActionAsync(id, action)` | `start`, `stop`, `restart`, `enable` or `disable` a service |
| `RebootAsync()` | Reboot the Pi; it is back after about a minute |
| `GetClockAsync()` / `SetClockAsync(utc, force)` | Read or set the Pi's clock |
| `GetBootAsync()` | The hardware app's start-up configuration (boot.config), without the Wi-Fi password |
| `SetBootAsync(BootConfigUpdateDto)` | Edit boot.config: each section set replaces the file's; `IfModified` gives 409 instead of overwriting another edit |
| `SetBootStartupAsync(enabled, aliasFiles)` | Change only whether boot.config is applied and which alias files load at start-up |

boot.config changes apply at the hardware app's next start (alias files and modules also on reset).

---

### `client.Files` — Files in the station's folders

| Method | Description |
|---|---|
| `GetRootsAsync()` | The folders reachable: `config`, `alias`, `fsms`, `media`, `extensions`, `logs`, `webapi-logs` |
| `ListAsync(root, path)` | A folder's contents |
| `DownloadAsync(root, path)` / `UploadAsync(root, path, data, overwrite)` | Read or write a file |
| `CreateFolderAsync`, `MoveAsync`, `DeleteAsync` | Manage files and folders |

---

### `client.Firmware` — Firmware updates

The station installs only releases signed by E-Sharp, and nothing below 6.0.0. Installing restarts the
hardware app and the WebApi, so poll `GetUpdateAsync` and expect connection errors for a minute or two.

| Method | Description |
|---|---|
| `GetStateAsync()` | Installed version, release source, minimum version, last update |
| `GetReleasesAsync(includeBeta)` | Releases, newest first, with `Installable` and `Downloaded` |
| `SetSourceAsync(location)` | Release source: an http(s) address, a folder on the Pi, or null for the default |
| `StartUpdateAsync(version, includeBeta)` | Start installing; returns at once |
| `GetUpdateAsync()` / `GetUpdateLogAsync()` | Follow the update |
| `UploadPackageAsync(zip)` / `DeletePackageAsync(fileName)` | Add a package from this computer (for a station without internet) or remove one |

See [Firmware](https://github.com/esharpab/esharp-accordion-projects-webapiclient/blob/main/docs/api/firmware.md) for a complete update with polling.

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
