# Models

All models live in `AccordionQ2.WebApiClient.Models`.

```csharp
using AccordionQ2.WebApiClient.Models;
```

---

## Request Models

### `ChannelLookupRequest`

Used internally by `ChannelsGroup.GetChannelAsync`. Supply at least one of `Alias` or `NetName`.

| Property | Type | Description |
|----------|------|-------------|
| `Alias` | `string?` | Alias name; used for lookup when `NetName` is not provided |
| `NetName` | `string?` | Net name; when provided, takes priority over `Alias` |

### `ChannelConfigRequest`

Partial-update request for `ConfigureAsync` / `ConfigureManyAsync`. Only non-null properties are applied.

| Property | Type | Description |
|----------|------|-------------|
| `NetName` | `string?` | Net name for lookup (takes priority over `Alias`) |
| `Alias` | `string?` | Alias for lookup, or new alias value when `NetName` is also set |
| `Enabled` | `bool?` | Enable or disable the channel |
| `Direction` | `DirectionTypes?` | `IN`, `OUT`, or both — must be within the channel's `Capability` flags |
| `ChannelType` | `ChannelTypes?` | Active type — must be within `ChannelTypeCapability` flags |
| `Description` | `string?` | Human-readable description |
| `Unit` | `string?` | Unit of measurement (e.g. `"V"`, `"°C"`, `"A"`) |
| `GroupName` | `string?` | Logical group name |
| `DeviceName` | `string?` | Name of the providing device |

### `NumericMeasureRequest`

Request for `NumericResultsGroup.MeasureAsync`.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `ChannelNetName` | `string` | — | Net name of the NumericResult channel to use |
| `TargetNetName` | `string` | — | Net name of the physical channel to sample |
| `Samples` | `int` | `1000` | Number of samples to acquire |
| `ReducedSet` | `bool` | `true` | When `true`, raw samples are discarded after computing statistics |

### `BusTransactionRequestBase`

Abstract base for all bus transaction requests.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `DeviceName` | `string` | — | Device name as registered in the hardware manager |
| `Action` | `BusActions` | `Send` | Bus action to perform |
| `DataToSend` | `string?` | `null` | Bytes to transmit, hex-encoded uppercase (e.g. `"AABB"`) |
| `NumberOfBytesToReceive` | `int` | `0` | Expected receive byte count |

### `I2cTransactionRequest` : `BusTransactionRequestBase`

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Address` | `string` | `"00"` | I2C 7-bit device address as two-digit hex (e.g. `"50"` for 0x50) |
| `MaxRetries` | `int` | `-1` | Retry limit on NAK; `-1` uses the device default |

### `UartTransactionRequest` : `BusTransactionRequestBase`

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `TimeoutMs` | `int` | `1000` | Receive timeout in milliseconds |

### `SpiTransactionRequest` : `BusTransactionRequestBase`

No additional properties beyond the base class.

### `SocketTransactionRequest` : `BusTransactionRequestBase`

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `HostName` | `string` | `""` | Remote host name or IP address |
| `Port` | `int` | `0` | Remote TCP port number |
| `TerminationByte` | `string` | `"00"` | Message boundary byte as two-digit hex |
| `UseTerminationByte` | `bool` | `false` | Whether to use `TerminationByte` as end-of-message marker |
| `TimeoutMs` | `int` | `1000` | Receive timeout in milliseconds |

---

## Response Models

### `ConnectionStatusDto`

| Property | Type | Description |
|----------|------|-------------|
| `IsConnected` | `bool` | `true` if the API is connected to the hardware manager |
| `LastError` | `string?` | Last connection error message, if any |

### `ChannelDto`

Full channel description returned by `ChannelsGroup.GetAllAsync` and `GetChannelAsync`.

| Property | Type | Description |
|----------|------|-------------|
| `ChannelIndex` | `int` | Global channel index |
| `Index` | `int` | Device-relative channel index |
| `Enabled` | `bool` | Whether the channel is active |
| `Usage` | `MpioUsageTypes` | Usage classification |
| `DeviceName` | `string` | Name of the providing device |
| `ChannelType` | `ChannelTypes` | Currently active channel type (flags) |
| `ChannelTypeCapability` | `ChannelTypes` | All supported channel types (flags) |
| `Alias` | `string` | Human-readable alias |
| `NetName` | `string` | Unique net name |
| `GroupName` | `string` | Logical group name |
| `Capability` | `DirectionTypes` | Supported directions (flags) |
| `Direction` | `DirectionTypes` | Currently configured direction |
| `DefaultDirection` | `DirectionTypes` | Factory-default direction |
| `DirectionChanged` | `bool` | Whether direction differs from the default |
| `Description` | `string` | Human-readable description |
| `Unit` | `string` | Unit of measurement |
| `IsVirtual` | `bool` | Whether this is a virtual (software-only) channel |

### `ModuleSettingsDto`

| Property | Type | Description |
|----------|------|-------------|
| `Name` | `string` | Module name |
| `Enabled` | `bool` | Whether the module is enabled |
| `ClassName` | `string` | Module class name |
| `AssemblyPath` | `string` | Path to the module assembly |
| `Namespace` | `string` | Module namespace |
| `ImageName` | `string` | Docker/firmware image name |
| `InitialData` | `Dictionary<string, object?>` | Arbitrary key/value initialisation data (values may be strings, numbers, booleans, or nested JSON objects) |

### `PhysicalSystemDto`

| Property | Type | Description |
|----------|------|-------------|
| `Host` | `string` | Host name |
| `EthIpV4` | `string` | Primary IPv4 address |
| `EthIpV6` | `string` | Primary IPv6 address |
| `Firmware` | `string` | Firmware version string |
| `MAC` | `string` | MAC address |
| `Modules` | `List<PhysicalModuleDto>` | Physical hardware module slots |
| `NetworkInterfaces` | `Dictionary<string, string>` | Network interface name → address mapping |

### `PhysicalModuleDto`

| Property | Type | Description |
|----------|------|-------------|
| `Index` | `int` | Slot index |
| `Name` | `string` | Module name |
| `ProductID` | `string` | Product identifier |
| `Revision` | `byte` | Hardware revision |
| `SerialNumber` | `string` | Serial number |

### `AppLicenseDto`

| Property | Type | Description |
|----------|------|-------------|
| `Name` | `string` | Application name |
| `Key` | `string` | License key |
| `Expires` | `DateTime` | License expiration date/time |
| `Type` | `AppTypes` | Application type (`SoftwareModule` or `HardwareModule`) |

### `BusTransactionResponse`

| Property | Type | Description |
|----------|------|-------------|
| `DeviceName` | `string` | Device used for the transaction |
| `Action` | `string` | Action that was performed |
| `Received` | `string` | Received bytes, hex-encoded uppercase (e.g. `"AABBCCDD"`) |
| `NumberOfBytesReceived` | `int` | Number of bytes received |

### `NumericResultChannelDto`

| Property | Type | Description |
|----------|------|-------------|
| `NetName` | `string` | Net name of the NumericResult channel |
| `Alias` | `string` | Human-readable alias |
| `PossibleTargetNames` | `string[]` | Physical channels this channel can sample |
| `SampleRate` | `int` | Hardware sampling rate in Hz |
| `DefaultSamples` | `int` | Default number of samples configured on the channel |

### `NumericMeasureResultDto`

| Property | Type | Description |
|----------|------|-------------|
| `ChannelNetName` | `string` | NumericResult channel used |
| `TargetNetName` | `string` | Physical channel sampled |
| `SampleCount` | `int` | Number of raw samples retained (`0` when `ReducedSet` is `true`) |
| `SampleRate` | `int` | Actual sampling rate in Hz |
| `ReducedSet` | `bool` | Whether raw samples were discarded after statistics were computed |
| `Started` | `DateTime` | Acquisition start timestamp |
| `Stopped` | `DateTime` | Acquisition stop timestamp |
| `Duration` | `TimeSpan` | Total acquisition duration |
