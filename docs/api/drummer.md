# Drummer

A Drummer module (Extensions/Drummer in the station's firmware) reaches IEEE 1687 instruments in an FPGA over JTAG:
GPIO, block RAM, clock counters, MMCMs, I2C controllers and targets, logic analysers, link testers, GT transceivers and
memory PHYs. `client.Drummer` finds the modules and runs their instruments' operations.

Every operation the station's Web UI offers on its Drummer > Instruments page (`/drummer/instruments`) is a **transaction** on the instrument's
**Call channel** (`Drummer.MEM.Call`; module operations on `Drummer.Target.Call`). The value sent is a JSON object
with the operation's name in `call` and its arguments; the answer is the operation's result. So a script does exactly
what the page does. The operations, their arguments and their results are listed in the firmware's
`Documentation/02-hardware/drummer.md`.

> Needs firmware with the `resources.transact` capability (`CapabilitiesDto.ResourcesTransact`). Older firmware runs
> transactions for Admin only, under the engine lock: the calls then throw `NotSupportedException` and send nothing.

## Methods

`client.Drummer` (`DrummerGroup`):

| Method | Returns | Description |
|--------|---------|-------------|
| `FindAsync(ct?)` | `Task<List<DrummerModuleRef>>` | The Drummer modules: one per Instrument of type `Drummer`, else one per `Target.Status` with a `Target.Package` beside it. `Prefix` ("Drummer") and `Name`. |
| `Target(prefix)` | `DrummerTarget` | One module. Makes no request. |

`DrummerTarget` (in `AccordionQ2.WebApiClient.Groups`):

| Method | Returns | Description |
|--------|---------|-------------|
| `CallAsync(instrument, name, args?, timeout?, ct?)` | `Task<JObject>` | Runs operation `name` on `<segment>.Call`, follows a job to its end. |
| `ModuleCallAsync(name, args?, timeout?, ct?)` | `Task<JObject>` | A module operation on `Target.Call` (`attention`, `attention_detail`, `la_group_*`), or an instrument's with `"sib"` in `args`. |
| `StatusAsync(ct?)` | `Task<JObject>` | `Target.Status`: connected, the design, the error. |
| `InfoAsync(ct?)` | `Task<JObject>` | `Target.Info`: the design, the chain, every instrument with its register map and tool facts. No scan. |
| `AttentionAsync(ct?)` | `Task<JObject>` | `Target.Attention`: the ATTN word, the instruments asking for attention and why. |
| `JobsAsync(ct?)` | `Task<JArray>` | `Target.Jobs`: running jobs and ended ones not fetched yet. |
| `SnapshotAsync(instrument, ct?)` | `Task<JObject>` | `<segment>.Snapshot`: the instrument's fields and status. While something else holds the JTAG: the last capture with `stale` and `age_ms`. |
| `ReadAsync(name, ct?)` / `WriteAsync(name, value, ct?)` | `Task<string>` / `Task` | A field or semantic channel below the module (`"CLK.Freq0"`, `"GPIO.GPIO0"`). |
| `Net(name)` | `string` | `"Drummer." + name`. |
| `GetPortAsync(ct?)` | `Task<string>` | `Target.Port`: the JTAG port bound now (`"MicroHAPS.JTAG.A"`), `""` when unbound. |
| `BindPortAsync(port, ct?)` | `Task` | Binds the instance to that port now (`""` or null unbinds). 409 when another instance claims it, or while a load, the self-test or a job holds the TAP. Not saved: the module's `Port` setting is the binding at its next load. |
| `LibraryAsync(ct?)` | `Task<JObject>` | `Target.Library`: the station's package library and this instance's selection (below). From the module's memory, no scan. |
| `SelectPackageAsync(sha256, ct?)` | `Task` | `Target.Select`: selects a library package for this instance, by its SHA-256 or 8 or more of its first hex digits. |
| `UnselectPackageAsync(ct?)` | `Task` | `Target.Select` = `""`: back to the `PackagePath` setting, or to no package. |
| `DeletePackageAsync(sha256, ct?)` | `Task` | `Target.Delete`: deletes a library package. 409 while a configured instance selects it (the message names the instances). |
| `UploadPackageAsync(path, timeout?, ct?)` | `Task<ByteStreamStatusDto>` | Uploads a `.drummer` package to `Target.Package` and waits until it is kept and selected (`done`) or refused (`failed`, returned). The connect and any load follow. |
| `BuildAsync(ct?)` | `Task<JObject?>` | `Target.Status`'s `build`: which build the FPGA runs, which build the package has, which rule decided. Null on firmware without build identity. |

A Drummer instance is named as it is configured in modules.config (`Drummer`, `DUT_A`): that name is its channels'
prefix (`Drummer.Target.Status`) and `DrummerModuleRef.Prefix`. An instance whose port is held by someone else (an XVC
client, the port's owner) or down is **parked**: `StatusAsync()` says `"state": "parked"` with `port`, `port_state`,
`holder` and `why`; its field channels and operations answer 409 until it is connected again, which it does by itself.
The ports are [`client.JtagPorts`](jtag-ports.md).

- **An instrument** is a segment or a SIB: `"MEM"` or `"SIB_MEM"`. The leading `SIB_` is dropped.
- **Arguments** are an anonymous object, a dictionary or a `JObject`, with the console's keys
  (`new { addr_start = 0, count = 65536 }`). An argument named `op` (`mem_start`'s) or `name` passes as any other: the
  operation's own name is a separate parameter. `call` and `job` are not arguments.
- **The answer** is the operation's result with two keys the module adds: `ms` (time in the module, TAP wait included)
  and `seq` (the module's TAP hold number: of two results, the higher `seq` is the newer).
- **Jobs**: the module answers within 15 s. An operation still holding the JTAG 10 s after it arrived becomes a job:
  the answer is `{"job": "<ticket>", "state": "running", ...}`. `CallAsync` then sends `{"call": "job", "job": "<ticket>",
  "wait_ms": 10000}` until the job has ended. After `timeout` (60 s by default) it throws a `TimeoutException` that
  names the job's ticket; the job goes on in the module, and its result can be fetched with that ticket (only the
  caller that started the job has it; `Target.Jobs` shows the id before its dot) for 5 minutes after it ends.

## The package library

Firmware with the library (`Target.Library`; older firmware answers 404 for these channels). The station keeps every
uploaded `.drummer` package once, by its SHA-256; each instance has one selected package, kept across restarts, and
that is what it loads.

```csharp
var t = client.Drummer.Target("Drummer");

// The developer flow: upload, and the station keeps it, selects it, loads its bitfile when the FPGA does not run that
// build (LoadOnSelect mismatch), runs discovery and connects
var up = await t.UploadPackageAsync(@"build\microhaps_mem.drummer");
if (up.State != "done") throw new Exception(up.Message);    // refused: the old selection stays

// The upload's wait ends at the selection: the connect and any load follow
for (var s = await t.StatusAsync(); (string?)s["state"] is "connecting" or "loading"; s = await t.StatusAsync())
    await Task.Delay(500);
Console.WriteLine((await t.BuildAsync())?["text"]);
// FPGA runs build unknown · package build none · the package's build: this station loaded ... (marker)

// Back to a known-good build: one call
var lib = await t.LibraryAsync();
foreach (var p in lib["packages"]!)
    Console.WriteLine($"{p["sha256"]!.ToString()[..8]} {p["file"]} {p["uploaded"]} {p["usr_access_time"]} selected by {p["selected_by"]}");
await t.SelectPackageAsync("4f0c1a2b");

await t.DeletePackageAsync("9a1b2c3d");                     // 409 while a configured instance selects it
```

`build.rule` says what decided whether the FPGA runs the package's build: `usr_access` (the FPGA's USR_ACCESS against
the stamp in the bitfile, a bitfile built with `BITSTREAM.CONFIG.USR_ACCESS TIMESTAMP`), `marker` (this station's
record of what it loaded on the port, forgotten at anything it sees that may reconfigure the FPGA), `design_id`,
`discovery`, `device`, or `neither` (`runs_package` null: can't tell). `Target.Status` also has `package` (what the
instance runs), `load_on_select` and `warnings` (instruments without a register map in the package, driver versions,
a bitfile held back).

An instrument whose register map is not in the package is **opaque** (`"opaque": true` in `InfoAsync()`): it stays in the
scan path, its `Snapshot` has its raw words, and only `read`, `scan` without fields, `reset_instrument` and
`clear_errors` work on it. One whose class version has no driver (`"driver": "generic"`) has its register channels and
the generic operations.

## Errors

| Status | Meaning |
|--------|---------|
| 400 | The request is wrong: no such operation, a missing or bad argument, the operation refused it ("no frequency given"). |
| 403 | The key's role is too low: operations and writes need Operator; a transaction or a write on a bus channel (`<seg>.Bus`) needs Admin. |
| **409** | **Busy, try again**: something else holds the JTAG (another operation, a job, a bitfile load, the self-test). The message names it: "la_read on LA holds the JTAG now; try again when it has ended". |
| 500 | An instrument or JTAG fault (no lock, a CRC error, the cable gone), with the message. |

All are `AccordionQ2ApiException` with `StatusCode` (`AccordionQ2SecurityException` for 401 and 403).

## Examples

### Measuring a clock

```csharp
var t = client.Drummer.Target("Drummer");
var info = await t.InfoAsync();
var r = await t.CallAsync("CLK", "clock_measure", new { gate = 1_200_000, channel = 1 });
Console.WriteLine($"{(double)r["hz"]! / 1e6:0.000000} MHz ({r["count"]} edges in {r["ms"]} ms)");
```

### A memory test

```csharp
await t.CallAsync("MEM", "mem_start", new
{
    op = "write_read", order = "up", pattern = "PRBS31", count = 65536, iterations = 1, reread = true,
});
while (true)
{
    var s = await t.SnapshotAsync("MEM");          // a channel read: no audit line, shared with other clients
    if (s["fields"] is JObject f && (int)f["RUNNING"]! == 0) break;
    await Task.Delay(500);
}
var done = await t.CallAsync("MEM", "mem_finish");
Console.WriteLine($"errors {done["mem"]!["err"]}, log records {done["log"]!["count"]}");
```

### Busy: try again

```csharp
for (int attempt = 0; ; attempt++)
{
    try
    {
        await t.CallAsync("MMCM", "mmcm_set", new { mhz = new Dictionary<string, string> { ["0"] = "50" } });
        break;
    }
    catch (AccordionQ2ApiException e) when (e.StatusCode == 409 && attempt < 5)
    {
        await Task.Delay(1000);                    // e.Message says what holds the JTAG
    }
}
```

### Semantic channels

```csharp
string hz = await t.ReadAsync("CLK.Freq0");        // "233250000": 0.1 s of TAP, used again for 1 s
await t.WriteAsync("GPIO.GPIO0", "True");         // one bit, the other outputs kept
```

Poll with channel reads (`SnapshotAsync`, `ReadAsync`) rather than with operations: every operation is one line in the
station's audit log.
