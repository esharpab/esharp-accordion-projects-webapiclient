using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace AccordionQ2.WebApiClient;

/// <summary>
/// Certificate checks for a station's HTTPS: a pinned SHA-256 fingerprint or a trusted CA.
/// <see cref="AccordionQ2Client"/> uses them itself; they are public for a caller that builds its own handler.
/// </summary>
public static class AccordionQ2Certificates
{
    /// <summary>
    /// <paramref name="fingerprint"/> as 64 upper-case hex digits. Colons, dashes and white space are
    /// ignored and either case is accepted. Throws <see cref="ArgumentException"/> for anything else.
    /// </summary>
    public static string NormalizeSha256(string fingerprint)
    {
        if (fingerprint is null) throw new ArgumentNullException(nameof(fingerprint));
        var hex = new char[64];
        var n = 0;
        foreach (var c in fingerprint)
        {
            if (c == ':' || c == '-' || char.IsWhiteSpace(c))
                continue;
            var u = char.ToUpperInvariant(c);
            if (!((u >= '0' && u <= '9') || (u >= 'A' && u <= 'F')))
                throw new ArgumentException("A SHA-256 fingerprint is hex digits, optionally separated by colons", nameof(fingerprint));
            if (n == hex.Length)
                throw new ArgumentException("A SHA-256 fingerprint has 64 hex digits; this has more", nameof(fingerprint));
            hex[n++] = u;
        }
        if (n != hex.Length)
            throw new ArgumentException($"A SHA-256 fingerprint has 64 hex digits; this has {n}", nameof(fingerprint));
        return new string(hex);
    }

    /// <summary>The SHA-256 fingerprint of <paramref name="certificate"/>, as 64 upper-case hex digits.</summary>
    public static string Sha256Of(X509Certificate certificate)
    {
        if (certificate is null) throw new ArgumentNullException(nameof(certificate));
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(certificate.GetRawCertData())).Replace("-", string.Empty);
    }

    /// <summary>
    /// Whether to accept a server certificate.
    /// <list type="bullet">
    /// <item>With <paramref name="sha256"/>: exactly when the certificate's fingerprint is that one, whatever
    /// the chain or the name (a station's certificate is self-signed and often reached by IP address).</item>
    /// <item>With <paramref name="trustedCa"/>: when the operating system accepts it, or it chains to that CA,
    /// its name matches and it may serve TLS (no extended key usage, or one with serverAuth).</item>
    /// <item>With neither: when the operating system accepts it (<paramref name="errors"/> is None).</item>
    /// </list>
    /// </summary>
    public static bool Validate(X509Certificate? certificate, SslPolicyErrors errors, string? sha256, X509Certificate2? trustedCa = null)
    {
        if (certificate is null)
            return false;
        if (sha256 is not null)
            return Sha256Of(certificate) == NormalizeSha256(sha256);
        if (errors == SslPolicyErrors.None)
            return true;
        if (trustedCa is null || errors != SslPolicyErrors.RemoteCertificateChainErrors)
            return false; // no CA to try, or the name doesn't match / no certificate

        using var chain = new X509Chain();
        chain.ChainPolicy.ExtraStore.Add(trustedCa);
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;
        chain.ChainPolicy.ApplicationPolicy.Add(new Oid(ServerAuthenticationOid)); // as the OS path requires
        using var leaf = new X509Certificate2(certificate.GetRawCertData());
        if (!chain.Build(leaf) || chain.ChainElements.Count < 2)
            return false;
        var root = chain.ChainElements[chain.ChainElements.Count - 1].Certificate;
        return root.RawData.SequenceEqual(trustedCa.RawData);
    }

    private const string ServerAuthenticationOid = "1.3.6.1.5.5.7.3.1";

    /// <summary>
    /// A callback for <see cref="HttpClientHandler.ServerCertificateCustomValidationCallback"/> that applies
    /// <see cref="Validate"/>. Throws <see cref="ArgumentException"/> for a malformed <paramref name="sha256"/>.
    /// </summary>
    /// <remarks>Needs .NET Framework 4.7.1 or later on .NET Framework.</remarks>
    public static Func<HttpRequestMessage, X509Certificate2, X509Chain, SslPolicyErrors, bool> CreateValidationCallback(
        string? sha256, X509Certificate2? trustedCa = null)
    {
        var pin = sha256 is null ? null : NormalizeSha256(sha256);
        return (_, certificate, _, errors) => Validate(certificate, errors, pin, trustedCa);
    }
}
