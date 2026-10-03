# Connection

Check whether the API is connected to the hardware manager.

## Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `GetStatusAsync(ct?)` | `Task<ConnectionStatusDto>` | Check the hardware manager connection state. |

## Example

```csharp
var status = await client.Connection.GetStatusAsync();
if (status.IsConnected)
    Console.WriteLine("Connected to hardware manager");
else
    Console.WriteLine($"Not connected: {status.LastError}");
```

## Response Model

### `ConnectionStatusDto`

| Property | Type | Description |
|----------|------|-------------|
| `IsConnected` | `bool` | `true` if the API is connected to the hardware manager |
| `LastError` | `string?` | Last connection error message, if any |
| `Generation` | `long` | Goes up each time the WebApi connects or reconnects to the hardware app; `0` from a WebApi that predates it. Reload channels when it changes |

To be told about connection changes instead of polling, listen to the [event stream](events.md).
