# API Overview

`AccordionQ2Client` exposes **eighteen operation groups**, each covering one area of the hardware API. All methods are **asynchronous** and throw `AccordionQ2ApiException` on HTTP errors.

```csharp
using AccordionQ2.WebApiClient;

using var client = new AccordionQ2Client("http://agent64.local:5000");

client.Connection      // Connection status
client.Resources       // Hardware resource values (read/write)
client.Channels        // Channel configuration
client.Modules         // Module management & topology
client.Application     // Application lifecycle & config files
client.Media           // Media file management
client.Comm            // Raw bus transactions (I2C, UART, SPI, Socket)
client.NumericResults  // Fast numeric sampling & statistics
client.Calibration     // Calibration channel read/write
client.Audit           // WebApi request audit log
client.Instruments     // Instruments and their function maps
client.Capabilities    // What the WebApi supports
client.Events          // The event stream
client.Subscriptions   // Channel values pushed over the event stream
client.Lease           // The control lease
client.System          // Services, reboot, clock, boot.config
client.Files           // Files in the station's folders
client.Firmware        // Firmware releases and updates
```

| Group | Property | Description | Details |
|-------|----------|-------------|---------|
| [Connection](connection.md) | `client.Connection` | Check hardware manager connectivity | [→](connection.md) |
| [Resources](resources.md) | `client.Resources` | Read/write hardware values (voltages, temperatures, etc.) | [→](resources.md) |
| [Channels](channels.md) | `client.Channels` | Configure multi-purpose I/O channels | [→](channels.md) |
| [Modules](modules.md) | `client.Modules` | Load/unload modules, query hardware topology | [→](modules.md) |
| [Application](application.md) | `client.Application` | Application lifecycle, configuration files | [→](application.md) |
| [Media](media.md) | `client.Media` | Upload/download media files | [→](media.md) |
| [Comm](comm.md) | `client.Comm` | Raw bus transactions (I2C, UART, SPI, Socket) | [→](comm.md) |
| [Numeric Results](numeric-results.md) | `client.NumericResults` | High-speed sampling with server-side statistics | [→](numeric-results.md) |
| [Calibration](calibration.md) | `client.Calibration` | Read and write Calibration channel tables | [→](calibration.md) |
| [Audit](audit.md) | `client.Audit` | Read the WebApi's request audit log | [→](audit.md) |
| [Instruments](instruments.md) | `client.Instruments` | Instruments (power supplies, meters) and their function maps | [→](instruments.md) |
| [Capabilities](capabilities.md) | `client.Capabilities` | API and codec versions, supported features | [→](capabilities.md) |
| [Events](events.md) | `client.Events` | Event stream: connection, configuration and lease changes | [→](events.md) |
| [Subscriptions](events.md#subscriptions) | `client.Subscriptions` | Channel values pushed over the event stream | [→](events.md#subscriptions) |
| [Lease](lease.md) | `client.Lease` | Take the control lease so only this client changes the station | [→](lease.md) |
| [System](system.md) | `client.System` | Services, reboot, clock and start-up configuration (boot.config) | [→](system.md) |
| [Files](files.md) | `client.Files` | Browse, download and upload files in the station's folders | [→](files.md) |
| [Firmware](firmware.md) | `client.Firmware` | List releases, upload packages and update the station | [→](firmware.md) |

## Newer Calls and Older Firmware

The groups from Instruments down need a WebApi that has them; older firmware answers 404 (`Capabilities.GetAsync` returns `CapabilitiesDto.None` instead). Ask [Capabilities](capabilities.md) first where a feature flag exists, or catch `AccordionQ2ApiException` with `StatusCode == 404`.
