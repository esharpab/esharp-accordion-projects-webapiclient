using System.Security.Cryptography.X509Certificates;

namespace AccordionQ2.WebApiClient;

/// <summary>
/// How an <see cref="AccordionQ2Client"/> reaches and authenticates to a station: its API key and,
/// for HTTPS, which certificate to trust.
/// </summary>
/// <remarks>
/// The WebApi is meant for intranets. A station's own HTTPS certificate is self-signed; trust it by its
/// SHA-256 fingerprint (<see cref="CertificateSha256"/>), or by the site CA that signed it
/// (<see cref="TrustedCaCertificate"/>). With neither, the operating system validates as usual.
/// </remarks>
public sealed class AccordionQ2ClientOptions
{
    /// <summary>
    /// The station API key (<c>acc_…</c>), sent as <c>X-Api-Key</c> on every request, the event stream
    /// included. Null sends none, which a station in security mode <c>Off</c> or <c>Report</c> accepts.
    /// </summary>
    /// <remarks>
    /// The client never logs the key or puts it in an exception message. Over <c>http://</c> the key crosses
    /// the network in clear text: acceptable only on a trusted intranet segment. Use <c>https://</c> with
    /// <see cref="CertificateSha256"/> once the station offers HTTPS.
    /// </remarks>
    public string? ApiKey { get; set; }

    /// <summary>
    /// The SHA-256 fingerprint of the station's HTTPS certificate, as hex with or without colons, spaces
    /// or dashes, in either case (e.g. <c>AB:CD:…</c> as the Web UI or <c>openssl x509 -fingerprint -sha256</c>
    /// shows it). When set, the client accepts exactly that certificate, whether self-signed or reached by an
    /// IP address, and refuses any other.
    /// </summary>
    public string? CertificateSha256 { get; set; }

    /// <summary>
    /// A CA to trust for the station's HTTPS certificate besides the operating system's, e.g. the site's
    /// own CA. The certificate must chain to it and match the host name. Ignored when
    /// <see cref="CertificateSha256"/> is set.
    /// </summary>
    public X509Certificate2? TrustedCaCertificate { get; set; }

    /// <summary>
    /// An <see cref="System.Net.Http.HttpClient"/> to use, e.g. from <c>IHttpClientFactory</c>; the caller
    /// owns it. Its handler is the caller's, so it can't be combined with <see cref="CertificateSha256"/> or
    /// <see cref="TrustedCaCertificate"/>: build that handler with <see cref="AccordionQ2Client.CreateHandler"/>.
    /// The client leaves it unchanged (it sets neither <c>BaseAddress</c> nor default headers). Its handler
    /// should not follow redirects (<c>AllowAutoRedirect = false</c>), or a redirect carries the key to another host.
    /// </summary>
    public HttpClient? HttpClient { get; set; }

    /// <summary>
    /// An <see cref="HttpMessageHandler"/> to send through (e.g. one with a proxy or logging); the caller
    /// owns it. Like <see cref="HttpClient"/>, it can't be combined with the certificate options, and it
    /// should not follow redirects.
    /// </summary>
    public HttpMessageHandler? Handler { get; set; }

    /// <summary>Describes the options without the API key.</summary>
    public override string ToString() =>
        $"ApiKey={(ApiKey is null ? "none" : "set")}, CertificateSha256={CertificateSha256 ?? "none"}, " +
        $"TrustedCaCertificate={(TrustedCaCertificate is null ? "none" : TrustedCaCertificate.Subject)}, " +
        $"HttpClient={(HttpClient is null ? "own" : "caller's")}, Handler={(Handler is null ? "own" : "caller's")}";
}
