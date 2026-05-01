# Comm &mdash; Raw Bus Transactions

Perform raw bus transactions over I2C, UART, SPI, and TCP sockets. All byte data is **hex-encoded** on the wire (e.g. `"AABB"` for bytes `0xAA 0xBB`).

## Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `I2cAsync(request, ct?)` | `Task<BusTransactionResponse>` | I2C bus transaction. |
| `UartAsync(request, ct?)` | `Task<BusTransactionResponse>` | UART transaction. |
| `SpiAsync(request, ct?)` | `Task<BusTransactionResponse>` | SPI transaction. |
| `SocketAsync(request, ct?)` | `Task<BusTransactionResponse>` | TCP socket transaction. |

All methods return a `BusTransactionResponse`:

| Property | Type | Description |
|----------|------|-------------|
| `DeviceName` | `string` | Device used for the transaction |
| `Action` | `string` | Action performed |
| `Received` | `string` | Received bytes, hex-encoded (e.g. `"AABBCCDD"`) |
| `NumberOfBytesReceived` | `int` | Number of bytes received |

---

## I2C

Use `I2cTransactionRequest`:

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `DeviceName` | `string` | — | Device name as registered in the hardware manager |
| `Address` | `string` | `"00"` | I2C 7-bit device address as two-digit hex (e.g. `"50"` for 0x50) |
| `Action` | `BusActions` | `Send` | `Send`, `Receive`, `SendReceive`, or `Scan` |
| `DataToSend` | `string?` | `null` | Bytes to transmit, hex-encoded (required for Send/SendReceive) |
| `NumberOfBytesToReceive` | `int` | `0` | Expected receive count (for Receive/SendReceive) |
| `MaxRetries` | `int` | `-1` | Retry limit on NAK (`-1` = device default) |

### Examples

```csharp
using AccordionQ2.WebApiClient.Models;

// Scan the bus for connected devices
var resp = await client.Comm.I2cAsync(new I2cTransactionRequest
{
    DeviceName = "0.ESH10000597.I2C00",
    Address    = "00",
    Action     = BusActions.Scan,
});
Console.WriteLine($"Scan result: {resp.Received}");

// Write two bytes to address 0x50
await client.Comm.I2cAsync(new I2cTransactionRequest
{
    DeviceName = "0.ESH10000597.I2C00",
    Address    = "50",
    Action     = BusActions.Send,
    DataToSend = "0010",
});

// Read 4 bytes from address 0x50
var read = await client.Comm.I2cAsync(new I2cTransactionRequest
{
    DeviceName              = "0.ESH10000597.I2C00",
    Address                 = "50",
    Action                  = BusActions.Receive,
    NumberOfBytesToReceive  = 4,
});
Console.WriteLine($"Read: {read.Received}"); // e.g. "AABBCCDD"

// Write then read (SendReceive)
var xfer = await client.Comm.I2cAsync(new I2cTransactionRequest
{
    DeviceName             = "0.ESH10000597.I2C00",
    Address                = "50",
    Action                 = BusActions.SendReceive,
    DataToSend             = "00",
    NumberOfBytesToReceive = 2,
});
```

---

## UART

Use `UartTransactionRequest`:

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `DeviceName` | `string` | — | Device name as registered in the hardware manager |
| `Action` | `BusActions` | `Send` | `Send`, `Receive`, `SendReceive`, or `ClearBuffers` |
| `DataToSend` | `string?` | `null` | Bytes to transmit, hex-encoded |
| `NumberOfBytesToReceive` | `int` | `0` | Expected receive count |
| `TimeoutMs` | `int` | `1000` | Receive timeout in milliseconds |

### Example

```csharp
// Send a SCPI query and read the response
// "*IDN?\n" in hex is "2A49444E3F0A"
var resp = await client.Comm.UartAsync(new UartTransactionRequest
{
    DeviceName             = "MyUartDevice",
    Action                 = BusActions.SendReceive,
    DataToSend             = "2A49444E3F0A",
    NumberOfBytesToReceive = 64,
    TimeoutMs              = 2000,
});
// Convert hex response back to ASCII
var text = System.Text.Encoding.ASCII.GetString(
    Convert.FromHexString(resp.Received));
Console.WriteLine(text);
```

---

## SPI

Use `SpiTransactionRequest`:

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `DeviceName` | `string` | — | Device name as registered in the hardware manager |
| `Action` | `BusActions` | `Send` | `Send`, `Receive`, or `SendReceive` |
| `DataToSend` | `string?` | `null` | Bytes to clock out, hex-encoded |
| `NumberOfBytesToReceive` | `int` | `0` | Expected receive count |

### Example

```csharp
// Full-duplex SPI transfer
var resp = await client.Comm.SpiAsync(new SpiTransactionRequest
{
    DeviceName             = "MySpiDevice",
    Action                 = BusActions.SendReceive,
    DataToSend             = "AABB",
    NumberOfBytesToReceive = 2,
});
Console.WriteLine($"SPI response: {resp.Received}");
```

---

## Socket (TCP/IP)

Use `SocketTransactionRequest`:

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `DeviceName` | `string` | — | Device name as registered in the hardware manager |
| `Action` | `BusActions` | `Send` | `Send`, `Receive`, or `SendReceive` |
| `HostName` | `string` | `""` | Remote host name or IP address |
| `Port` | `int` | `0` | Remote TCP port number |
| `DataToSend` | `string?` | `null` | Bytes to send, hex-encoded |
| `NumberOfBytesToReceive` | `int` | `0` | Expected receive count |
| `TerminationByte` | `string` | `"00"` | Byte used as a message boundary, as two-digit hex |
| `UseTerminationByte` | `bool` | `false` | Whether to use `TerminationByte` as end-of-message marker |
| `TimeoutMs` | `int` | `1000` | Receive timeout in milliseconds |

### Example

```csharp
// Send a SCPI query over TCP
// "*IDN?\n" in hex is "2A49444E3F0A"
var resp = await client.Comm.SocketAsync(new SocketTransactionRequest
{
    DeviceName             = "MySocketDevice",
    Action                 = BusActions.SendReceive,
    HostName               = "192.168.1.10",
    Port                   = 5025,
    DataToSend             = "2A49444E3F0A",
    NumberOfBytesToReceive = 64,
});
var text = System.Text.Encoding.ASCII.GetString(
    Convert.FromHexString(resp.Received));
Console.WriteLine(text);
```

---

## Hex Encoding Note

All `DataToSend` strings and the `Received` field use **uppercase hex without separators**, e.g. `"AABB0C"`. You can use `Convert.FromHexString` / `Convert.ToHexString` (.NET 5+) or a manual loop on older runtimes to convert between `byte[]` and hex strings.
