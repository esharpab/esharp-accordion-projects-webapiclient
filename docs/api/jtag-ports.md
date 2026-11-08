# JTAG ports

A JTAG port is a JTAG TAP that a module on the station owns the way to: MicroHAPS's FT4232H interface A
(`MicroHAPS.JTAG.A`), an FTDI cable of the JtagPorts module (`JTAG_FTDI.JTAG.A`). One holder at a time: the owner itself
(an FPGA image load, a power cycle), an XVC client (Vivado's hardware manager), or the Drummer instance bound to it.
`client.JtagPorts` lists them and switches their XVC server. Firmware with stable module names; on older firmware the
list is empty.

Each port is two channels:

| Channel | Type | What |
|---|---|---|
| `<port>.State` | Register, IN, Unit `jtag-port` | The port's state as JSON (below), from the station's memory. |
| `<port>.Xvc` | Digital, IN/OUT, Unit `jtag-port:xvc` | The XVC server's switch: true, it listens. Writing it needs **Admin**. |

## Methods

`client.JtagPorts` (`JtagPortsGroup`):

| Method | Returns | Description |
|--------|---------|-------------|
| `GetAllAsync(ct?)` | `Task<List<JtagPortDto>>` | Every port with its state read now, by name. |
| `GetStateAsync(port, ct?)` | `Task<JtagPortStateDto?>` | One port's state (`port` is its name or one of its channels' net names). |
| `SetXvcAsync(port, on, ct?)` | `Task` | Switches the XVC server on or off. 403 below Admin; 409 when the TCP port can't be bound. |
| `PortName(name)` | `string` | `"MicroHAPS.JTAG.A"` for itself and for its `.State` or `.Xvc` channel. |

A Drummer instance binds to a port with `client.Drummer.Target("Drummer").BindPortAsync("MicroHAPS.JTAG.A")` and
says which one with `GetPortAsync()` (see [Drummer](drummer.md)).

## Example

```csharp
foreach (var p in await client.JtagPorts.GetAllAsync())
{
    var s = p.State;
    Console.WriteLine($"{p.Name} ({s?.Label}): {s?.State}, held by {s?.Holder?.Kind} {s?.Holder?.Name}, bound to {s?.Bound}");
    if (s?.Xvc is { Enabled: true } xvc)
        Console.WriteLine($"  XVC on {xvc.Listen}, client {xvc.Client ?? "none"}, {xvc.Sessions} sessions");
}

await client.JtagPorts.SetXvcAsync("MicroHAPS.JTAG.A", true);   // an Admin key
```

## Models

### `JtagPortStateDto` (the State channel's JSON)

| Property | JSON | Description |
|----------|------|-------------|
| `Port` | `port` | `"MicroHAPS.JTAG.A"` |
| `Owner` | `owner` | The module that owns it: `"MicroHAPS"` |
| `Label` | `label` | What it opens: `"FT4232H J020324 interface A"` |
| `Seq` | `seq` | One more at every change; a value older than one you have is stale |
| `State` | `state` | `"free"`, `"held"` or `"down"` |
| `Holder` | `holder` | `Kind` (`borrower`, `xvc`, `owner`), `Name`, `What` (`"an FPGA image load"`), `Since`; null when free or down |
| `Why` | `why` | Why it is down: `"the board is powered down (POWER is off)"` |
| `Bound` | `bound` | The Drummer instance that claims it, or null |
| `MaxTckHz` | `max_tck_hz` | The fastest TCK a holder gets |
| `Xvc` | `xvc` | `Enabled`, `Engine`, `Listen` (`"0.0.0.0:2542"`), `Allow`, `Client`, `Waiting`, `Sessions`, `LastEnd` |
| `Since` | `since` | When the state last changed |

## Holders, and who waits

The owner takes the port from anyone; an XVC client takes it from a Drummer instance at its first shift, except while
the instance runs a bitfile load or a self-test (the client then waits for it to end). The instance is then
`parked` and connects again, with discovery, when the port is free. With the switch on and no client, the instance
keeps the port as usual.

In the station's security mode `Off` everyone is Admin, so the switch is anyone's there.
