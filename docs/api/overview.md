# API Overview

`AccordionQ2Client` exposes **eight operation groups**, each covering one area of the hardware API. All methods are **asynchronous** and throw `AccordionQ2ApiException` on HTTP errors.

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
