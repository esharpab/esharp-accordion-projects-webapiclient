# Enumerations

All enums live in `AccordionQ2.WebApiClient.Models`.

```csharp
using AccordionQ2.WebApiClient.Models;
```

---

## `ModuleStatus`

Application module status. Mirrors `EsharpDefinitions.CommonTypes.ModuleStatus`.

| Value | Description |
|-------|-------------|
| `Unknown` | Status is unknown |
| `OK` | Module is operating normally |
| `Warning` | Module has warnings |
| `Error` | Module has errors |
| `Disabled` | Module is disabled |

```csharp
var status = await client.Application.GetStatusAsync();
if (status == ModuleStatus.OK)
    Console.WriteLine("Running normally");
```

---

## `AppTypes`

Application/module type classification. Mirrors `EsharpDefinitions.Types.ApplicationTypes.AppTypes`.

| Value | Description |
|-------|-------------|
| `Unknown` | Type is unknown |
| `SoftwareModule` | Software-only module |
| `HardwareModule` | Physical hardware module |

---

## `BusActions`

Bus transaction action types. Mirrors `EsharpDefinitions.Types.BusTransactionTypes.BusActions`.

| Value | Int | Description |
|-------|-----|-------------|
| `Undefined` | `-1` | Not defined |
| `Send` | `0` | Transmit data |
| `Receive` | `1` | Receive data |
| `SendReceive` | `2` | Transmit then receive |
| `Scan` | `3` | Scan for devices (I2C) |
| `Break` | `4` | Send a break condition |
| `ClearBuffers` | `5` | Clear receive/transmit buffers (UART) |
| `Reconfigure` | `6` | Reconfigure the bus |

---

## `UartBusTypes`

UART electrical standard. Mirrors `EsharpDefinitions.Types.BusTransactionTypes.UartBusTypes`.

| Value | Description |
|-------|-------------|
| `Undefined` | Not defined |
| `RS232` | Standard RS-232 full-duplex serial |
| `RS422` | Differential RS-422 serial |
| `RS485` | Multi-drop RS-485 serial |

---

## `FlowControlTypes`

UART flow control mode. Mirrors `EsharpDefinitions.Types.BusTransactionTypes.FlowControlTypes`.

| Value | Description |
|-------|-------------|
| `Undefined` | Not defined |
| `None` | No flow control |
| `XON_XOFF` | Software (XON/XOFF) flow control |
| `RTS_CTS` | Hardware RTS/CTS flow control |
| `RTS_CTS_AND_XON_XOFF` | Hardware RTS/CTS combined with XON/XOFF |
| `DTR_DSR` | Hardware DTR/DSR flow control |
| `DTR_DSR_AND_XON_XOFF` | Hardware DTR/DSR combined with XON/XOFF |

---

## `ParityTypes`

Serial port parity. Mirrors `EsharpDefinitions.Types.InstrumentTypes.ParityTypes`.

| Value | Description |
|-------|-------------|
| `Undefined` | Not defined / unknown |
| `None` | No parity |
| `Even` | Even parity |
| `Odd` | Odd parity |
| `Mark` | Mark parity |
| `Space` | Space parity |

---

## `DirectionTypes`

I/O direction flags for channels. Decorated with `[Flags]`. Mirrors `EsharpDefinitions.Types.BusTransactionTypes.DirectionTypes`.

| Value | Int | Description |
|-------|-----|-------------|
| `Undefined` | `0` | Not defined |
| `IN` | `1` | Input capable |
| `OUT` | `2` | Output capable |

Supports bitwise operations:

```csharp
// Check if a channel is input-capable
if (ch.Capability.HasFlag(DirectionTypes.IN))
    Console.WriteLine("Input capable");

// Bidirectional
if (ch.Capability == (DirectionTypes.IN | DirectionTypes.OUT))
    Console.WriteLine("Bidirectional");
```

---

## `MpioUsageTypes`

Multi-purpose I/O usage classification. Mirrors `EsharpDefinitions.Types.BusTransactionTypes.MpioUsageTypes`.

| Value | Int | Description |
|-------|-----|-------------|
| `Undefined` | `-1` | Not defined |
| `HiddenSystemControl` | `0` | Hidden system control pin (not user-visible) |
| `ReadOnlySystemControl` | `1` | Read-only system control pin |
| `UserAllocatable` | `2` | Available for user allocation |
| `BusSignal` | `3` | Bus signal (I2C/SPI/UART etc.) |

---

## `ChannelTypes`

Hardware channel type flags. Decorated with `[Flags]`. Mirrors `EsharpDefinitions.Types.BusTransactionTypes.ChannelTypes`.

| Value | Bit (hex) | Description |
|-------|-----------|-------------|
| `Undefined` | `0x000000` | Not defined |
| `Analog` | `0x000001` | Analog channel |
| `Digital` | `0x000002` | Digital channel |
| `VirtualDigital` | `0x000004` | Virtual digital channel |
| `Temperature` | `0x000008` | Temperature sensor |
| `Multiplexer` | `0x000010` | Multiplexer channel |
| `Resistance` | `0x000020` | Resistance measurement |
| `Counter` | `0x000040` | Counter channel |
| `Frequency` | `0x000080` | Frequency measurement |
| `Actuator` | `0x000100` | Actuator control |
| `Register` | `0x000400` | Register access |
| `Current` | `0x000800` | Current measurement |
| `Ratiometric` | `0x001000` | Ratiometric measurement |
| `UART` | `0x002000` | UART communication |
| `SPI` | `0x004000` | SPI communication |
| `I2C` | `0x008000` | I2C communication |
| `ByteStream` | `0x010000` | Byte stream |
| `Socket` | `0x020000` | TCP socket |
| `Waveform` | `0x040000` | Waveform channel |
| `NumericResult` | `0x080000` | Numeric result (single) |
| `PseudoDigital` | `0x100000` | Pseudo-digital channel |
| `Image` | `0x200000` | Image capture |
| `Audio` | `0x400000` | Audio channel |
| `Video` | `0x800000` | Video channel |
| `Instrument` | `0x1000000` | Instrument channel |
| `NumericResults` | `0x2000000` | Numeric results group |
| `Calibration` | `0x4000000` | Calibration channel |

Supports bitwise operations:

```csharp
// Check if a channel supports analog mode
if (ch.ChannelTypeCapability.HasFlag(ChannelTypes.Analog))
    Console.WriteLine("Analog capable");

// Set channel to analog input
await client.Channels.ConfigureAsync(new ChannelConfigRequest
{
    NetName     = "MPIO00",
    ChannelType = ChannelTypes.Analog,
    Direction   = DirectionTypes.IN,
});
```
