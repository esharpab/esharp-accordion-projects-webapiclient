# Capabilities

What the WebApi on a station supports: the contract version, its build version, the channel codec version, and a list of features. Use it to decide whether a newer call is available before making it.

The server answers without calling the hardware app, so this works while the hardware app is disconnected too.

## Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `GetAsync(ct?)` | `Task<CapabilitiesDto>` | The WebApi's capabilities. A WebApi that predates `GET /api/capabilities` (404) gives `CapabilitiesDto.None` instead of an exception. |

## Example

Decide by `Features`, not by version numbers:

```csharp
using AccordionQ2.WebApiClient.Models;

var caps = await client.Capabilities.GetAsync();
Console.WriteLine($"WebApi {caps.WebApiVersion}, contract v{caps.ApiVersion}, codec v{caps.CodecVersion}");

if (caps.Has(CapabilitiesDto.Events))
{
    // The event stream is available: see Events
}
else
{
    // Older firmware: poll instead
}
```

## Response Model

### `CapabilitiesDto`

| Property | Type | Description |
|----------|------|-------------|
| `ApiVersion` | `int` | The contract version; `0` for a WebApi that predates it |
| `WebApiVersion` | `string` | The WebApi build's informational version, e.g. `"5.15.0"` |
| `CodecVersion` | `int` | The server's channel codec version (EsharpDefinitions `ChannelFactory.CodecVersion`); compare it with your own before using [encoded channels](channels.md#encoded-channels) |
| `Features` | `string[]` | The supported features |

| Member | Description |
|--------|-------------|
| `Has(feature)` | `true` when `Features` contains `feature` |
| `CapabilitiesDto.None` | What a WebApi without capabilities supports: no features |

### Feature names

| Constant | Value | Means |
|----------|-------|-------|
| `CapabilitiesDto.ChannelsEncoded` | `"channels.encoded"` | `Channels.GetEncodedAsync` and `Channels.ConfigureEncodedAsync` work |
| `CapabilitiesDto.Events` | `"events"` | The [event stream](events.md) works |

The server may list other features too, for example `"instruments"`; `Has` takes any string.
