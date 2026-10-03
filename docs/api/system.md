# System

The station's services, reboot, clock, and the hardware app's start-up configuration (`boot.config`).

- Reads are open to everyone. Changes are writes: while another client holds the [lease](lease.md) they are refused with 423.
- Off a station (a WebApi running on a development PC) the services, reboot and clock calls answer **501**.
- The WebApi runs these through `sudo`; a Pi without the needed sudo rights answers 500 with sudo's message.

## Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `GetServicesAsync(ct?)` | `Task<List<ServiceStatusDto>>` | The hardware app, the WebApi and the dashboard, with their state. |
| `ServiceActionAsync(id, action, ct?)` | `Task` | `start`, `stop`, `restart`, `enable` or `disable` a service. |
| `RebootAsync(ct?)` | `Task` | Reboot the Pi a second after answering. |
| `GetClockAsync(ct?)` | `Task<ClockStatusDto>` | The Pi's clock. |
| `SetClockAsync(utc, force = false, ct?)` | `Task<ClockStatusDto>` | Set the Pi's clock. |
| `GetBootAsync(ct?)` | `Task<BootConfigDto>` | The start-up configuration, without the Wi-Fi password. |
| `SetBootAsync(update, ct?)` | `Task<BootConfigDto>` | Edit the start-up configuration. |
| `SetBootStartupAsync(enabled, aliasFiles, ct?)` | `Task<BootConfigDto>` | Change only whether boot.config is applied and which alias files are loaded. |

## Services

The services are `hardware` (the hardware app), `webapi` (the WebApi and the browser GUI) and `dashboard` (Node-RED).

```csharp
foreach (var s in await client.System.GetServicesAsync())
    Console.WriteLine($"{s.Id,-10} {s.Unit,-28} {s.ActiveState}/{s.SubState} boot={s.Enabled} actions={string.Join(",", s.Actions)}");

// Restart the hardware app; the call doesn't wait for systemd
await client.System.ServiceActionAsync("hardware", "restart");
```

- `start`, `stop` and `restart` don't wait for the job to finish; follow `GetServicesAsync` to see it happen. `enable` and `disable` change whether the service starts at boot.
- The WebApi answering (`Self == true`) can be restarted through itself but not stopped. Restarting it drops this client's connection, and the event stream, for a few seconds.
- Errors: 404 for an unknown service, 400 for an action it can't take (not in its `Actions`), 500 with systemd's message.

## Reboot

```csharp
await client.System.RebootAsync();
// The Pi is back after about a minute; expect HttpRequestException until then
```

## Clock

A station without internet has no time server, so the computer talking to it is usually the best clock.

```csharp
var clock = await client.System.GetClockAsync();
Console.WriteLine($"Pi: {clock.Utc:u} ({clock.TimeZone}), off by {(clock.Utc - DateTimeOffset.UtcNow).TotalSeconds:0.0} s");

if (!clock.NtpSynchronized)
    clock = await client.System.SetClockAsync(DateTimeOffset.UtcNow);
```

- A clock a time server keeps (`NtpSynchronized`) is refused with 409 unless `force: true`.
- 400 for a time before 2020.
- `Utc` is the Pi's time when it answered; allow for the round trip when comparing.

## Start-up configuration (boot.config)

`boot.config` on the station says what the hardware app applies when it starts: the services to run, the Wi-Fi, static IP addresses, USB ports, the modules to load and the alias files to load, in order.

- **Changes apply at the hardware app's next start.** Alias files and modules are also applied again on every reset of the engine (`Application.ResetAsync`).
- With `Enabled == false` only `Services` are applied; Wi-Fi, addresses, USB ports, modules and alias files are not.
- The Wi-Fi password is never returned, only `Wifi.PasswordSet`.
- `ProtectedServices` lists the hardware app's and the WebApi's own units. Turning one off is refused with 400: the hardware app stops and disables a service marked off at every start, which would leave SSH as the only way back in.
- A station without the file answers 404 until the hardware app has started once.

### Reading it

```csharp
var boot = await client.System.GetBootAsync();
Console.WriteLine($"Applied at start-up: {boot.Enabled}, last changed {boot.Modified:u}");
foreach (var a in boot.AliasFiles.OrderBy(a => a.Order))
    Console.WriteLine($"  alias {a.Path} enabled={a.Enabled}");
foreach (var m in boot.Modules)
    Console.WriteLine($"  module {m.Name} ({m.ClassName}) enabled={m.Enabled}");
Console.WriteLine($"  Wi-Fi {boot.Wifi.Ssid} enabled={boot.Wifi.Enabled} password set={boot.Wifi.PasswordSet}");
```

### Choosing the alias files loaded at start-up

`SetBootStartupAsync` changes only `Enabled` and the alias files. Pass null for either to leave it as it is. Alias files are loaded in the order given; `Order` is ignored here. Turning `Enabled` on also applies the file's Wi-Fi, addresses, USB ports and modules at the next start.

```csharp
using AccordionQ2.WebApiClient.Models;

await client.System.SetBootStartupAsync(
    enabled: true,
    aliasFiles: new[]
    {
        new BootAliasFileDto { Path = "station.csv" },
        new BootAliasFileDto { Path = "fixture-b.csv", Enabled = false },
    });
```

An alias file must be a plain file name in the alias folder (see [Files](files.md), root `alias`) and listed once; 400 otherwise.

### Editing the file

`SetBootAsync` takes a `BootConfigUpdateDto`. Each section you set replaces the file's; a section left null stays as it is. Set `IfModified` to the `Modified` you read: if someone else changed the file since, the edit is refused with **409** and nothing is written, so read it again instead of overwriting their edit.

```csharp
using AccordionQ2.WebApiClient;
using AccordionQ2.WebApiClient.Models;

var boot = await client.System.GetBootAsync();

try
{
    boot = await client.System.SetBootAsync(new BootConfigUpdateDto
    {
        IfModified       = boot.Modified,
        Description      = "Line 3",
        IpConfigurations = new List<BootAddressDto>
        {
            new() { Interface = "eth0", StaticIp = "192.168.0.222/24", Enabled = true },
        },
        // Password = null keeps the saved password; "" would clear it
        Wifi = new BootWifiUpdateDto { Enabled = true, Ssid = "Lab", Password = null },
    });
}
catch (AccordionQ2ApiException ex) when (ex.StatusCode == 409)
{
    Console.WriteLine("boot.config changed since it was read; read it again");
}
catch (AccordionQ2ApiException ex) when (ex.StatusCode == 400)
{
    Console.WriteLine($"Refused: {ex.Message}");
}
```

Everything is checked before anything is written. 400 for, among others: an address that isn't IPv4 with an optional prefix length, an enabled address without one, Wi-Fi on without an SSID, a Wi-Fi password that isn't 8 to 63 characters, a module without a name or a duplicate one, an enabled module without assembly and class, a duplicate service, and turning off a protected service. Both calls answer with the file as `GetBootAsync` shows it.

A module keeps what the API doesn't show (its image name and initial data) as long as its name stays the same. An empty `Namespace` is taken from `ClassName`.

## Models

### `ServiceStatusDto`

| Property | Type | Description |
|----------|------|-------------|
| `Id` | `string` | `hardware`, `webapi` or `dashboard` |
| `Unit` | `string` | The systemd unit, e.g. `accordion.service` |
| `Description` | `string` | The unit's description |
| `Installed` | `bool` | False when the Pi doesn't have the unit |
| `ActiveState` | `string` | systemd's state: `active`, `inactive`, `failed`, `activating`, `deactivating` |
| `SubState` | `string` | systemd's detail: `running`, `dead`, `exited`, `auto-restart` and so on |
| `Enabled` | `bool` | Starts at boot |
| `Since` | `DateTimeOffset?` | When it last became active |
| `Self` | `bool` | The WebApi answering: can be restarted but not stopped |
| `Actions` | `List<string>` | What it can be asked for: `start`, `stop`, `restart`, `enable`, `disable` |

### `ClockStatusDto`

| Property | Type | Description |
|----------|------|-------------|
| `Utc` | `DateTimeOffset` | The Pi's time when it answered |
| `TimeZone` | `string` | e.g. `Etc/UTC` |
| `NtpEnabled` | `bool` | Time synchronisation is on |
| `NtpSynchronized` | `bool` | A time server keeps the clock; setting it then needs `force` |

### `BootConfigDto`

| Property | Type | Description |
|----------|------|-------------|
| `Enabled` | `bool` | Applied at start-up; when false only `Services` are |
| `Modified` | `DateTime` | When the file last changed; send it back as `IfModified` |
| `Description` | `string?` | Free text |
| `AliasFiles` | `List<BootAliasFileDto>` | Loaded after the engine starts, and on every reset, in order |
| `Modules` | `List<BootModuleDto>` | Modules loaded after the engine starts |
| `IpConfigurations` | `List<BootAddressDto>` | Static addresses given beside DHCP |
| `Wifi` | `BootWifiDto` | Wi-Fi settings, without the password |
| `DisableUsbPorts` | `bool` | Turn the USB ports off |
| `EnableMediaDevices` | `bool` | Enable media devices |
| `Services` | `List<BootServiceDto>` | Services started or stopped at start-up; always applied |
| `ProtectedServices` | `List<string>` | Units that can't be turned off here |

### `BootAliasFileDto`

| Property | Type | Description |
|----------|------|-------------|
| `Path` | `string` | A file name in the alias folder |
| `Enabled` | `bool` | Default `true` |
| `Order` | `int` | Load order |

### `BootModuleDto`

| Property | Type | Description |
|----------|------|-------------|
| `Name` | `string` | Module name; unique |
| `AssemblyPath` | `string` | e.g. `additional/Snowball.dll` |
| `ClassName` | `string` | Full class name |
| `Namespace` | `string` | Empty when sent: taken from `ClassName` |
| `Enabled` | `bool` | Load it at start-up |

### `BootAddressDto`

| Property | Type | Description |
|----------|------|-------------|
| `Interface` | `string` | e.g. `eth0` |
| `StaticIp` | `string` | An IPv4 address, with or without its prefix length, e.g. `192.168.0.222/24` |
| `Enabled` | `bool` | Apply it |

### `BootWifiDto`

| Property | Type | Description |
|----------|------|-------------|
| `Enabled` | `bool` | Wi-Fi on |
| `Ssid` | `string` | Network name |
| `PasswordSet` | `bool` | Whether a password is saved (it is never returned) |

### `BootServiceDto`

| Property | Type | Description |
|----------|------|-------------|
| `Name` | `string` | Display name; unique |
| `ServiceFile` | `string` | The systemd unit name |
| `Enabled` | `bool` | Run it; false stops and disables it at every start |

### `BootConfigUpdateDto`

Every property is optional; null leaves that section as it is.

| Property | Type | Description |
|----------|------|-------------|
| `IfModified` | `DateTime?` | The `Modified` read; 409 if the file changed since |
| `Enabled` | `bool?` | Apply boot.config at start-up |
| `Description` | `string?` | Free text |
| `AliasFiles` | `List<BootAliasFileDto>?` | Replaces the alias files |
| `Modules` | `List<BootModuleDto>?` | Replaces the modules |
| `IpConfigurations` | `List<BootAddressDto>?` | Replaces the static addresses |
| `Wifi` | `BootWifiUpdateDto?` | Replaces the Wi-Fi settings |
| `DisableUsbPorts` | `bool?` | |
| `EnableMediaDevices` | `bool?` | |
| `Services` | `List<BootServiceDto>?` | Replaces the services |

### `BootWifiUpdateDto`

| Property | Type | Description |
|----------|------|-------------|
| `Enabled` | `bool` | Wi-Fi on |
| `Ssid` | `string` | Network name; at most 32 bytes, required when enabled |
| `Password` | `string?` | A new password (8 to 63 characters); null keeps the saved one, `""` clears it |
