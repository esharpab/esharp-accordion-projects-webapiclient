# Channels

Channels represent multi-purpose I/O pins (analog, digital, I2C, SPI, etc.).

## Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `GetAllAsync(ct?)` | `Task<List<ChannelDto>>` | Return every configured channel. |
| `GetChannelAsync(alias?, netName?, ct?)` | `Task<ChannelDto>` | Look up one channel by alias or net name. |
| `ConfigureAsync(config, ct?)` | `Task` | Partial-update a single channel. |
| `ConfigureManyAsync(configs, ct?)` | `Task` | Partial-update multiple channels in one round-trip. |

## Examples

### Listing Channels

```csharp
var channels = await client.Channels.GetAllAsync();
foreach (var ch in channels)
    Console.WriteLine($"  {ch.Alias}: type={ch.ChannelType}, unit={ch.Unit}");
```

### Looking Up a Channel

```csharp
// By alias
var ch = await client.Channels.GetChannelAsync(alias: "0.1.ESH10000158.MON_3V3");
Console.WriteLine($"Type: {ch.ChannelType}, Direction: {ch.Direction}");

// By net name
var ch2 = await client.Channels.GetChannelAsync(netName: "MPIO00");
```

### Checking Channel Capabilities

```csharp
using AccordionQ2.WebApiClient.Models;

if (ch.ChannelTypeCapability.HasFlag(ChannelTypes.Analog))
    Console.WriteLine("This channel supports analog mode");

if (ch.Capability.HasFlag(DirectionTypes.IN))
    Console.WriteLine("Input capable");
```

### Configuring a Channel

Channel configuration uses **partial updates** — only non-null properties in `ChannelConfigRequest` are applied; everything else is left unchanged.

```csharp
// Update a single property
await client.Channels.ConfigureAsync(new ChannelConfigRequest
{
    Alias       = "0.1.ESH10000158.MON_3V3",
    Description = "Main 3.3 V rail monitor",
    Unit        = "V",
});

// Batch configure
await client.Channels.ConfigureManyAsync(new List<ChannelConfigRequest>
{
    new() { NetName = "MPIO00", Enabled = true, Direction = DirectionTypes.IN  },
    new() { NetName = "MPIO01", Enabled = true, Direction = DirectionTypes.OUT },
});
```

## Request Model

### `ChannelConfigRequest`

Only non-null properties are applied. Supply `NetName` to locate by net name, `Alias` to locate by alias. If both are supplied, `NetName` is used for lookup and `Alias` is updated to the new value.

| Property | Type | Description |
|----------|------|-------------|
| `NetName` | `string?` | Net name for lookup (takes priority over `Alias`) |
| `Alias` | `string?` | Alias for lookup, or new alias when `NetName` is also set |
| `Enabled` | `bool?` | Enable or disable the channel |
| `Direction` | `DirectionTypes?` | `IN`, `OUT`, or both — must be within the channel's `Capability` flags |
| `ChannelType` | `ChannelTypes?` | Active channel type — must be within the channel's `ChannelTypeCapability` flags |
| `Description` | `string?` | Human-readable description |
| `Unit` | `string?` | Unit of measurement (e.g. `"V"`, `"°C"`, `"A"`) |
| `GroupName` | `string?` | Logical group name |
| `DeviceName` | `string?` | Name of the providing device |

## Response Model

### `ChannelDto`

| Property | Type | Description |
|----------|------|-------------|
| `ChannelIndex` | `int` | Global channel index |
| `Index` | `int` | Device-relative channel index |
| `Enabled` | `bool` | Whether the channel is active |
| `Usage` | `MpioUsageTypes` | Usage classification |
| `DeviceName` | `string` | Name of the providing device |
| `ChannelType` | `ChannelTypes` | Currently active channel type flags |
| `ChannelTypeCapability` | `ChannelTypes` | All supported channel types (flags) |
| `Alias` | `string` | Human-readable alias |
| `NetName` | `string` | Unique net name |
| `GroupName` | `string` | Logical group name |
| `Capability` | `DirectionTypes` | Supported directions (IN, OUT) |
| `Direction` | `DirectionTypes` | Currently configured direction |
| `DefaultDirection` | `DirectionTypes` | Factory-default direction |
| `DirectionChanged` | `bool` | Whether direction differs from the default |
| `Description` | `string` | Human-readable description |
| `Unit` | `string` | Unit of measurement |
| `IsVirtual` | `bool` | Whether this is a virtual (software-only) channel |
