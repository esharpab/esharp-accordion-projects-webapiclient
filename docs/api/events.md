# Events and Subscriptions

The WebApi pushes events to clients over one long-lived HTTP connection (`GET /api/events`, server-sent events): when its connection to the hardware app goes up or down, when channels change, when the control lease changes hands, and the values of channels you subscribe to. Listening saves polling.

`client.Events` reads the stream. `client.Subscriptions` asks for channel values to be delivered on it.

> Check `Capabilities.Has(CapabilitiesDto.Events)` first (see [Capabilities](capabilities.md)). A WebApi without the stream answers 404.

## Events

### Methods

| Member | Returns | Description |
|--------|---------|-------------|
| `ListenAsync(onEvent, ct?)` | `Task` | Opens the stream and calls `onEvent` for each event, in order, until `ct` is cancelled; then it returns. |
| `EventsGroup.SilenceLimit` | `TimeSpan` | 30 seconds. The server pings every 10 s, so this long without any data means the stream is dead. |

`onEvent` is a `Func<ServerEventDto, Task>`. The next event is read when it returns.

`ListenAsync` ends by throwing when the stream fails:

| Exception | When |
|-----------|------|
| `AccordionQ2ApiException` | The stream couldn't be opened (404 for a WebApi without it) |
| `TimeoutException` | No data for `SilenceLimit` |
| `IOException` | The server ended the stream, e.g. because the WebApi restarted |
| `HttpRequestException` | The host can't be reached |

Reconnecting is up to you. The server doesn't replay missed events, so **reload channels** after opening (or reopening) the stream, whenever `Generation` changes, and when the event `Id`s have a gap.

### Event names

| Name | Constant | Sent when | Read the data as |
|------|----------|-----------|------------------|
| `hello` | `ServerEventDto.Hello` | Once, right after the stream opens | `StreamStateDto` (with `ApiVersion` and `StreamId`) |
| `connection` | `ServerEventDto.Connection` | The WebApi's connection to the hardware app went up or down | `StreamStateDto` (with `LastError`) |
| `configuration` | `ServerEventDto.Configuration` | Channels were added, removed or changed; reload them | `StreamStateDto` (with `ChangeType`: `Added`, `Removed` or `Changed`) |
| `values` | `ServerEventDto.Values` | A subscription's values (below) | `ValuesEventDto` |
| `lease` | `ServerEventDto.Lease` | The control lease was taken, released or ran out | `LeaseStateDto` (see [Lease](lease.md)) |

The hardware app may report one change twice, so one change can bring two `configuration` events: reload once per burst.

### Example: Listening with Reconnect

```csharp
using AccordionQ2.WebApiClient;
using AccordionQ2.WebApiClient.Models;

using var cts = new CancellationTokenSource();
long generation = -1;

while (!cts.IsCancellationRequested)
{
    try
    {
        long lastId = 0;
        await client.Events.ListenAsync(async e =>
        {
            bool gap = lastId != 0 && e.Id != lastId + 1;
            lastId = e.Id;

            switch (e.Name)
            {
                case ServerEventDto.Hello:
                case ServerEventDto.Connection:
                case ServerEventDto.Configuration:
                    var state = e.DataAs<StreamStateDto>();
                    Console.WriteLine($"{e.Name}: connected={state.IsConnected}, generation={state.Generation}");
                    if (e.Name != ServerEventDto.Connection || state.Generation != generation || gap)
                    {
                        generation = state.Generation;
                        var channels = await client.Channels.GetAllAsync(cts.Token);
                        Console.WriteLine($"Reloaded {channels.Count} channels");
                    }
                    break;

                case ServerEventDto.Lease:
                    var lease = e.DataAs<LeaseStateDto>();
                    Console.WriteLine(lease.Held ? $"Lease held by {lease.Owner}" : "Lease free");
                    break;
            }
        }, cts.Token);
    }
    catch (Exception ex) when (ex is TimeoutException or IOException or HttpRequestException or AccordionQ2ApiException)
    {
        Console.WriteLine($"Event stream lost ({ex.Message}); reconnecting");
        await Task.Delay(TimeSpan.FromSeconds(2), cts.Token);
    }
}
```

## Subscriptions

A subscription asks the WebApi to send the values of some channels about once per interval, as `values` events on your stream, instead of you reading them every interval. Channels several clients watch are read from the hardware once and shared.

A subscription belongs to the stream it was created for: take the `StreamId` from that stream's `hello` event. It ends when the stream closes, when you delete it, or when it isn't renewed within `ExpiresInMs` (60 s).

### Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `CreateAsync(streamId, channels, intervalMs, ct?)` | `Task<SubscriptionDto>` | Subscribe the stream to `channels` (net names or aliases), every `intervalMs` (100 to 60 000). |
| `UpdateAsync(id, channels, intervalMs, ct?)` | `Task<SubscriptionDto>` | Replace the channels and interval, and renew it. 404 once it has ended. |
| `DeleteAsync(id, ct?)` | `Task` | End the subscription. |

`StreamStateDto.StreamId` is null from a WebApi without subscriptions.

### Example: Subscribing to Values

```csharp
using AccordionQ2.WebApiClient.Models;

var names = new[] { "0.4.ESH10000662.VMON1", "0.4.ESH10000662.IMON1" };
using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));

await client.Events.ListenAsync(async e =>
{
    if (e.Name == ServerEventDto.Hello)
    {
        var hello = e.DataAs<StreamStateDto>();
        var sub = await client.Subscriptions.CreateAsync(hello.StreamId!, names, intervalMs: 500);

        // Renew it well before it expires, for as long as we listen
        _ = Task.Run(async () =>
        {
            while (!cts.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(sub.ExpiresInMs / 2), cts.Token);
                sub = await client.Subscriptions.UpdateAsync(sub.Id, names, intervalMs: 500, cts.Token);
            }
        });
    }
    else if (e.Name == ServerEventDto.Values)
    {
        var v = e.DataAs<ValuesEventDto>();
        foreach (var (name, value) in v.Values)
            Console.WriteLine($"{name} = {value} ({v.AgeMs[name]:0} ms old)");
        foreach (var (name, error) in v.Errors ?? new Dictionary<string, string>())
            Console.WriteLine($"{name}: {error}");
    }
}, cts.Token);
```

A channel whose read fails is left out of `Values` that time and reported in `Errors`. Channels whose read uses data up (UART, SPI, I²C, byte streams, numeric results and similar) are never served to subscriptions; they always appear in `Errors`.

## Models

### `ServerEventDto`

| Member | Type | Description |
|--------|------|-------------|
| `Id` | `long` | Increases by one per event within a stream; a gap means events were dropped |
| `Name` | `string` | The event name (table above) |
| `Data` | `string` | The event's data as JSON |
| `DataAs<T>()` | `T` | The data deserialized as `T`, e.g. `StreamStateDto` |

### `StreamStateDto`

The data of `hello`, `connection` and `configuration` events. Fields an event doesn't carry keep their defaults.

| Property | Type | Description |
|----------|------|-------------|
| `IsConnected` | `bool` | Whether the WebApi is connected to the hardware app |
| `Generation` | `long` | Goes up each time the WebApi connects or reconnects to the hardware app |
| `LastError` | `string?` | In `connection`: why the connection dropped |
| `ApiVersion` | `int` | In `hello`: the contract version |
| `StreamId` | `string?` | In `hello`: the stream's id, for `Subscriptions.CreateAsync` |
| `ChangeType` | `string?` | In `configuration`: `Added`, `Removed` or `Changed` |

### `SubscriptionDto`

| Property | Type | Description |
|----------|------|-------------|
| `Id` | `string` | The subscription's id |
| `ExpiresInMs` | `long` | It ends unless renewed within this long |

### `ValuesEventDto`

| Property | Type | Description |
|----------|------|-------------|
| `Subscription` | `string` | The subscription's id |
| `Values` | `Dictionary<string, string>` | Each channel's value, keyed by the name you subscribed with |
| `AgeMs` | `Dictionary<string, double>` | How old each value is in milliseconds; 0 when it was just read |
| `Errors` | `Dictionary<string, string>?` | Channels that couldn't be read this time, with why; null when all were |
