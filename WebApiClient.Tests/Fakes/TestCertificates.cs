using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace AccordionQ2.WebApiClient.Tests.Fakes;

/// <summary>Certificates made in memory for the tests: a station's self-signed one, and a CA with a leaf.</summary>
internal static class TestCertificates
{
    private static readonly DateTimeOffset From = DateTimeOffset.UtcNow.AddDays(-1);
    private static readonly DateTimeOffset To = DateTimeOffset.UtcNow.AddDays(30);

    private const string ServerAuth = "1.3.6.1.5.5.7.3.1";
    private const string ClientAuth = "1.3.6.1.5.5.7.3.2";

    /// <summary>A self-signed server certificate for localhost, 127.0.0.1 and ::1, as a station makes its own.</summary>
    public static X509Certificate2 CreateSelfSignedServer() => CreateSelfSignedServer("localhost", loopback: true);

    /// <summary>
    /// A self-signed server certificate for <paramref name="dnsName"/>; for 127.0.0.1 and ::1 too only when
    /// <paramref name="loopback"/>, so without it a client on loopback meets a name mismatch.
    /// </summary>
    public static X509Certificate2 CreateSelfSignedServer(string dnsName, bool loopback)
    {
        using var key = RSA.Create(2048);
        var request = ServerRequest("CN=" + dnsName, key, dnsName, loopback, ServerAuth);
        return Exportable(request.CreateSelfSigned(From, To));
    }

    public static X509Certificate2 CreateCa(string name)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(name, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        return Exportable(request.CreateSelfSigned(From.AddDays(-1), To.AddDays(1)));
    }

    /// <summary>A server certificate for <paramref name="dnsName"/>, 127.0.0.1 and ::1 signed by <paramref name="ca"/>.</summary>
    public static X509Certificate2 CreateLeaf(X509Certificate2 ca, string dnsName) =>
        CreateLeaf(ca, dnsName, loopback: true, clientAuthOnly: false);

    /// <summary>
    /// A certificate for <paramref name="dnsName"/> (and loopback when <paramref name="loopback"/>) signed by
    /// <paramref name="ca"/>; for client authentication only, not TLS servers, when <paramref name="clientAuthOnly"/>.
    /// </summary>
    public static X509Certificate2 CreateLeaf(X509Certificate2 ca, string dnsName, bool loopback, bool clientAuthOnly)
    {
        using var key = RSA.Create(2048);
        var request = ServerRequest("CN=" + dnsName, key, dnsName, loopback, clientAuthOnly ? ClientAuth : ServerAuth);
        var serial = RandomNumberGenerator.GetBytes(12);
        using var signed = request.Create(ca, From, To, serial);
        return Exportable(signed.CopyWithPrivateKey(key));
    }

    private static CertificateRequest ServerRequest(string subject, RSA key, string dnsName, bool loopback, string usage)
    {
        var request = new CertificateRequest(subject, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var san = new SubjectAlternativeNameBuilder();
        san.AddDnsName(dnsName);
        if (loopback)
        {
            san.AddIpAddress(IPAddress.Loopback);
            san.AddIpAddress(IPAddress.IPv6Loopback);
        }
        request.CertificateExtensions.Add(san.Build());
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, false));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new Oid(usage) }, false));
        return request;
    }

    // Windows' TLS (SChannel) can't use the ephemeral key of a certificate made in memory; a PFX round trip fixes that.
    private static X509Certificate2 Exportable(X509Certificate2 cert)
    {
        using (cert)
            return X509CertificateLoader.LoadPkcs12(cert.Export(X509ContentType.Pfx), null);
    }
}
