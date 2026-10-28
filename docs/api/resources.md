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
| `ReadCachedValuesAsync(names, maxAgeMs?, ct?)` | `Task<ResourceValuesDto>` | Read multiple resources from the WebApi's cache alone, never from the hardware; lists the names it had no value for. |
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

### Cache-only Reads

`ReadCachedValuesAsync` answers from the WebApi's value cache alone: the station never reads the hardware for it, and the [lease](lease.md) never refuses it. Use it where a read must not disturb anything, for example a remote reader looking at a station while a test runs: reading a channel whose `readEffect` is `Disturbing` (most of them) can switch a mux or reset a peak buffer.

```csharp
var r = await client.Resources.ReadCachedValuesAsync(new[]
{
    "0.4.ESH10000662.VMON1",
    "0.4.ESH10000662.PEAK1",
    "Ports.UART.ttyAMA0",
}, maxAgeMs: 5000);

foreach (var (name, value) in r.Resources)
    Console.WriteLine($"{name} = {value} ({r.AgeMs[name]:0} ms old)");
foreach (var name in r.NotCached!)
    Console.WriteLine($"{name}: not cached");
```

- A name whose cached value is at most `maxAgeMs` old is in `Resources` and `AgeMs`. Any other name is in `NotCached` and in neither of the others. `maxAgeMs: 0` (the default) or less takes a cached value of any age; it goes on the wire as `int.MaxValue` ms (24.8 days), so an older WebApi that ignored `cacheOnly` would answer from its cache rather than read every name.
- Channels whose reads use data up (bus receives, byte streams, numeric results) are always in `NotCached`.
- `NotCached` is empty when every name was cached. It is null from `ReadValuesAsync` and `GetValuesAsync`.
- A name not in `GET /api/channels` fails the call with 400 and nothing is read. That includes hidden system lines, which `ReadValuesAsync` can read.
- The call checks `GET /api/capabilities` for `CapabilitiesDto.ValuesCacheOnly` (firmware 7.0.5 or later) first. An older WebApi would ignore `cacheOnly` and read the hardware, so without the feature the call throws `NotSupportedException` and asks for no values. It also throws when an answer has no `notCached`. The check is kept for `ResourcesGroup.CapabilityCheckLifetime` (60 s) and made again after any failed call, so a station whose update rolled back to older firmware is caught before it is asked for values.

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
| `NotCached` | `string[]?` | From `ReadCachedValuesAsync`: the names the cache had no value for, in the order asked (empty when every name was cached). Null from the other reads |
