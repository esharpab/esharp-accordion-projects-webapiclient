# Security (`client.Security`)

The station's security endpoints, `api/security` (firmware 7.1.0; client 7.5.0): logins and users, the security mode, API keys, the HTTPS certificate, the security events, the firewall, extensions and the factory reset. A station before 7.1.0 answers 404.

Every call needs the **Admin** role unless the table says otherwise; in security mode `Off` every caller is Admin. None needs the [lease](lease.md). A call that carries a secret (login, claim, own password, a user's password, a new user, installing a certificate) needs HTTPS or the station itself. Over plain HTTP from another host the station refuses it in every mode (400, or a 308 in `On`), but only after the password or private key has crossed the network in clear text. So the client doesn't send it: with a base address that is `http://` and not `localhost` or a loopback address (`127.0.0.1`, `[::1]`), these calls throw `InvalidOperationException` naming the https address, and nothing is sent. For the station's side (what each mode does, the claim, recovery) see the firmware documentation's security pages.

## Logging in

A person logs in with a user name and password; the answer is a token that the client then sends as `Authorization: Bearer` on every request, the event stream included. A token lasts 15 minutes and can be renewed for 12 hours after the login. The client doesn't renew by itself: call `RenewAsync` before `TokenExpiresAt`.

```csharp
using var client = new AccordionQ2Client("https://station-12:5001", new AccordionQ2ClientOptions { CertificateSha256 = "3F:A2:...:9C" });

LoginResponseDto login = await client.Security.LoginAsync("ann", password);
// login.User.Role, login.ExpiresAt (the station's clock), login.ExpiresIn (seconds)

if (client.Security.TokenExpiresAt < DateTimeOffset.UtcNow.AddMinutes(2))
    await client.Security.RenewAsync();      // 401 (InvalidToken) after 12 hours: log in again

await client.Security.LogoutAsync();         // this token and its renewals stop working
```

| Call | Role | What it sends | Answer |
|---|---|---|---|
| `LoginAsync(name, password)` | anyone | no key, no token | `LoginResponseDto`; sets `Token` |
| `RenewAsync()` | Viewer | the token only | `LoginResponseDto`; sets `Token` |
| `LogoutAsync()` | Viewer | the token only | clears `Token` (also when the station can't be reached) |
| `GetMeAsync()` | anyone | as every call | `MeDto`: the mode, `Claimed`, who this client is (`Caller`: `Key` or `User`), `LoginAvailable` |
| `ClaimAsync(name, password)` | anyone | no key, no token | `LoginResponseDto` for the new admin; sets `Token`. Mode `Off` and `Report` only; in `On` 409: an unclaimed station is claimed there with the recovery drive (`RecoveryResetAdminAsync`) |
| `SignOutAllAsync()` | Admin | as every call | every login token on the station ends, this one too (clears `Token`); API keys stay |
| `ChangeOwnPasswordAsync(current, new)` | Viewer | the token only | `LoginResponseDto`: the user's other logins end; sets `Token` |

`Token` can also be set to a token from elsewhere, or to null. With an API key as well, the client sends both and the station uses the key; renew, logout and the own password send only the token, as they act on a login. The client never puts the key or the token in an exception message.

A wrong name or password throws `AccordionQ2SecurityException` with `Reason` `WrongPassword` (the station gives the same answer for a wrong name and a wrong password); a refused token `InvalidToken`; too many failures `AccordionQ2ThrottledException` with `RetryAfter`. See [Error handling](../error-handling.md).

## Users

| Call | Answer |
|---|---|
| `GetUsersAsync()` | `List<UserDto>` (never a password or its hash) |
| `CreateUserAsync(name, role, password)` | `UserDto`; 409 for the first admin of an unclaimed station (claim it, the recovery drive in mode `On`, or an admin API key in `On`) |
| `SetUserPasswordAsync(id, password)` | that user's logins end everywhere |
| `SetUserRoleAsync(id, role)` | `UserDto`; 409 for the last admin user while the station has no admin API key (every mode) |
| `RemoveUserAsync(id)` | their logins end at once; 409 for the last admin user while the station has no admin API key (every mode) |

`role` is `StationRole.Viewer`, `Operator` or `Admin`.

## Mode, API keys and the certificate

| Call | Answer |
|---|---|
| `GetStateAsync()` | `SecurityStateDto`: the mode, the keys and users, the clients blocked now (`Throttled`), the extension upload window, `ModuleImageRole`, the last extension check |
| `SetModeAsync(mode)` | `SecurityStateDto` with `Warnings`; `On` needs an admin key or login on this client, HTTPS available, and this request over HTTPS or on the station |
| `CreateKeyAsync(name, role)` | `CreatedApiKeyDto`: `Key` is shown this once only |
| `RevokeKeyAsync(id)` | it stops working at once |
| `GetCertificateAsync()` | `CertificateDto`: `Sha256` is the fingerprint to pin; `Warnings` when it has expired or expires within 30 days; `DefaultRequestNames` |
| `InstallCertificateAsync(InstallCertificateDto)` | the site's certificate, PEM or PKCS#12; open TLS connections close, pinned clients need the new `Sha256` |
| `RemoveCertificateAsync()` | the self-signed certificate serves again |
| `RegenerateCertificateAsync(extraNames)` | a new self-signed certificate (a new fingerprint) |
| `RequestCertificateAsync(names)` | `CertificateRequestDto`: a new key made on the station (it never leaves it) and the CSR for your CA (`CsrPem`); no names: `DefaultRequestNames`. It replaces a pending request |
| `GetCertificateRequestAsync()` | the pending request (names, `Created`, `CsrPem`; never the key), or null |
| `DeleteCertificateRequestAsync()` | discards the pending request and its key |
| `InstallSignedCertificateAsync(certificatePem)` | the certificate the CA signed for the pending request (leaf and intermediates, PEM, no key): it serves as the site certificate and the request is used up; 400 when it isn't the pending request's |

Your own CA without a private key leaving the station:

```csharp
var request = await client.Security.RequestCertificateAsync();          // the station's default names
File.WriteAllText("station-12.csr", request.CsrPem);                   // give it to the CA
// ... the CA issues station-12.cer (PEM, with its chain)
var installed = await client.Security.InstallSignedCertificateAsync(File.ReadAllText("station-12.cer"));
Console.WriteLine(installed.Sha256);
```

The certificate calls need HTTPS or the station itself: over plain HTTP from another host the station answers 400.

## Security events

```csharp
SecurityEventsDto log = await client.Security.GetEventsAsync(tail: 100,
    since: DateTimeOffset.UtcNow.AddDays(-1), kinds: new[] { "LoginFailed", "Throttled" });
foreach (var e in log.Events)                 // newest first
    Console.WriteLine($"{e.Time:u} {e.Kind} {e.Who} from {e.Client}");
```

`tail` is 1 to 5000 (200 by default); `kinds` are the station's event names. The answer says how long the log keeps events (`MaxAgeDays`, `MaxBytes`) and the oldest kept.

## Firewall

| Call | Answer |
|---|---|
| `GetHostAsync()` | `HostSecurityDto`: the settings, whether the firewall is in effect, the TCP ports the firewall opens in `On` |
| `SetHostAsync(HostSettingsDto)` | stored in every mode, applied at once in `On`: `XvcPorts` (closed when empty), `XvcFrom` (CIDRs or addresses; empty or null for any) |

In `On` the firewall drops connections that other hosts make to ports containers publish on the station.

## Extensions and module images

| Call | Answer |
|---|---|
| `GetExtensionsAsync()` | `ExtensionsReportDto`: the release checked against, every folder of hw/additional with its `State` and `Digest`, the modules loading from outside it |
| `AcceptExtensionAsync(folder, digest)` | accepts the folder's content as it is now (`""` for the top of hw/additional) by the digest just read; 409 when it changed since |
| `WithdrawExtensionAsync(folder)` | in `On` the folder no longer loads unless it is the signed release's |
| `SetExtensionUploadsAsync(minutes)` | opens the file API's changes to the extensions folder in `On` for 1 to 1440 minutes, or closes them (0) |
| `SetModuleImageRoleAsync(role)` | the role module images need in `On`: `Admin` (the default) or `Operator` |

## Recovery drive

For a station whose admin password is lost, by someone at the station. No credential is needed: the drive proves physical access, and the secret this client keeps proves that it made the drive. The client makes the drive, not the station:

```csharp
RecoveryDto? window = await client.Security.GetRecoveryAsync();          // null before 7.1.0
byte[] file = client.Security.MakeRecoveryDrive(window!.Station!);        // the secret stays in client.Security.RecoveryKey
File.WriteAllBytes(@"E:\accordion-recovery.json", file);                  // the top folder of a FAT32 or exFAT USB drive
client.Security.SaveRecoveryKey(@"C:\keys\recovery-key.json");           // optional: to reset from another process
// plug the drive into the station (restart the station with it in when its USB ports are off), then:
client.Security.LoadRecoveryKey(@"C:\keys\recovery-key.json");           // only in another process
window = await client.Security.GetRecoveryAsync();
if (window?.IsOpen == true)
    await client.Security.RecoveryResetAdminAsync("admin", newPassword);
await client.Security.LoginAsync("admin", newPassword);
```

`GetRecoveryAsync()` says whether the recovery window is open (a recovery drive for this station is in it), until when, whether the station's USB ports are off (`UsbPortsOff`: the drive then needs a restart of the station), the station's name (`Station`, which the drive must carry), when the drive in the station was made (`Created`) why the window is closed (`Why`, such as `Outdated` for a drive made by an earlier firmware build, `Expired` after 24 hours, `ClockBehind`) and the station's clock (`Now`, UTC); null before 7.1.0.

`MakeRecoveryDrive(station)` draws a random 32-byte secret, keeps it in `RecoveryKey` and returns `accordion-recovery.json`: `kind`, `station`, `created` (this computer's clock, UTC) and `hash`, the lower-case hex SHA-256 of the secret's bytes, a 0x00 byte, the station's name in upper case, a 0x00 byte and `created` (`RecoveryDrive.Hash`). The file never holds the secret. `SaveRecoveryKey(path)` and `LoadRecoveryKey(path)` write and read the key (JSON with the secret). On Linux and macOS the file is made readable by its owner only (0600); on Windows it gets its folder's permissions, so choose a folder only you can read. A drive opens the recovery window until 24 hours after `created`, on the station's clock (`created` may be up to an hour ahead of it). When this computer's clock and the station's differ by more than an hour (a station without a time server), make the drive at the station's time: `MakeRecoveryDrive(window.Station!, window.Now)`.

`RecoveryResetAdminAsync(user, password)` sends the key's secret and sets that user's password (`null` means `admin`; made as Admin if missing, made Admin if not) and ends their logins; the mode, the keys and the other users stay. One reset per insertion of the drive; the window also closes when the drive is taken out. Without a `RecoveryKey` it throws `InvalidOperationException` and sends nothing. 409 when no recovery drive is in the station, 403 when the drive was made by another client or browser (counted like a wrong password; 429 after several), 400 for a password the station refuses. Like a login, it needs HTTPS or the station itself. `DownloadRecoveryDriveFileAsync()` is obsolete: it reads the station's name and calls `MakeRecoveryDrive`.

## Factory reset

```csharp
FactoryResetPreviewDto preview = await client.Security.GetFactoryResetPreviewAsync();
// preview.Erased, preview.Kept, preview.HostName
await client.Security.StartFactoryResetAsync(preview.HostName);   // the host name, typed to confirm
```

The reset starts and the station reboots: it comes back in mode `Off`, unclaimed, on DHCP, with a new certificate, so this client loses it. `eraseCalibration: true` also erases hw/calibration. 400 for a wrong name, 409 while an update runs or a client holds the lease, 501 off a station. `GetLastFactoryResetAsync()` gives the last reset's record, or null when there is none.
