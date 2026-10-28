# Changelog

All notable changes to AccordionQ2.WebApiClient. Format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/); versions follow [Semantic Versioning](https://semver.org/).

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
