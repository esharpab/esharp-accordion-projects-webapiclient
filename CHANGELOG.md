# Changelog

All notable changes to AccordionQ2.WebApiClient. Format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/); versions follow [Semantic Versioning](https://semver.org/).

---

## [Unreleased]

### Added

- The recovery drive is made by the client (firmware 7.1.0's final build): `Security.MakeRecoveryDrive(station)` returns
  the bytes of `accordion-recovery.json` (`kind`, `station`, `created`, `hash`; never the secret) and keeps the drive's
  random secret in `Security.RecoveryKey` (`RecoveryDriveKey`); `SaveRecoveryKey(path)` and `LoadRecoveryKey(path)`
  write and read it in a file the caller chooses. `RecoveryDrive.Hash` is the hash's encoding (SHA-256 of the secret,
  0x00, the station's name in upper case, 0x00, `created`), the same as the station's, the Web UI's and the Python
  client's. `RecoveryDto` has `Station` (the name a drive must carry), `Created`, `Why` and `Now` (the station's clock, for
  `MakeRecoveryDrive(station, now)` when the station's clock is more than an hour ahead of this computer's). On Linux and macOS
  `SaveRecoveryKey` makes the key file readable by its owner only (0600).

### Changed

- `Security.RecoveryResetAdminAsync` sends the secret of `RecoveryKey`, and throws `InvalidOperationException` without
  one (nothing is sent). The station answers 403 when the drive in it was made by another client or browser.

### Deprecated

- `Security.DownloadRecoveryDriveFileAsync()`: the station no longer makes the drive's file. It now reads the station's
  name and calls `MakeRecoveryDrive`.

## [7.5.0] – 2026-11-19

### Fixed

- CA mode (`TrustedCaCertificate`) accepts a station certificate issued by an intermediate CA the station sends in
  the TLS handshake (FW7 CLI-04). Those intermediates are untrusted helpers: the chain must still end exactly at
  `TrustedCaCertificate` and allow server authentication. 7.4.0 and earlier accepted only a certificate the CA issued
  directly.
- `Events.ListenAsync` shows the station's message for any status it refuses the stream with, e.g. a 308 whose
  message names the https URL when a station in security mode On is reached over plain HTTP, or a 503 with the
  reason HTTPS isn't available (CLI-05). It reads at most 64 KiB of the error body and waits at most 5 s for it: a
  body that stalls (e.g. from a proxy) gives `HTTP n`, as before.

### Added

- `client.Security`: the station's security endpoints (firmware 7.1.0, `api/security`; FW7 CLI-04 follow-up), with the
  routes, verbs, bodies and answers of the WebApi's controllers. Logins: `LoginAsync`, `RenewAsync`, `LogoutAsync`,
  `GetMeAsync`, `ClaimAsync`, `ChangeOwnPasswordAsync`; the client then sends the login token as `Authorization: Bearer`
  (`Security.Token`, and `Security.TokenExpiresAt` on this computer's clock; it doesn't renew by itself). A login and a
  claim go without the client's key or token; renew, logout and the own password with the token only. Users:
  `GetUsersAsync`, `CreateUserAsync`, `SetUserPasswordAsync`, `SetUserRoleAsync`, `RemoveUserAsync`. The mode and keys:
  `GetStateAsync`, `SetModeAsync`, `CreateKeyAsync`, `RevokeKeyAsync`. The certificate: `GetCertificateAsync`,
  `InstallCertificateAsync`, `RemoveCertificateAsync`, `RegenerateCertificateAsync`. `GetEventsAsync(tail, since, kinds)`
  (the security events). The firewall: `GetHostAsync`, `SetHostAsync` (the XVC ports). Extensions: `GetExtensionsAsync`, `AcceptExtensionAsync`, `WithdrawExtensionAsync`,
  `SetExtensionUploadsAsync`, `SetModuleImageRoleAsync`. The factory reset: `GetFactoryResetPreviewAsync`,
  `StartFactoryResetAsync`, `GetLastFactoryResetAsync` (null without a record). The models are in `SecurityModels.cs`,
  with the enums `StationRole` and `SecurityMode` for the calls' arguments; answers keep the station's names as strings.
  The calls that carry a secret (login, claim, own password, a new user, a user's password, installing a certificate)
  throw `InvalidOperationException` instead of sending it when the base address is plain `http://` to another host
  (not `localhost` or a loopback address): the station would refuse them there, but only after the secret had crossed
  the network in clear text.
- The recovery drive (firmware 7.1.0), for a station whose admin password is lost: `Security.GetRecoveryAsync()`
  (`RecoveryDto`: whether the recovery window is open, until when, whether the USB ports are off; null before 7.1.0),
  `DownloadRecoveryDriveFileAsync()` (the bytes of `accordion-recovery.json`, to copy to a USB drive; it names the
  station and holds no secret) and `RecoveryResetAdminAsync(user, password)` (`RecoveryResetDto`; while a recovery drive
  for the station is in it, anyone at the station can set an admin password; sent without the client's key or token,
  never over plain HTTP to another host; 409 without a drive). All three need no credential.
- `Security.ClaimAsync(name, password)` takes no setup code (firmware 7.1.0): in security mode Off
  and Report it makes the first admin of an unclaimed station from any address, over HTTPS; in On the station answers
  409, and an unclaimed station is claimed with the recovery drive (`RecoveryResetAdminAsync`). `SetUserRoleAsync` and
  `RemoveUserAsync` answer 409 for the last admin user while the station has no admin API key, in every mode.
- `Security.SignOutAllAsync()` (Admin; `POST api/security/sessions/sign-out-all`): every login token on the station
  ends, this client's own too (it forgets `Token`); API keys keep working.
- `client.Docs`: the station's documentation for scripts and AI agents (firmware 7.1.0, `api/docs`): `ListAsync()`
  (`DocsListDto`: the pages with their path, title, section and summary, `/docs/` on the station's Web UI and its
  llms.txt; empty with a note on a station without the pages or before 7.1.0) and `GetAsync(path, html: false)` (the
  page's Markdown, or its HTML page). No credential needed. `CapabilitiesDto.Docs` (`docs`). docs/api/docs.md.
- The station's certificate request (firmware 7.1.0, FW7 CERT-01), for a site CA without a private key leaving the
  station: `Security.RequestCertificateAsync(names)` (a key made on the station and its CSR, `CertificateRequestDto`),
  `GetCertificateRequestAsync()` (null when none is pending), `DeleteCertificateRequestAsync()`, and
  `InstallSignedCertificateAsync(certificatePem)` (the signed certificate and its chain, no key). `CertificateDto` has
  `DefaultRequestNames`, and on a GET `Warnings` names an expired certificate or one that expires within 30 days.
- `AccordionQ2ThrottledException` (a subclass of `AccordionQ2ApiException`) for 429, the slow-down of security mode On
  (FW7 SEC-06, CRA-08): `RetryAfter` (from `Retry-After`, seconds or an HTTP date), `StationMessage`, `RequiredRole`, and
  a message that says it is a slow-down and when to try again. 7.4.0 and earlier threw a plain `AccordionQ2ApiException`.
  Only a 429 with `Retry-After` or the station's `SecurityError` is one: another 429, such as a stream's subscription
  limit, stays a plain `AccordionQ2ApiException` with the station's text. A 403 whose `requires` is `Anonymous` (the
  claim from another network segment) is a plain `AccordionQ2ApiException` too, not `RoleTooLow`.
- `AccordionQ2ClientOptions.MaxThrottleWait` (zero by default: off, not even a `Retry-After` of 0 is resent): a 429 whose Retry-After is at most this long is
  waited out and the request sent again, at most 3 times; a stream upload (`Channels.UploadByteStreamAsync`) is never
  resent. The event stream's opening waits the same way; the stream still doesn't reconnect by itself.
- `AccordionQ2SecurityReason.InvalidToken` (401 for a login token the station no longer accepts) and `WrongPassword`
  (401 from a login or an own password change). A 403 for a login says "login" instead of "API key". The client never
  puts the login token in an exception message, as for the key.
- The `AccordionQ2Certificates.Validate(certificate, presented, errors, sha256, trustedCa)` overload, which takes the
  chain the TLS stack built (the validation callback's `X509Chain`), for callers who configure their own handler.
  `CreateValidationCallback` now passes its chain to it.
- `System.GetNetworkAsync()` (`GET api/system/network`, firmware 7.1.0, capability `network.ipv6`; FW7 NET-05): the
  station's interfaces and every address on them, IPv4 and IPv6, as `NetworkDto` / `NetworkInterfaceDto` /
  `NetworkAddressDto`: family, kind (global, unique-local, link-local), origin (static, dhcp, automatic, temporary), the
  kernel's deprecated, tentative and duplicate-address flags, the valid lifetime and whether boot.config lists it.
- `BootAddressDto.StaticIp` documents IPv6 static addresses, which boot.config takes from firmware 7.1.0
  (`2001:db8::10/64`; /64 when none is given, stored canonical).
- Firmware security updates (firmware 7.1.0; FW7 CRA-05, CRA-11): `FirmwareReleaseDto.Security` (a security release),
  `FirmwareReleasesDto.SecurityUpdate` (the release to install for security fixes), and on `FirmwareStateDto` the
  `Runtime` (`RuntimeDto`, the .NET the WebApi runs; firmware 7.0.5 already sends it), `AutoUpdate` (`AutoUpdateDto`, with
  `ModeIsDefault` and `WindowIsDefault`),
  `OsUpdates` (`OsUpdatesDto`: the Debian security updates the firmware carries and the .NET runtimes it removed) and
  `Sbom`. New calls on `Firmware`: `SetAutoUpdateAsync(mode, window)`, `CheckAutoUpdateAsync()`,
  `InstallOsUpdatesAsync(force)` (`OsUpdateRecordDto`), `GetOsUpdatesLogAsync()` and `GetSbomAsync()` (the CycloneDX 1.6
  JSON as a string).

### Compatibility

- Source and binary compatible with 7.4.0. The 4-argument `Validate` overload is unchanged (it has no chain, so it
  still accepts only a certificate the CA issued directly). Pin and OS-trusted paths are unchanged.
- `GetNetworkAsync` and the network DTOs are additions. Against firmware before 7.1.0 the call throws
  `AccordionQ2ApiException` with status 404; check the capability `network.ipv6` first.
- The firmware DTO members are additions with defaults (false, null): against firmware before 7.1.0 they stay so. The new
  firmware calls throw `AccordionQ2ApiException` with status 404 there.
- `client.Security` is an addition; against firmware before 7.1.0 its calls throw `AccordionQ2ApiException` with status
  404 (`GetLastFactoryResetAsync` gives null). A 429 is now `AccordionQ2ThrottledException`, still an
  `AccordionQ2ApiException` with `StatusCode` 429, so existing catches keep working. `AccordionQ2SecurityReason` has two
  new values: a `switch` over it without a default case should handle them. `StationRole` and `SecurityMode` are new
  names in `AccordionQ2.WebApiClient.Models`: code that imports that namespace and another with the same names needs an
  alias.
- Event-stream errors other than 401, 403 and a 404 without a message now carry the station's message instead of
  "The event stream couldn't be opened: HTTP n". A 3xx without a message now says the client doesn't follow
  redirects and to check the base URL.

---

## [7.4.0] – 2026-11-08

### Added

- **The Drummer package library** (firmware with `Target.Library`): `DrummerTarget.LibraryAsync()`,
  `SelectPackageAsync(sha256)` (a full SHA-256 or 8 or more of its first hex digits), `UnselectPackageAsync()`,
  `DeletePackageAsync(sha256)` (409 while a configured instance selects it) and `UploadPackageAsync(path, timeout?)`
  (`Channels.UploadByteStreamAsync` on `Target.Package`, then its wait: done when the package is kept and selected; the
  connect and any load follow).
- `DrummerTarget.BuildAsync()`: `Target.Status`'s `build` (which build the FPGA runs, which build the package has,
  which rule decided).
- `JtagPortStateDto.Incarnation` and `Disturbed` (`JtagDisturbedDto`: `Count`, `Why`, `Since`): what may have
  reconfigured the FPGA behind the port.

### Compatibility

- Source and binary compatible with 7.3.0. Against firmware without `Target.Library` the library calls throw the
  station's 404 for an unknown channel; `BuildAsync` returns null; `Incarnation` is empty and `Disturbed` null.

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
