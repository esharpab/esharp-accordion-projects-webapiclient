# Lease

The control lease lets one client at a time change the station, typically a test station for the length of a sequence. While a client holds it, everyone else's changes are refused, so nothing else moves an output or reconfigures a channel in the middle of a test.

## How it works

- `AcquireAsync` takes the lease. From then on this client sends the lease id (`X-Lease-Id`) with every request until `ReleaseAsync`, so its own requests work as if there were no lease.
- The lease ends by itself unless renewed within its time to live, so a crashed test station doesn't lock the station. Renew it with `RenewAsync` before `ExpiresInMs` runs out.
- While another client holds it, this client gets `AccordionQ2ApiException` with **423** for:
  - changes: every POST, PUT and DELETE except those that change nothing (reads sent as POST, the channel lookup, subscriptions, the lease itself);
  - forced reads: `Resources.GetValueAsync` and `Resources.GetValuesAsync`, which always read the hardware.
- Cached reads (`Resources.ReadValuesAsync` with `maxAgeMs > 0`) and [subscriptions](events.md#subscriptions) keep working for everyone, but don't make the hardware read channels whose read changes something; those values come from the cache, however old. Look at `AgeMs`.
- Every change of the lease is announced as a `lease` event on the [event stream](events.md).

> **Shared `HttpClient`:** the lease id is added to the `DefaultRequestHeaders` of the client's `HttpClient`. If you passed in an `HttpClient` that other code also uses, those requests carry the lease too.

## Methods

| Member | Returns | Description |
|--------|---------|-------------|
| `GetAsync(ct?)` | `Task<LeaseStateDto>` | Who holds the lease, if anyone. Open to everyone. |
| `AcquireAsync(owner, ttlMs = 30000, ct?)` | `Task<LeaseDto>` | Take the lease for `owner` (shown to everyone it blocks), for `ttlMs` (1 000 to 600 000). 409 when someone else holds it. |
| `RenewAsync(ttlMs = null, ct?)` | `Task<LeaseDto>` | Renew the lease this client holds, optionally with a new time to live. 404 once it has ended. Throws `InvalidOperationException` when this client holds no lease. |
| `ReleaseAsync(ct?)` | `Task` | Release the lease this client holds. Does nothing when it holds none, or when the lease has already ended. |
| `HeldLeaseId` | `string?` | The lease this client holds, or null. |
| `LeaseGroup.HeaderName` | `string` | `"X-Lease-Id"` |

## Example: Holding the Lease for a Test Sequence

```csharp
using AccordionQ2.WebApiClient;
using AccordionQ2.WebApiClient.Models;

LeaseDto lease;
try
{
    lease = await client.Lease.AcquireAsync("TAT station 3", ttlMs: 30000);
}
catch (AccordionQ2ApiException ex) when (ex.StatusCode == 409)
{
    var state = await client.Lease.GetAsync();
    Console.WriteLine($"Busy: held by {state.Owner} for another {state.ExpiresInMs / 1000:0} s");
    return;
}

using var stop = new CancellationTokenSource();
var renew = Task.Run(async () =>
{
    while (!stop.IsCancellationRequested)
    {
        await Task.Delay(TimeSpan.FromSeconds(10), stop.Token);
        await client.Lease.RenewAsync(ct: stop.Token);
    }
});

try
{
    await client.Resources.SetValueAsync("0.4.ESH10000662.VSET1", "12");
    // ... the rest of the sequence ...
}
finally
{
    stop.Cancel();
    try { await renew; } catch (OperationCanceledException) { }
    await client.Lease.ReleaseAsync();
}
```

## Handling 423 as Another Client

```csharp
try
{
    await client.Resources.SetValueAsync("0.4.ESH10000662.VSET1", "5");
}
catch (AccordionQ2ApiException ex) when (ex.StatusCode == 423)
{
    var state = await client.Lease.GetAsync();
    Console.WriteLine($"Read-only: {state.Owner} holds the lease since {state.Since:t}");
}
```

## Models

### `LeaseStateDto`

From `GetAsync`, and the data of a `lease` event.

| Property | Type | Description |
|----------|------|-------------|
| `Held` | `bool` | Whether anyone holds the lease |
| `Owner` | `string?` | Who holds it |
| `ExpiresInMs` | `double` | How long until it ends unless renewed |
| `Since` | `DateTimeOffset?` | When it was taken |

### `LeaseDto`

From `AcquireAsync` and `RenewAsync`.

| Property | Type | Description |
|----------|------|-------------|
| `LeaseId` | `string` | The lease id this client now sends |
| `Owner` | `string?` | The owner given |
| `ExpiresInMs` | `double` | How long until it ends unless renewed |
