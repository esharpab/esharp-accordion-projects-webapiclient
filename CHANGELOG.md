# Changelog

All notable changes to AccordionQ2.WebApiClient. Format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/); versions follow [Semantic Versioning](https://semver.org/).

---

## [7.3.0] – 2026-11-08

### Added

- **Stable module names** (firmware with `names.stable` in its capabilities): a software module's net names start with
  its configured name (`MicroHAPS.HCI.Scan`, `Drummer.Target.Status`) instead of a load-order number
  (`1.MicroHAPS.HCI.Scan`); hardware keeps its slot address (`0.2.ESH10000560.CH1`).
- `Modules.GetPresentAsync()` (`GET /api/modules/present`): every module present, the hardware in the backplane by slot
  and the software modules loaded by name (`ModulePresenceDto`: `Kind`, `Address`, `Prefix`, `Name`, `ProductId`,
  `Revision`, `SerialNumber`, `ClassName`). A module's channels are those whose net name starts with `Prefix + "."`.
- **`client.JtagPorts`** (`JtagPortsGroup`): the station's JTAG ports, found by their State channels (Unit `jtag-port`).
  `GetAllAsync()` with each port's state (`JtagPortDto`, `JtagPortStateDto`: free, held or down, the holder, the bound
  Drummer instance, the XVC server), `GetStateAsync(port)`, `SetXvcAsync(port, on)` (Admin). docs/api/jtag-ports.md.
- `DrummerTarget.GetPortAsync()` and `BindPortAsync(port)`: the instance's `Target.Port` (409 when another instance
  claims the port).
- `CapabilitiesDto.Names` (`NamesCapabilityDto`: `Stable`, `OldNamesAccepted`, `Legacy` with the old names' map), for a
  client that translates the names it stored.

### Changed

- The examples and docs use the stable names: `Drummer.LA.Call` for `3.Drummer.LA.Call`, `Drummer.GPIO.GPIO0` for a
  GPIO pin, `MicroHAPS.JTAG.FPGA_IMAGE`. `DrummerGroup.FindAsync()` is unchanged: it finds the instances by their
  Instrument channel, whatever they are named.

### Compatibility

- Source and binary compatible with 7.2.0. `GetPresentAsync` answers 404 on firmware before stable names
  (`GetPhysicalSystemAsync` lists the hardware there); `JtagPorts.GetAllAsync` is empty there, and `Names` is null.
- A script that stored old net names and compares them with `Channels.GetAllAsync()` finds no match on the new
  firmware: the station accepts an old name in a request (with a warning line and the header `X-Accordion-Old-Names`)
  for two releases, but a list comparison never meets that translation.

---

## [7.2.0] – 2026-11-07

### Added

- **`client.Drummer`** (`DrummerGroup`, `DrummerModuleRef` and `DrummerTarget`, in `AccordionQ2.WebApiClient.Groups`):
  the Drummer modules, which reach IEEE 1687 instruments in an FPGA over JTAG. `FindAsync()` lists them (by the
  Instrument of type `Drummer`, else by `Target.Status` beside `Target.Package`); `Target("3.Drummer")` gives one.
- `DrummerTarget.CallAsync(instrument, name, args, timeout)` runs an instrument operation (`clock_measure`, `mmcm_set`,
  `mem_start`, ...) as a transaction on its Call channel (`3.Drummer.MEM.Call`) and returns the result as a `JObject`
  (with the module's `ms` and `seq`). The instrument is a segment or a SIB (`"MEM"`, `"SIB_MEM"`). An operation that
  becomes a job is followed (`{"call": "job"}`, `wait_ms` 10000) to its end; after the timeout (60 s by default) a
  `TimeoutException` names the job's id, and the job goes on in the module. `ModuleCallAsync(name, args)` for
  `Target.Call`.
- `DrummerTarget.StatusAsync`, `InfoAsync`, `AttentionAsync`, `JobsAsync`, `SnapshotAsync(instrument)`,
  `ReadAsync(name)`, `WriteAsync(name, value)` and `Net(name)`.
- `CapabilitiesDto.ResourcesTransact` (`"resources.transact"`): transactions on any channel for Operator (Admin on bus
  channels), answers not cached, a busy target answers 409.
- docs/api/drummer.md.

### Compatibility

- The Drummer calls check `resources.transact` first (kept for 60 s, asked again after a failed call). Without it
  (older firmware runs transactions for Admin only, under the engine lock) they throw `NotSupportedException` and send
  nothing.
- A 409 (`AccordionQ2ApiException.StatusCode` 409) is a busy target: try again.
- Source and binary compatible with 7.1.0.

---

## [7.1.0] – 2026-10-28

### Added

- `ChannelConfigRequest.Value`: an output's value, applied in the same configure as `Direction`, `ChannelType`
  and `Details`, so an MPIO switched from input to output starts at that level instead of the last value read
  while it was an input. A string as `Resources.SetValueAsync` takes it (`"1.8"`, `"True"`); `WithValue(double)`
  and `WithValue(bool)` format it (invariant culture). Left out of the request when null.
- **Cache-only reads**: `Resources.ReadCachedValuesAsync(names, maxAgeMs = 0)` sends `values/get` with
  `cacheOnly` (firmware 7.0.5 or later). The station answers from its value cache alone, never reads the
  hardware and never refuses it under the lease. A name with no cached value at most `maxAgeMs` old (any age
  for 0), and every channel whose reads consume data, is in the new `ResourceValuesDto.NotCached` instead of
  `Resources`. The call checks `CapabilitiesDto.ValuesCacheOnly` (`"values.cacheOnly"`) first, because an older
  WebApi ignores the field and reads; without it, `NotSupportedException` and no values are asked for. The check
  is kept for 60 s (`ResourcesGroup.CapabilityCheckLifetime`) and redone after any failed call, so a station
  rolled back to older firmware is caught; and "any age" (`maxAgeMs` 0) is sent as `int.MaxValue` ms, so such a
  station would answer from its cache rather than force a read of every name.
  Existing calls send and return what they did (`NotCached` is null for them).
- `Channels.UploadByteStreamAsync(name, stream | path, target, force)`: streams a file to a ByteStream file channel
  (`POST /api/channels/bytestream`, firmware 7.0.5), e.g. an FPGA image to MicroHAPS's `JTAG.FPGA_IMAGE`, loaded
  into the KU5P's SRAM by openFPGALoader without Vivado. The stream is sent as it is read, not buffered whole.
- `Channels.GetByteStreamStatusAsync(name)` and `Channels.WaitByteStreamAsync(name, timeout)`: the load's state,
  progress and result, and the last image's name, size and SHA-256 (`ByteStreamStatusDto`, `ByteStreamImageDto`).

### Compatibility

- **Needs firmware 7.0.5 or later.** Older firmware ignores the field (200 OK) and the output starts at its last
  reading. Without `Value`, firmware 7.0.5 starts a channel that becomes an output at its default value.
- Source and binary compatible with 7.0.0.

---

## [7.0.0] – 2026-10-28

Secured stations (FW7 CLI-01): an API key, HTTPS with a pinned certificate, and IPv6 addresses.
AccordionQ2.WebApiClient 7.0.0 matches the Python client `accordionq2` 7.0.0 and Accordion firmware 7.

### Compatibility with 5.x

The major version follows the firmware. For most code 7.0.0 is a recompile-and-go upgrade.

- **Source compatible for normal use.** `new AccordionQ2Client(url)`, `new AccordionQ2Client(url, httpClient)`
  and every group method keep their signatures and results. Without an API key or a certificate pin the
  client sends exactly what 5.28 sent.
- **Not binary compatible.** Assemblies built against 5.x must be rebuilt against 7.0.0.
- **Existing error handling keeps working.** `AccordionQ2SecurityException` (a 401/403) derives from
  `AccordionQ2ApiException`, so `catch (AccordionQ2ApiException)` still catches it.
- **Every firmware works.** Against firmware 6.x, or a firmware 7 station whose security mode is `Off` (the
  default), the client behaves as 5.28. Firmware 6.x ignores a key sent to it.
- **What can break:**
  - `new AccordionQ2Client(url, null)` no longer compiles (`null` could be an `HttpClient` or the new
    options); write `new AccordionQ2Client(url)`.
  - A class deriving from `ApiGroupBase` outside the library no longer compiles; its constructor is internal.
  - The client no longer sets `BaseAddress` on a caller's own `HttpClient`. Code that relied on that side
    effect to send relative requests itself must set it.
  - A client that builds its own `HttpClient` no longer follows redirects, so the key never reaches another
    host; a 3xx throws `AccordionQ2ApiException`. A station's API never redirects.
  - A base URL must be an absolute `http://` or `https://` URL with a host.

### Added

- `AccordionQ2ClientOptions`: `ApiKey` (sent as `X-Api-Key` on every request, the event stream included,
  never put in an exception message or `ToString()`), `CertificateSha256` (pin the station's HTTPS
  certificate; checked during the handshake, before the key is sent), `TrustedCaCertificate` (a site CA; the
  certificate must chain to it, match the host and be meant for TLS servers), `HttpClient`, `Handler`.
- `AccordionQ2Client.CreateHandler(options)` for `IHttpClientFactory` callers who want the certificate trust.
- `AccordionQ2SecurityException` with `Reason` (missing key, invalid key, role too low) and `RequiredRole`.
- `AccordionQ2Certificates`: fingerprint normalisation and `Sha256Of(certificate)`.
- `Capabilities.GetAsync()` reports the station's security mode.
- IPv6 base URLs in brackets, with a zone id as `%eth0` or RFC 6874's `%25eth0`.
- docs/security.md: modes, keys, roles, pins, a site CA, IPv6.

## [5.28.1] – 2026-10-03

Docs for every group; `StartUpdateAsync`'s errors. Earlier versions: see the git history.
