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
