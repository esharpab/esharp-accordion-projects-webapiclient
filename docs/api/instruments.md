# Instruments

Instruments are power supplies, meters and other instruments a module exposes. Each one is an Instrument channel with a **function map**: the net name of the channel behind each of its capabilities, for example `OUTPUT_VOLTAGE` → `0.4.ESH10000662.VSET1`.

`Channels.GetAllAsync()` returns only the base fields of each channel, so it doesn't give you the map; this group does. You read and write the instrument's controls through [Resources](resources.md), by the net names in the map.

> Needs firmware with `GET /api/instruments`. Older firmware answers 404 (`AccordionQ2ApiException` with `StatusCode == 404`).

## Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `GetAllAsync(ct?)` | `Task<List<InstrumentDto>>` | Every Instrument channel, in channel-list order, with its type and function map. |

The server answers from its channel cache, so this costs no extra call to the hardware app.

## Examples

### Listing Instruments

```csharp
var instruments = await client.Instruments.GetAllAsync();
foreach (var inst in instruments)
{
    Console.WriteLine($"{inst.InstrumentName} {inst.GroupName} ({inst.Type})");
    foreach (var (capability, netName) in inst.FunctionMap)
        Console.WriteLine($"  {capability} -> {netName}");
}
```

### Driving a Power Supply

Every key in the function map is optional; treat a missing key as a missing control.

```csharp
// Set a power-supply output to 12 V with a 100 mA limit, then turn it on
var supply = (await client.Instruments.GetAllAsync()).First(i => i.Type == "PowerSupply");

await client.Resources.SetValuesAsync(new Dictionary<string, string>
{
    [supply.FunctionMap["OUTPUT_VOLTAGE"]]      = "12",
    [supply.FunctionMap["OUTPUT_CURRENTLIMIT"]] = "100",
});
await client.Resources.SetValueAsync(supply.FunctionMap["OUTPUT_ENABLE"], "True");

// Read back the output, if the supply can measure it
if (supply.FunctionMap.TryGetValue("READ_VOUT", out var vout))
    Console.WriteLine($"Output: {await client.Resources.GetValueAsync(vout)} V");
```

## Response Model

### `InstrumentDto`

| Property | Type | Description |
|----------|------|-------------|
| `NetName` | `string` | Net name of the Instrument channel; unique |
| `Alias` | `string` | Alias of the Instrument channel |
| `GroupName` | `string` | Group, e.g. `"CH1"` for one output of a two-output supply |
| `Description` | `string` | Description of the instrument |
| `InstrumentName` | `string` | Instrument name; several outputs of one module can share it |
| `Type` | `string` | Instrument type (EsharpDefinitions `InstrumentTypes`), e.g. `"PowerSupply"`. Taken from the channel, not from a value read |
| `FunctionMap` | `Dictionary<string, string>` | Capability name (EsharpDefinitions `Capabilities`) to the net name of the channel that implements it |

An Instrument channel's `Details` in [Channels](channels.md) carries the same name, type and function map (`InstrumentName`, `InstrumentType`, `FunctionMap`).
