# Calibration

Calibration channels carry a `CalibrationTable` encoded as a Base64 binary payload on the wire.
The server transparently decodes and encodes that payload, so you always work with plain JSON objects.

Both **NetName** and **Alias** are accepted wherever a channel name is required.

## Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `GetChannelsAsync(ct?)` | `Task<List<CalibrationChannelDto>>` | All Calibration channels. |
| `GetTableAsync(channelName, ct?)` | `Task<CalibrationTableDto>` | Read and decode a CalibrationTable from a channel. |
| `SetTableAsync(channelName, table, ct?)` | `Task` | Encode and write a CalibrationTable to a channel. |

## Typical Workflow

```csharp
using AccordionQ2.WebApiClient.Models;

// 1. Discover available Calibration channels
var channels = await client.Calibration.GetChannelsAsync();
foreach (var ch in channels)
    Console.WriteLine($"{ch.NetName}  ({ch.Alias})");

// 2. Read the current table (by NetName or Alias)
var table = await client.Calibration.GetTableAsync(channels[0].NetName);
Console.WriteLine($"Product: {table.ProductId}  Rev: {table.Revision}  S/N: {table.SerialNumber}");
foreach (var row in table.CalData)
    Console.WriteLine($"  {row.Key,-40} gain={row.Gain:F6}  offset={row.Offset:F6}");

// 3. Modify a row and write back
var newRows = table.CalData
    .Select(r => r.Key == "ADC0"
        ? new CalibrationRowDto { Key = r.Key, Gain = 1.0012, Offset = 0.001 }
        : r)
    .ToList();

var updated = new CalibrationTableDto
{
    ProductId    = table.ProductId,
    Revision     = table.Revision,
    SerialNumber = table.SerialNumber,
    CalData      = newRows
};

await client.Calibration.SetTableAsync(channels[0].NetName, updated);
```

## Alias Support

The channel can be addressed by either its NetName or its friendly Alias:

```csharp
// These two calls are equivalent
var t1 = await client.Calibration.GetTableAsync("0.8.ESH10000590.CAL0");
var t2 = await client.Calibration.GetTableAsync("FRONT AIR CALIBRATION");
```

## Models

### `CalibrationChannelDto`

| Property | Type | Description |
|----------|------|-------------|
| `NetName` | `string` | Hardware net name of the channel |
| `Alias` | `string` | Human-readable alias |

### `CalibrationTableDto`

| Property | Type | Description |
|----------|------|-------------|
| `ProductId` | `string` | Product identifier |
| `Revision` | `string` | Hardware revision |
| `SerialNumber` | `string` | Unit serial number |
| `CalData` | `List<CalibrationRowDto>` | Calibration rows |

### `CalibrationRowDto`

| Property | Type | Description |
|----------|------|-------------|
| `Key` | `string` | Row identifier (e.g. channel name or composite field) |
| `Gain` | `double` | Gain correction factor |
| `Offset` | `double` | Offset correction value |
