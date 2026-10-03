# Resources

Resources represent readable/writable hardware values such as voltages, temperatures, and firmware revisions. They are identified by a dotted name string (e.g. `"TempRegulator.CPU_TEMP"`).

> **Alias support:** Both the **NetName** (e.g. `"0.23.ESH10000517.READ_TEMPERATURE"`) and the channel **Alias** (e.g. `"FRONT_AIR READ TEMPERATURE"`) are accepted interchangeably by all read, write, and transact methods.

## Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `GetNamesAsync(ct?)` | `Task<string[]>` | List all available resource names. |
| `GetValueAsync(name, ct?)` | `Task<string>` | Read the current value of a single resource. |
| `SetValueAsync(name, value, ct?)` | `Task` | Write a value to a single resource. |
| `GetValuesAsync(names, ct?)` | `Task<Dictionary<string, string>>` | Read multiple resources in one round-trip. |
| `ReadValuesAsync(names, maxAgeMs, ct?)` | `Task<ResourceValuesDto>` | Read multiple resources, accepting values read at most `maxAgeMs` ago; says how old each value is. |
| `SetValuesAsync(resources, ct?)` | `Task` | Write multiple resources in one round-trip. |
| `TransactAsync(name, value, ct?)` | `Task<string>` | Write then read (command/response pattern). |

## Examples

### Listing Available Resources

```csharp
string[] names = await client.Resources.GetNamesAsync();
foreach (var name in names)
    Console.WriteLine(name);
```

### Single Read/Write

```csharp
// Read a single value
string voltage = await client.Resources.GetValueAsync("0.1.ESH10000158.MON_3V3");
Console.WriteLine($"Voltage: {voltage} V");

// Write a value
await client.Resources.SetValueAsync("MyOutput", "2.5");
```

### Batch Operations

```csharp
// Batch read
var values = await client.Resources.GetValuesAsync(new[]
{
    "TempRegulator.CPU_TEMP",
    "Engine.Uptime",
});
foreach (var (name, val) in values)
    Console.WriteLine($"{name} = {val}");

// Batch write
await client.Resources.SetValuesAsync(new Dictionary<string, string>
{
    ["Output1"] = "1.0",
    ["Output2"] = "2.0",
});
```

### Reads with a Maximum Age

The WebApi keeps the last value of each channel, fed by its own reads and writes and by every read and write other clients make. `ReadValuesAsync` returns a cached value no older than `maxAgeMs` as it is and reads the rest from the hardware in one call. Several GUIs or scripts polling the same channels then cost about one read.

```csharp
var r = await client.Resources.ReadValuesAsync(new[]
{
    "0.4.ESH10000662.VMON1",
    "Engine.Uptime",
}, maxAgeMs: 1000);

foreach (var (name, value) in r.Resources)
    Console.WriteLine($"{name} = {value} ({(r.AgeMs.TryGetValue(name, out var age) ? age : 0):0} ms old)");
```

- `maxAgeMs: 0` reads every value from the hardware, exactly as `GetValuesAsync` does.
- `AgeMs` is 0 for a value that was just read. A WebApi without the cache reads every value and leaves `AgeMs` empty.
- A failed hardware read fails the whole call (500).
- Values whose read uses data up (bus receives, byte streams, numeric results) are never served from the cache.
- While another client holds the [lease](lease.md), `GetValueAsync` and `GetValuesAsync` are refused with 423, but `ReadValuesAsync` with `maxAgeMs > 0` keeps working from the cache.

To have values pushed instead of polling, use [subscriptions](events.md#subscriptions).

### Write-then-Read Transaction

Useful for command/response patterns such as EEPROM or register access:

```csharp
string response = await client.Resources.TransactAsync("Eeprom.Read", "0x0010");
Console.WriteLine($"Register value: {response}");
```

## Models

### `ResourceValuesDto`

| Property | Type | Description |
|----------|------|-------------|
| `Resources` | `Dictionary<string, string>` | Each requested name and its value |
| `AgeMs` | `Dictionary<string, double>` | How old each value is in milliseconds; 0 when just read. Empty from a WebApi without the cache |
