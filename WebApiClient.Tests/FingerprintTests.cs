using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using AccordionQ2.WebApiClient.Tests.Fakes;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AccordionQ2.WebApiClient.Tests;

/// <summary>Fingerprint formats and the validation rule, with in-memory certificates. No network.</summary>
[TestClass]
public class FingerprintTests
{
    private static X509Certificate2 SelfSigned(string name = "CN=station-12")
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest(name, key, HashAlgorithmName.SHA256);
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));
    }

    [TestMethod]
    public void Sha256Of_MatchesTheFrameworksHash()
    {
        using var cert = SelfSigned();
        Assert.AreEqual(cert.GetCertHashString(HashAlgorithmName.SHA256), AccordionQ2Certificates.Sha256Of(cert));
    }

    [TestMethod]
    public void NormalizeSha256_AcceptsEveryCommonFormat()
    {
        using var cert = SelfSigned();
        var hex = AccordionQ2Certificates.Sha256Of(cert);
        var colons = string.Join(":", Enumerable.Range(0, 32).Select(i => hex.Substring(i * 2, 2)));
        foreach (var form in new[] { hex, hex.ToLowerInvariant(), colons, colons.ToLowerInvariant(),
                                     colons.Replace(':', ' '), colons.Replace(':', '-'), " " + colons + "\n" })
            Assert.AreEqual(hex, AccordionQ2Certificates.NormalizeSha256(form), form);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("AB:CD")]
    [DataRow("0000000000000000000000000000000000000000000000000000000000000000FF")]
    [DataRow("G000000000000000000000000000000000000000000000000000000000000000")]
    [DataRow("sha256:0000000000000000000000000000000000000000000000000000000000000000")]
    public void NormalizeSha256_RefusesTheRest(string fingerprint) =>
        Assert.ThrowsExactly<ArgumentException>(() => AccordionQ2Certificates.NormalizeSha256(fingerprint));

    [TestMethod]
    public void Pin_AcceptsOnlyItsCertificate_WhateverTheChainOrName()
    {
        using var station = SelfSigned();
        using var other = SelfSigned();
        var pin = AccordionQ2Certificates.Sha256Of(station).ToLowerInvariant();
        const SslPolicyErrors selfSignedByIp = SslPolicyErrors.RemoteCertificateChainErrors | SslPolicyErrors.RemoteCertificateNameMismatch;

        Assert.IsTrue(AccordionQ2Certificates.Validate(station, selfSignedByIp, pin));
        Assert.IsFalse(AccordionQ2Certificates.Validate(other, selfSignedByIp, pin));
        Assert.IsFalse(AccordionQ2Certificates.Validate(other, SslPolicyErrors.None, pin), "a pin is strict: a publicly valid certificate isn't enough");
        Assert.IsFalse(AccordionQ2Certificates.Validate(null, SslPolicyErrors.RemoteCertificateNotAvailable, pin));
    }

    [TestMethod]
    public void NoPin_IsNormalValidation()
    {
        using var cert = SelfSigned();
        Assert.IsTrue(AccordionQ2Certificates.Validate(cert, SslPolicyErrors.None, null));
        Assert.IsFalse(AccordionQ2Certificates.Validate(cert, SslPolicyErrors.RemoteCertificateChainErrors, null));
        Assert.IsFalse(AccordionQ2Certificates.Validate(cert, SslPolicyErrors.RemoteCertificateNameMismatch, null));
    }

    [TestMethod]
    public void TrustedCa_AcceptsWhatItSigned_AndNothingElse()
    {
        using var ca = TestCertificates.CreateCa("CN=Site CA");
        using var otherCa = TestCertificates.CreateCa("CN=Other CA");
        using var leaf = TestCertificates.CreateLeaf(ca, "station-12");
        using var stray = TestCertificates.CreateLeaf(otherCa, "station-12");

        Assert.IsTrue(AccordionQ2Certificates.Validate(leaf, SslPolicyErrors.RemoteCertificateChainErrors, null, ca));
        Assert.IsFalse(AccordionQ2Certificates.Validate(stray, SslPolicyErrors.RemoteCertificateChainErrors, null, ca));
        Assert.IsFalse(AccordionQ2Certificates.Validate(leaf,
            SslPolicyErrors.RemoteCertificateChainErrors | SslPolicyErrors.RemoteCertificateNameMismatch, null, ca),
            "a CA doesn't excuse a wrong name");
        Assert.IsFalse(AccordionQ2Certificates.Validate(ca, SslPolicyErrors.RemoteCertificateChainErrors, null, ca),
            "the CA itself isn't a server certificate it signed");
    }

    private const SslPolicyErrors ChainErrors = SslPolicyErrors.RemoteCertificateChainErrors;
    private const string ClientAuth = "1.3.6.1.5.5.7.3.2";

    /// <summary>A chain as .NET on Linux reports the server's extra certificates: in its ExtraStore.</summary>
    private static X509Chain PresentedInExtraStore(params X509Certificate2[] sent)
    {
        var presented = new X509Chain();
        presented.ChainPolicy.ExtraStore.AddRange(sent);
        return presented;
    }

    [TestMethod]
    public void TrustedCa_AcceptsALeafUnderAnIntermediate_FromThePresentedChainsExtraStore()
    {
        using var root = TestCertificates.CreateCa("CN=Site root");
        using var intermediate = TestCertificates.CreateIntermediateCa(root, "CN=Site issuing CA");
        using var leaf = TestCertificates.CreateLeaf(intermediate, "station-12");
        using var presented = PresentedInExtraStore(intermediate);

        Assert.IsTrue(AccordionQ2Certificates.Validate(leaf, presented, ChainErrors, null, root));
    }

    [TestMethod]
    public void TrustedCa_AcceptsALeafUnderAnIntermediate_FromThePresentedChainsElements()
    {
        // As Windows tends to report it: a chain built over the leaf, so ChainElements holds the intermediate.
        using var root = TestCertificates.CreateCa("CN=Site root");
        using var intermediate = TestCertificates.CreateIntermediateCa(root, "CN=Site issuing CA");
        using var leaf = TestCertificates.CreateLeaf(intermediate, "station-12");
        using var built = new X509Chain();
        built.ChainPolicy.ExtraStore.Add(intermediate);
        built.ChainPolicy.ExtraStore.Add(root);
        built.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        built.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;
        built.Build(leaf);
        Assert.IsGreaterThanOrEqualTo(2, built.ChainElements.Count, "the test's chain must hold the intermediate");
        Assert.AreEqual(intermediate.Thumbprint, built.ChainElements[1].Certificate.Thumbprint);

        Assert.IsTrue(AccordionQ2Certificates.Validate(leaf, built, ChainErrors, null, root));
    }

    [TestMethod]
    public void TrustedCa_RefusesALeafUnderAnIntermediate_WithoutThePresentedChain()
    {
        using var root = TestCertificates.CreateCa("CN=Site root");
        using var intermediate = TestCertificates.CreateIntermediateCa(root, "CN=Site issuing CA");
        using var leaf = TestCertificates.CreateLeaf(intermediate, "station-12");

        Assert.IsFalse(AccordionQ2Certificates.Validate(leaf, ChainErrors, null, root));
        Assert.IsFalse(AccordionQ2Certificates.Validate(leaf, null, ChainErrors, null, root));
    }

    [TestMethod]
    public void TrustedCa_RefusesAnIntermediateFromAnotherRoot()
    {
        using var root = TestCertificates.CreateCa("CN=Site root");
        using var otherCa = TestCertificates.CreateCa("CN=Other root");
        using var intermediate = TestCertificates.CreateIntermediateCa(otherCa, "CN=Other issuing CA");
        using var leaf = TestCertificates.CreateLeaf(intermediate, "station-12");
        using var presented = PresentedInExtraStore(intermediate);
        using var presentedWithItsRoot = PresentedInExtraStore(intermediate, otherCa);

        Assert.IsFalse(AccordionQ2Certificates.Validate(leaf, presented, ChainErrors, null, root));
        Assert.IsFalse(AccordionQ2Certificates.Validate(leaf, presentedWithItsRoot, ChainErrors, null, root),
            "a root the station sends is never trusted by itself");
    }

    [TestMethod]
    public void TrustedCa_RefusesWhenTheIntermediateMayNotSign()
    {
        using var root = TestCertificates.CreateCa("CN=Site root");
        using var intermediate = TestCertificates.CreateIntermediateCa(root, "CN=Site issuing CA", keyCertSign: false);
        using var leaf = TestCertificates.CreateLeaf(intermediate, "station-12");
        using var presented = PresentedInExtraStore(intermediate);

        Assert.IsFalse(AccordionQ2Certificates.Validate(leaf, presented, ChainErrors, null, root));
    }

    [TestMethod]
    public void TrustedCa_ThroughAnIntermediate_StillRefusesANameMismatchAndClientAuthOnly()
    {
        using var root = TestCertificates.CreateCa("CN=Site root");
        using var intermediate = TestCertificates.CreateIntermediateCa(root, "CN=Site issuing CA");
        using var leaf = TestCertificates.CreateLeaf(intermediate, "station-12");
        using var clientOnly = TestCertificates.CreateLeaf(intermediate, "station-12", loopback: true, clientAuthOnly: true);
        using var presented = PresentedInExtraStore(intermediate);

        Assert.IsFalse(AccordionQ2Certificates.Validate(leaf, presented,
            ChainErrors | SslPolicyErrors.RemoteCertificateNameMismatch, null, root), "a CA doesn't excuse a wrong name");
        Assert.IsFalse(AccordionQ2Certificates.Validate(clientOnly, presented, ChainErrors, null, root));
    }

    [TestMethod]
    public void TrustedCa_RefusesAnIntermediateLimitedToClientAuth()
    {
        // The chain engine intersects the EKUs down the chain: serverAuth on the leaf isn't enough.
        using var root = TestCertificates.CreateCa("CN=Site root");
        using var intermediate = TestCertificates.CreateIntermediateCa(root, "CN=Site client CA", ekuOid: ClientAuth);
        using var leaf = TestCertificates.CreateLeaf(intermediate, "station-12");
        using var presented = PresentedInExtraStore(intermediate);

        Assert.IsFalse(AccordionQ2Certificates.Validate(leaf, presented, ChainErrors, null, root));
    }

    [TestMethod]
    public void Pin_IgnoresThePresentedChain()
    {
        using var root = TestCertificates.CreateCa("CN=Site root");
        using var intermediate = TestCertificates.CreateIntermediateCa(root, "CN=Site issuing CA");
        using var leaf = TestCertificates.CreateLeaf(intermediate, "station-12");
        using var other = SelfSigned();
        using var presented = PresentedInExtraStore(intermediate);

        Assert.IsTrue(AccordionQ2Certificates.Validate(leaf, presented, ChainErrors, AccordionQ2Certificates.Sha256Of(leaf), root));
        Assert.IsFalse(AccordionQ2Certificates.Validate(leaf, presented, ChainErrors, AccordionQ2Certificates.Sha256Of(other), root),
            "with a pin the CA and the chain don't matter");
    }

    [TestMethod]
    public void CreateValidationCallback_RefusesAMalformedPinUpFront() =>
        Assert.ThrowsExactly<ArgumentException>(() => AccordionQ2Certificates.CreateValidationCallback("not-a-pin"));
}
