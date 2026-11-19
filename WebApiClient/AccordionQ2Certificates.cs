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
    /// <remarks>
    /// This overload has no chain from the server, so in CA mode it accepts only a certificate issued
    /// directly by <paramref name="trustedCa"/>. Use the overload that takes the callback's chain to accept one
    /// issued by an intermediate the station sends.
    /// </remarks>
    public static bool Validate(X509Certificate? certificate, SslPolicyErrors errors, string? sha256, X509Certificate2? trustedCa = null) =>
        Validate(certificate, null, errors, sha256, trustedCa);

    /// <summary>
    /// Whether to accept a server certificate, as the other overload, but in CA mode it also uses the
    /// intermediates the station sends.
    /// </summary>
    /// <param name="certificate">The server's certificate.</param>
    /// <param name="presented">
    /// The chain the TLS stack built from what the server sent (the callback's <see cref="X509Chain"/>); may be null.
    /// Its certificates, apart from the leaf, are used as untrusted intermediates. They're never trusted by
    /// themselves: the chain must still end at <paramref name="trustedCa"/>.
    /// </param>
    /// <param name="errors">What the operating system found.</param>
    /// <param name="sha256">A pinned SHA-256 fingerprint, or null.</param>
    /// <param name="trustedCa">A CA to trust besides the operating system's, or null.</param>
    public static bool Validate(X509Certificate? certificate, X509Chain? presented, SslPolicyErrors errors, string? sha256, X509Certificate2? trustedCa = null)
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
        if (presented is not null)
        {
            // Untrusted helpers only: the root must still be trustedCa (below). Windows tends to report the
            // peer's certificates in ChainElements, .NET on Linux in ChainPolicy.ExtraStore; read both.
            // Not disposed: they belong to the caller's chain.
            for (var i = 1; i < presented.ChainElements.Count; i++)
                chain.ChainPolicy.ExtraStore.Add(presented.ChainElements[i].Certificate);
            foreach (var extra in presented.ChainPolicy.ExtraStore)
                chain.ChainPolicy.ExtraStore.Add(extra);
        }
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
    /// <see cref="Validate(X509Certificate, X509Chain, SslPolicyErrors, string, X509Certificate2)"/> with the chain the
    /// TLS stack built, so a CA's intermediates the station sends are used. Throws <see cref="ArgumentException"/> for a malformed <paramref name="sha256"/>.
    /// </summary>
    /// <remarks>Needs .NET Framework 4.7.1 or later on .NET Framework.</remarks>
    public static Func<HttpRequestMessage, X509Certificate2, X509Chain, SslPolicyErrors, bool> CreateValidationCallback(
        string? sha256, X509Certificate2? trustedCa = null)
    {
        var pin = sha256 is null ? null : NormalizeSha256(sha256);
        return (_, certificate, chain, errors) => Validate(certificate, chain, errors, pin, trustedCa);
    }
}
