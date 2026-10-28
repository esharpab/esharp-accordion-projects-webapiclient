using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using AccordionQ2.WebApiClient.Tests.Fakes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AccordionQ2.WebApiClient.Tests;

/// <summary>
/// HTTPS against a local Kestrel server on loopback (127.0.0.1 and ::1, never another host) with a certificate
/// made in the test, as a station's own self-signed one (NET-01) will be.
/// </summary>
[TestClass]
public class PinnedHttpsTests
{
    private const string Key = "acc_pinned_https_test_key";

    private sealed class LocalStation : IAsyncDisposable
    {
        private readonly WebApplication _app;
        public string? ReceivedKey;
        public int Requests;

        private LocalStation(WebApplication app) => _app = app;

        public int Port { get; private set; }

        public static async Task<LocalStation> StartAsync(X509Certificate2 certificate, bool ipv6)
        {
            var builder = WebApplication.CreateSlimBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.ConfigureKestrel(k =>
                k.Listen(ipv6 ? IPAddress.IPv6Loopback : IPAddress.Loopback, 0, l => l.UseHttps(certificate)));
            var app = builder.Build();
            var station = new LocalStation(app);
            app.MapGet("/api/capabilities", (HttpRequest request) =>
            {
                Interlocked.Increment(ref station.Requests);
                station.ReceivedKey = request.Headers["X-Api-Key"];
                return Results.Text(
                    "{\"apiVersion\":7,\"webApiVersion\":\"test\",\"features\":[\"events\",\"security.apikey\"],\"securityMode\":\"On\"}",
                    "application/json");
            });
            await app.StartAsync();
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            station.Port = new Uri(address).Port;
            return station;
        }

        public async ValueTask DisposeAsync()
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }

    private static X509Certificate2 _certificate = null!;
    private static string _pin = null!;

    [ClassInitialize]
    public static void ClassInit(TestContext _)
    {
        _certificate = TestCertificates.CreateSelfSignedServer();
        _pin = AccordionQ2Certificates.Sha256Of(_certificate);
    }

    [ClassCleanup]
    public static void ClassCleanup() => _certificate?.Dispose();

    private static string WithColons(string hex) =>
        string.Join(":", Enumerable.Range(0, hex.Length / 2).Select(i => hex.Substring(i * 2, 2)));

    [TestMethod]
    public async Task MatchingPin_Connects_AndTheKeyArrivesOverTls()
    {
        await using var station = await LocalStation.StartAsync(_certificate, ipv6: false);
        using var client = new AccordionQ2Client($"https://127.0.0.1:{station.Port}",
            new AccordionQ2ClientOptions { ApiKey = Key, CertificateSha256 = _pin });

        var capabilities = await client.Capabilities.GetAsync();

        Assert.AreEqual("On", capabilities.SecurityMode);
        Assert.AreEqual(Key, station.ReceivedKey);
    }

    [TestMethod]
    public async Task MatchingPin_InColonLowerCaseForm_Connects()
    {
        await using var station = await LocalStation.StartAsync(_certificate, ipv6: false);
        using var client = new AccordionQ2Client($"https://localhost:{station.Port}",
            new AccordionQ2ClientOptions { CertificateSha256 = WithColons(_pin).ToLowerInvariant() });

        Assert.AreEqual(7, (await client.Capabilities.GetAsync()).ApiVersion);
    }

    [TestMethod]
    public async Task WrongPin_IsRefused_BeforeAnyRequestOrKeyIsSent()
    {
        await using var station = await LocalStation.StartAsync(_certificate, ipv6: false);
        var wrong = _pin.Substring(0, 63) + (_pin[63] == '0' ? "1" : "0");
        using var client = new AccordionQ2Client($"https://127.0.0.1:{station.Port}",
            new AccordionQ2ClientOptions { ApiKey = Key, CertificateSha256 = wrong });

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => client.Capabilities.GetAsync());
        Assert.DoesNotContain(Key, ex.ToString());
        Assert.AreEqual(0, station.Requests);
        Assert.IsNull(station.ReceivedKey);
    }

    [TestMethod]
    public async Task NoPin_SelfSigned_IsRefused()
    {
        await using var station = await LocalStation.StartAsync(_certificate, ipv6: false);
        using var client = new AccordionQ2Client($"https://127.0.0.1:{station.Port}");

        await Assert.ThrowsAsync<HttpRequestException>(() => client.Capabilities.GetAsync());
        Assert.AreEqual(0, station.Requests);
    }

    [TestMethod]
    public async Task Ipv6Loopback_WithPin_Connects()
    {
        if (!Socket.OSSupportsIPv6)
            Assert.Inconclusive("This machine has no IPv6");
        await using var station = await LocalStation.StartAsync(_certificate, ipv6: true);
        using var client = new AccordionQ2Client($"https://[::1]:{station.Port}/",
            new AccordionQ2ClientOptions { ApiKey = Key, CertificateSha256 = _pin });

        Assert.AreEqual(7, (await client.Capabilities.GetAsync()).ApiVersion);
        Assert.AreEqual(Key, station.ReceivedKey);
    }

    [TestMethod]
    public async Task TrustedCa_Connects_ToACertificateItSigned()
    {
        using var ca = TestCertificates.CreateCa("CN=Accordion test site CA");
        using var leaf = TestCertificates.CreateLeaf(ca, "localhost");
        await using var station = await LocalStation.StartAsync(leaf, ipv6: false);

        using (var client = new AccordionQ2Client($"https://127.0.0.1:{station.Port}",
                   new AccordionQ2ClientOptions { TrustedCaCertificate = ca }))
            Assert.AreEqual(7, (await client.Capabilities.GetAsync()).ApiVersion);

        using var otherCa = TestCertificates.CreateCa("CN=Some other CA");
        using var refused = new AccordionQ2Client($"https://127.0.0.1:{station.Port}",
            new AccordionQ2ClientOptions { TrustedCaCertificate = otherCa });
        await Assert.ThrowsAsync<HttpRequestException>(() => refused.Capabilities.GetAsync());
    }

    [TestMethod]
    public async Task Pin_AcceptsTheCertificate_WhenTheAddressIsNotInIt()
    {
        // A station reached by an IP address its certificate doesn't name: the pin decides, not the name.
        using var certificate = TestCertificates.CreateSelfSignedServer("station-elsewhere.invalid", loopback: false);
        await using var station = await LocalStation.StartAsync(certificate, ipv6: false);

        using (var unpinned = new AccordionQ2Client($"https://127.0.0.1:{station.Port}"))
            await Assert.ThrowsAsync<HttpRequestException>(() => unpinned.Capabilities.GetAsync());

        using var client = new AccordionQ2Client($"https://127.0.0.1:{station.Port}",
            new AccordionQ2ClientOptions { ApiKey = Key, CertificateSha256 = AccordionQ2Certificates.Sha256Of(certificate) });
        Assert.AreEqual(7, (await client.Capabilities.GetAsync()).ApiVersion);
        Assert.AreEqual(Key, station.ReceivedKey);
    }

    [TestMethod]
    public async Task TrustedCa_RefusesItsCertificate_WhenTheNameDoesNotMatch()
    {
        using var ca = TestCertificates.CreateCa("CN=Accordion test site CA");
        using var leaf = TestCertificates.CreateLeaf(ca, "station-elsewhere.invalid", loopback: false, clientAuthOnly: false);
        await using var station = await LocalStation.StartAsync(leaf, ipv6: false);
        using var client = new AccordionQ2Client($"https://127.0.0.1:{station.Port}",
            new AccordionQ2ClientOptions { ApiKey = Key, TrustedCaCertificate = ca });

        await Assert.ThrowsAsync<HttpRequestException>(() => client.Capabilities.GetAsync());
        Assert.AreEqual(0, station.Requests);
        Assert.IsNull(station.ReceivedKey);
    }

    [TestMethod]
    public void TrustedCa_Validate_RequiresServerAuthentication()
    {
        // The callback alone: Kestrel won't serve a certificate without serverAuth, so no TLS test can reach it.
        using var ca = TestCertificates.CreateCa("CN=Accordion test site CA");
        using var server = TestCertificates.CreateLeaf(ca, "localhost", loopback: true, clientAuthOnly: false);
        using var clientOnly = TestCertificates.CreateLeaf(ca, "localhost", loopback: true, clientAuthOnly: true);
        const System.Net.Security.SslPolicyErrors chainErrors = System.Net.Security.SslPolicyErrors.RemoteCertificateChainErrors;

        Assert.IsTrue(AccordionQ2Certificates.Validate(server, chainErrors, null, ca));
        Assert.IsFalse(AccordionQ2Certificates.Validate(clientOnly, chainErrors, null, ca));
    }

    [TestMethod]
    public async Task CreateHandler_WorksForTheCallersOwnHttpClient()
    {
        await using var station = await LocalStation.StartAsync(_certificate, ipv6: false);
        using var http = new HttpClient(AccordionQ2Client.CreateHandler(new AccordionQ2ClientOptions { CertificateSha256 = _pin }));
        using var client = new AccordionQ2Client($"https://127.0.0.1:{station.Port}",
            new AccordionQ2ClientOptions { ApiKey = Key, HttpClient = http });

        Assert.AreEqual(7, (await client.Capabilities.GetAsync()).ApiVersion);
        Assert.AreEqual(Key, station.ReceivedKey);
    }
}
