using AccordionQ2.WebApiClient.Tests.Fakes;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AccordionQ2.WebApiClient.Tests;

/// <summary>Base URLs, IPv6 literals with zone ids among them, as the requests that go out show. No network.</summary>
[TestClass]
public class BaseUrlTests
{
    private static async Task<Uri> FirstRequestUri(string baseUrl)
    {
        var handler = new RecordingHandler();
        using var client = new AccordionQ2Client(baseUrl, new AccordionQ2ClientOptions { Handler = handler });
        await client.Capabilities.GetAsync();
        return handler.Last.Uri!;
    }

    [TestMethod]
    [DataRow("http://station:5000", "station")]
    [DataRow("http://station:5000/", "station")]
    [DataRow("http://10.0.0.7:5000", "10.0.0.7")]
    [DataRow("http://[::1]:5000", "::1")]
    [DataRow("https://[2001:db8::7]:5001/", "2001:db8::7")]
    [DataRow("http://[fe80::1%eth0]:5000", "fe80::1%eth0")]
    [DataRow("http://[fe80::1%25eth0]:5000", "fe80::1%eth0")] // RFC 6874's form
    [DataRow("http://[fe80::1%12]:5000", "fe80::1%12")]       // Windows: interface index
    // As the Python client: "%25" followed by more is RFC 6874's "%"; "%25" alone is zone 25.
    [DataRow("http://[fe80::1%2512]:5000", "fe80::1%12")]
    [DataRow("http://[fe80::1%2525]:5000", "fe80::1%25")]
    [DataRow("http://[fe80::1%25]:5000", "fe80::1%25")]
    public async Task Request_GoesToTheHost_WithTheZoneId(string baseUrl, string idnHost)
    {
        var uri = await FirstRequestUri(baseUrl);
        Assert.AreEqual(idnHost, uri.IdnHost, "IdnHost is what the socket connects to, zone id included");
        Assert.AreEqual("/api/capabilities", uri.AbsolutePath);
    }

    [TestMethod]
    public async Task PathBase_IsKept()
    {
        var uri = await FirstRequestUri("http://[::1]:5000/accordion/");
        Assert.AreEqual("http://[::1]:5000/accordion/api/capabilities", uri.AbsoluteUri);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("station:5000")]
    [DataRow("ftp://station")]
    [DataRow("http://[fe80::1%eth0:5000")]
    public void Malformed_IsAnArgumentException(string baseUrl) =>
        Assert.ThrowsExactly<ArgumentException>(() => AccordionQ2Client.ParseBaseUrl(baseUrl));

    [TestMethod]
    public void Credentials_InTheUrl_AreRefused_WithoutRepeatingThem()
    {
        var ex = Assert.ThrowsExactly<ArgumentException>(() => AccordionQ2Client.ParseBaseUrl("http://user:acc_secret@station:5000"));
        Assert.DoesNotContain("acc_secret", ex.Message);
    }
}
