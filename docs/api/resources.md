# Resources

Resources represent readable/writable hardware values such as voltages, temperatures, and firmware revisions. They are identified by a dotted name string (e.g. `"TempRegulator.CPU_TEMP"`).

## Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `GetNamesAsync(ct?)` | `Task<string[]>` | List all available resource names. |
| `GetValueAsync(name, ct?)` | `Task<string>` | Read the current value of a single resource. |
| `SetValueAsync(name, value, ct?)` | `Task` | Write a value to a single resource. |
| `GetValuesAsync(names, ct?)` | `Task<Dictionary<string, string>>` | Read multiple resources in one round-trip. |
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

### Write-then-Read Transaction

Useful for command/response patterns such as EEPROM or register access:

```csharp
string response = await client.Resources.TransactAsync("Eeprom.Read", "0x0010");
Console.WriteLine($"Register value: {response}");
```
