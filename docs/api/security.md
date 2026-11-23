# Security (`client.Security`)

The station's security endpoints, `api/security` (firmware 7.1.0; client 7.5.0): logins and users, the security mode, API keys, the HTTPS certificate, the security events, the firewall and SSH, extensions and the factory reset. A station before 7.1.0 answers 404.

Every call needs the **Admin** role unless the table says otherwise; in security mode `Off` every caller is Admin. None needs the [lease](lease.md). A call that carries a secret (login, claim, own password, a user's password, a new user, installing a certificate) needs HTTPS or the station itself. Over plain HTTP from another host the station refuses it in every mode (400, or a 308 in `On`), but only after the password, setup code or private key has crossed the network in clear text. So the client doesn't send it: with a base address that is `http://` and not `localhost` or a loopback address (`127.0.0.1`, `[::1]`), these calls throw `InvalidOperationException` naming the https address, and nothing is sent. For the station's side (what each mode does, the claim, recovery) see the firmware documentation's security pages.

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
| `ClaimAsync(setupCode, name, password)` | anyone | no key, no token | `LoginResponseDto` for the new admin; sets `Token` |
| `ChangeOwnPasswordAsync(current, new)` | Viewer | the token only | `LoginResponseDto`: the user's other logins end; sets `Token` |

`Token` can also be set to a token from elsewhere, or to null. With an API key as well, the client sends both and the station uses the key; renew, logout and the own password send only the token, as they act on a login. The client never puts the key or the token in an exception message.

A wrong name or password throws `AccordionQ2SecurityException` with `Reason` `WrongPassword` (the station gives the same answer for a wrong name and a wrong password); a refused token `InvalidToken`; too many failures `AccordionQ2ThrottledException` with `RetryAfter`. See [Error handling](../error-handling.md).

## Users

| Call | Answer |
|---|---|
| `GetUsersAsync()` | `List<UserDto>` (never a password or its hash) |
| `CreateUserAsync(name, role, password)` | `UserDto`; 409 for the first admin of an unclaimed station (claim it, or use an admin API key in mode `On`) |
| `SetUserPasswordAsync(id, password)` | that user's logins end everywhere |
| `SetUserRoleAsync(id, role)` | `UserDto`; 400 when it would leave mode `On` without an admin |
| `RemoveUserAsync(id)` | their logins end at once; 400 when it would leave `On` without an admin |

`role` is `StationRole.Viewer`, `Operator` or `Admin`.

## Mode, API keys and the certificate

| Call | Answer |
|---|---|
| `GetStateAsync()` | `SecurityStateDto`: the mode, the keys and users, the clients blocked now (`Throttled`), the extension upload window, `ModuleImageRole`, the last extension check |
| `SetModeAsync(mode)` | `SecurityStateDto` with `Warnings`; `On` needs an admin key or login on this client, HTTPS available, and this request over HTTPS or on the station |
| `CreateKeyAsync(name, role)` | `CreatedApiKeyDto`: `Key` is shown this once only |
| `RevokeKeyAsync(id)` | it stops working at once |
| `GetCertificateAsync()` | `CertificateDto`: `Sha256` is the fingerprint to pin |
| `InstallCertificateAsync(InstallCertificateDto)` | the site's certificate, PEM or PKCS#12; open TLS connections close, pinned clients need the new `Sha256` |
| `RemoveCertificateAsync()` | the self-signed certificate serves again |
| `RegenerateCertificateAsync(extraNames)` | a new self-signed certificate (a new fingerprint) |

## Security events

```csharp
SecurityEventsDto log = await client.Security.GetEventsAsync(tail: 100,
    since: DateTimeOffset.UtcNow.AddDays(-1), kinds: new[] { "LoginFailed", "Throttled" });
foreach (var e in log.Events)                 // newest first
    Console.WriteLine($"{e.Time:u} {e.Kind} {e.Who} from {e.Client}");
```

`tail` is 1 to 5000 (200 by default); `kinds` are the station's event names. The answer says how long the log keeps events (`MaxAgeDays`, `MaxBytes`) and the oldest kept.

## Firewall, SSH and SSH keys

| Call | Answer |
|---|---|
| `GetHostAsync()` | `HostSecurityDto`: the settings, whether the firewall and SSH settings are in effect, the TCP ports the firewall opens in `On`, the SSH port, the number of SSH keys |
| `SetHostAsync(HostSettingsDto)` | stored in every mode, applied at once in `On`: `SshAllowed`, `SshFrom`, `XvcPorts`, `XvcFrom` (CIDRs or addresses; empty or null for any) |
| `GetSshKeysAsync()` | `List<SshKeyDto>` of the `accordion` user |
| `AddSshKeyAsync(line)` | one authorized_keys line without options: whoever holds the key gets a shell on the station |
| `RemoveSshKeyAsync(id)` | `SshKeysDto`: the keys left, with a warning when that was the last one in `On` |

## Extensions and module images

| Call | Answer |
|---|---|
| `GetExtensionsAsync()` | `ExtensionsReportDto`: the release checked against, every folder of hw/additional with its `State` and `Digest`, the modules loading from outside it |
| `AcceptExtensionAsync(folder, digest)` | accepts the folder's content as it is now (`""` for the top of hw/additional) by the digest just read; 409 when it changed since |
| `WithdrawExtensionAsync(folder)` | in `On` the folder no longer loads unless it is the signed release's |
| `SetExtensionUploadsAsync(minutes)` | opens the file API's changes to the extensions folder in `On` for 1 to 1440 minutes, or closes them (0) |
| `SetModuleImageRoleAsync(role)` | the role module images need in `On`: `Admin` (the default) or `Operator` |

## Factory reset

```csharp
FactoryResetPreviewDto preview = await client.Security.GetFactoryResetPreviewAsync();
// preview.Erased, preview.Kept, preview.HostName
await client.Security.StartFactoryResetAsync(preview.HostName);   // the host name, typed to confirm
```

The reset starts and the station reboots: it comes back in mode `Off`, unclaimed, on DHCP, with a new certificate, so this client loses it. `eraseCalibration: true` also erases hw/calibration. 400 for a wrong name, 409 while an update runs or a client holds the lease, 501 off a station. `GetLastFactoryResetAsync()` gives the last reset's record, or null when there is none.
