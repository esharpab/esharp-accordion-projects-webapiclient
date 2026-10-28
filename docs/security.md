# Security: API keys and HTTPS

A station is meant for an intranet: the lab or factory network, not the internet. On that network it can ask every client for an **API key**, and serve **HTTPS** with its own self-signed certificate. This page covers both from the client's side. Both need version 7.0 or later of this library.

## Security modes

The station's security mode decides what a request without a valid key gets. `GET /api/capabilities` answers without a key and reports the mode:

| `SecurityMode` | What the station does |
|----------------|-----------------------|
| `Off` | Checks nothing. A WebApi that predates the modes reports `Off` too |
| `Report` | Lets every request through and logs what `On` would refuse. Use it to find clients that don't send a key yet |
| `On` | Refuses a request without a valid key (401) or with too low a role (403) |

```csharp
var caps = await client.Capabilities.GetAsync();
if (caps.Has(CapabilitiesDto.SecurityApiKey) && caps.SecurityMode != "Off")
    Console.WriteLine($"This station checks API keys ({caps.SecurityMode})");
```

## API keys

The station's administrator makes a key for each machine (a test station, a script) with a name and a role. The key starts with `acc_` and is shown once. Pass it in the options:

```csharp
using var client = new AccordionQ2Client("http://station-12:5000", new AccordionQ2ClientOptions
{
    ApiKey = Environment.GetEnvironmentVariable("ACCORDION_API_KEY"),
});
```

The client sends it as the `X-Api-Key` header on every request, the [event stream](api/events.md) included. It adds the header, and the station's address, to each request rather than to the `HttpClient`: it sets neither the `HttpClient`'s default headers nor its `BaseAddress`. So it works the same with your own `HttpClient` or handler, leaves a shared `HttpClient` unchanged, and one `HttpClient` can serve clients for several stations.

> **Over `http://` the key crosses the network in clear text.** Anyone who can watch the traffic between the client and the station can read it and use it. That is acceptable only on a trusted intranet segment. Once the station offers HTTPS, use `https://` with a [pinned certificate](#https-with-a-pinned-certificate).

### Redirects

The client doesn't follow HTTP redirects. .NET removes only the `Authorization` header when it follows one, so `X-Api-Key` and `X-Lease-Id` would go to whatever host the `Location` header names. The station's API never redirects, so a 3xx answer throws an `AccordionQ2ApiException` with that status, usually because the base URL is wrong.

The client's own handler, and one from `AccordionQ2Client.CreateHandler`, has `AllowAutoRedirect = false`. If you supply your own `HttpClient` or `Handler`, turn redirects off in it yourself:

```csharp
var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
```

Keep the key out of source code: read it from an environment variable or a secret store. The client never logs it or puts it in an exception message. `AccordionQ2ClientOptions.ToString()` shows only `ApiKey=set`. A key with characters a header can't carry (spaces, line breaks, non-ASCII) throws an `ArgumentException` in the constructor, and the message doesn't contain the key.

### Roles

Each role may do what the roles below it may:

| Role | May |
|------|-----|
| `Viewer` | Read: values, channels, state, the event stream and subscriptions |
| `Operator` | Run tests: write values, configure channels, alias files, the lease, media, acquisitions |
| `Admin` | The station itself: firmware, system, boot.config, files, extensions, bus transactions, logs, keys |

The station decides which role each call needs; `GET /api/capabilities` needs none. The client reports a refusal as an [`AccordionQ2SecurityException`](error-handling.md#security-errors-401-and-403).

## HTTPS with a pinned certificate

A station's HTTPS certificate is self-signed, and you often reach the station by IP address, so normal validation would refuse it. Pin it instead, by its SHA-256 fingerprint:

```csharp
using var client = new AccordionQ2Client("https://station-12:5001", new AccordionQ2ClientOptions
{
    ApiKey            = apiKey,
    CertificateSha256 = "3F:A2:7C:...:9C",
});
```

With a pin, the client accepts exactly that certificate, whatever its chain or name, and refuses every other one, publicly valid ones included. Its validity dates aren't checked either: a station's own certificate stays pinned until it is replaced, even past its expiry date (a pin checks identity, and a station's clock may be wrong). The Python client does the same. The request then fails with an `HttpRequestException` whose inner exception is an `AuthenticationException`. The key isn't sent: the TLS handshake fails before any request goes out.

The fingerprint may be written in any of the usual forms: with or without colons, spaces or dashes, in upper or lower case. For example, `AB:CD:...` as `openssl x509 -noout -fingerprint -sha256 -in station.crt` prints it, or `abcd...`. Anything other than 64 hex digits throws an `ArgumentException`. Get the fingerprint from the station's administrator over a channel you trust, not from the station over the network you are trying to secure.

When a station gets a new certificate, its fingerprint changes and the old pin stops working. That is the point of pinning: update the pin.

### A site CA instead

If your site signs station certificates with its own CA, trust that CA instead of pinning each station:

```csharp
var siteCa = X509CertificateLoader.LoadCertificateFromFile("site-ca.crt"); // .NET 9+; new X509Certificate2(path) before
using var client = new AccordionQ2Client("https://station-12.lab.example:5001", new AccordionQ2ClientOptions
{
    TrustedCaCertificate = siteCa,
});
```

The certificate must chain to that CA, match the host name in the URL, and be meant for TLS servers: a certificate with an extended key usage that doesn't include server authentication (for example one for client authentication or code signing) is refused. Certificates the operating system trusts are accepted too. When `CertificateSha256` is set, `TrustedCaCertificate` is ignored. Installing the CA in the operating system's trust store works as well and needs no option.

### With your own HttpClient or handler

A pin or CA lives in the handler, which a supplied `HttpClient` already has. Giving `CertificateSha256` or `TrustedCaCertificate` together with `HttpClient` or `Handler` therefore throws an `ArgumentException`, rather than silently not checking. Build the handler with `AccordionQ2Client.CreateHandler`, which also turns redirects off, and pass the key in the options:

```csharp
var trust = new AccordionQ2ClientOptions { CertificateSha256 = pin };
builder.Services.AddHttpClient("accordion")
    .ConfigurePrimaryHttpMessageHandler(() => AccordionQ2Client.CreateHandler(trust));

// later
var http   = factory.CreateClient("accordion");
var client = new AccordionQ2Client("https://station-12:5001",
    new AccordionQ2ClientOptions { ApiKey = apiKey, HttpClient = http });
```

`AccordionQ2Certificates.CreateValidationCallback(pin)` and `AccordionQ2Certificates.Validate(...)` apply the same rule to a handler you configure yourself, e.g. a `SocketsHttpHandler`'s `SslOptions.RemoteCertificateValidationCallback`.

On .NET Framework, certificate options need 4.7.1 or later. API keys work on 4.6.1.

## IPv6 addresses

Write an IPv6 address in brackets. A link-local address needs its zone: the interface name on Linux, or its index on Windows. Write it as `%eth0`, or as `%25eth0`, the form RFC 6874 gives for URLs:

```csharp
new AccordionQ2Client("http://[::1]:5000");
new AccordionQ2Client("https://[2001:db8::12]:5001", options);
new AccordionQ2Client("http://[fe80::1%eth0]:5000");     // Linux
new AccordionQ2Client("http://[fe80::1%25eth12]:5000");  // RFC 6874 form
new AccordionQ2Client("http://[fe80::1%12]:5000");       // Windows: interface index
```

A zone that starts with `25` followed by more is read as the RFC 6874 form, as in the Python client: `%2512` is zone `12`. So write Windows interface index 251 as `%25251`; `%25` on its own is index 25.

A base URL that isn't an absolute `http://` or `https://` URL throws an `ArgumentException`. So does one with a user name or password in it: send the key with `ApiKey` instead.
