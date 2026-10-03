# Channels

Channels represent multi-purpose I/O pins (analog, digital, I2C, SPI, etc.).

## Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `GetAllAsync(ct?)` | `Task<List<ChannelDto>>` | Return every configured channel. |
| `GetChannelAsync(alias?, netName?, ct?)` | `Task<ChannelDto>` | Look up one channel by alias or net name. |
| `ConfigureAsync(config, ct?)` | `Task` | Partial-update a single channel. |
| `ConfigureManyAsync(configs, ct?)` | `Task` | Partial-update multiple channels in one round-trip. |
| `GetEncodedAsync(netName?, ct?)` | `Task<EncodedChannelsDto>` | Every channel, or one by net name, in the byte protocol's binary encoding. |
| `ConfigureEncodedAsync(codecVersion, data, ct?)` | `Task<int>` | Configure whole channels sent in that encoding; returns how many were configured. |

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

### Type-Specific Fields (`Details`)

Each `ChannelDto` from `GetAllAsync` and `GetChannelAsync` has `Details`: the fields of its concrete type that the base fields leave out, such as an analog channel's gain and offset, a digital pin's push/pull type or a multiplexer's choices. Only the fields that belong to the channel's type are set; the rest are null. `Details` itself is null for types without extra fields (Socket, ByteStream, Calibration and the media types), and from firmware that predates it.

```csharp
var wave = await client.Channels.GetChannelAsync(netName: "0.2.ESH10000560.GEN1_WAVE");
Console.WriteLine(string.Join(", ", wave.Details?.DestinationNets ?? new string[0])); // SINE, SQUARE, TRIANGLE, ...

// Set a multiplexer by writing one of its destination nets as the value
await client.Resources.SetValueAsync(wave.NetName, "SQUARE");
```

Enum-like fields are strings (`"PushPull"`, `"Differential"`), so a value added in the hardware app doesn't break older clients. `DefaultValue` is a string in the resource-value format (`"True"`, `"1.25"`), whatever the type.

To change type-specific fields, set `Details` on a `ChannelConfigRequest` with only the fields to change. Only the configurable fields of the channel's type are accepted (for example `Gain`, `Offset`, `InputConfiguration`, `DefaultValue` and `Resolution` on an analog channel); anything else is refused with 400 naming the field.

```csharp
await client.Channels.ConfigureAsync(new ChannelConfigRequest
{
    NetName = "0.1.ESH10000158.MON_3V3",
    Details = new ChannelDetailsDto { Gain = 2.0, Offset = -0.01 },
});
```

See `ChannelDetailsDto` in the client for the full list of fields, grouped by channel type.

### Encoded Channels

`GetEncodedAsync` and `ConfigureEncodedAsync` move channels in the binary encoding the byte protocol uses, as base64url text. They carry the full concrete channel objects, so type changes and every type-specific setting work, which the partial updates above can't do. Decoding the data needs EsharpDefinitions, which this package doesn't include:

```csharp
// With EsharpDefinitions referenced:
// List<IMultiPurposeChannel> channels = SerializableHelpers.CreateFromBase64<TelemetryConfiguration>(encoded.Data).Channels;
// string data = new TelemetryConfiguration(channels, TelemetryConfigurationTypes.Changed).AsBase64();
```

Check that the WebApi has the feature and the same codec version as yours first (see [Capabilities](capabilities.md)):

```csharp
var caps = await client.Capabilities.GetAsync();
if (caps.Has(CapabilitiesDto.ChannelsEncoded))
{
    EncodedChannelsDto all = await client.Channels.GetEncodedAsync();
    EncodedChannelsDto one = await client.Channels.GetEncodedAsync("0.1.ESH10000158.VOUT");
    Console.WriteLine($"codec v{all.CodecVersion}, generation {all.Generation}, {all.Data.Length} chars");

    // ... decode one.Data, change the channel, encode it again as newData ...
    string newData = one.Data;
    int configured = await client.Channels.ConfigureEncodedAsync(caps.CodecVersion, newData);
}
```

- `GetEncodedAsync(netName)` matches net names only, not aliases; 404 for an unknown one.
- `Generation` is the hardware-app session the channels came from (see [Events](events.md)).
- `ConfigureEncodedAsync` throws `AccordionQ2ApiException` with 409 when `codecVersion` isn't the server's, 400 when `data` doesn't decode, and 500 with the hardware app's message.
- Don't update local state from its result: reload channels on the `configuration` event instead.

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
| `Details` | `ChannelDetailsDto?` | Type-specific fields to change; set only those to change |

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
| `Details` | `ChannelDetailsDto?` | Type-specific fields; null for types without any and from older firmware |

### `EncodedChannelsDto`

| Property | Type | Description |
|----------|------|-------------|
| `CodecVersion` | `int` | The server's codec version |
| `Generation` | `long` | The hardware-app session the channels came from |
| `Data` | `string` | A `TelemetryConfiguration` in base64url (EsharpDefinitions `SerializableHelpers.AsBase64`) |
