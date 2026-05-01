# AccordionQ2.WebApiClient

.NET client library for the **AccordionQ2 Hardware Management REST API**.

[![NuGet](https://img.shields.io/nuget/v/AccordionQ2.WebApiClient)](https://www.nuget.org/packages/AccordionQ2.WebApiClient/)
[![PyPI (Python)](https://img.shields.io/pypi/v/accordionq2)](https://pypi.org/project/accordionq2/)

This is the .NET counterpart of the [Python WebApiClient](https://pypi.org/project/accordionq2/). Both libraries expose the same API surface so switching between them feels natural.

## Features

- **Strongly typed** &mdash; full C# model classes and enums for all request/response objects
- **Async-first** &mdash; every method returns `Task` or `Task<T>` with `CancellationToken` support
- **Full API coverage** &mdash; 8 operation groups covering all hardware management endpoints
- **HttpClient integration** &mdash; bring your own `HttpClient` (e.g. from `IHttpClientFactory`) or let the library manage one
- **Cross-platform** &mdash; targets .NET Standard 2.0; works on .NET 5+, .NET Framework 4.6.1+, Mono, Xamarin

## Quick Example

```csharp
using AccordionQ2.WebApiClient;

using var client = new AccordionQ2Client("http://agent64.local:5000");

var status = await client.Connection.GetStatusAsync();
Console.WriteLine($"Connected: {status.IsConnected}");

string temp = await client.Resources.GetValueAsync("TempRegulator.CPU_TEMP");
Console.WriteLine($"CPU temperature: {temp}");
```

## Getting Started

- [Installation](getting-started/installation.md)
- [Quick Start](getting-started/quickstart.md)

## API Reference

- [Overview](api/overview.md) &mdash; all 8 API groups at a glance
- [Resources](api/resources.md), [Channels](api/channels.md), [Modules](api/modules.md), [Application](api/application.md), [Media](api/media.md), [Connection](api/connection.md)
- [Comm (Bus Transactions)](api/comm.md) &mdash; I2C, UART, SPI, Socket
- [Numeric Results](api/numeric-results.md) &mdash; high-speed sampling

## Also Available

| Platform | Package |
|----------|---------|
| .NET Standard 2.0+ | [`AccordionQ2.WebApiClient`](https://www.nuget.org/packages/AccordionQ2.WebApiClient/) via NuGet |
| Python 3.8+ | [`accordionq2`](https://pypi.org/project/accordionq2/) via pip |
