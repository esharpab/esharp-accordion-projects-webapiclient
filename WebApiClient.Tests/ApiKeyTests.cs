using System.Net;
using System.Text;
using AccordionQ2.WebApiClient.Tests.Fakes;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AccordionQ2.WebApiClient.Tests;

/// <summary>
/// The API key goes out as <c>X-Api-Key</c> on every request, however the client was built, and never into a
/// shared HttpClient's defaults or an exception message. No network: a fake handler answers.
/// </summary>
[TestClass]
public class ApiKeyTests
{
    private const string Key = "acc_0123456789abcdefTESTKEY";
    private const string Url = "http://station:5000";

    private static (AccordionQ2Client Client, RecordingHandler Handler) Create(string? key = Key)
    {
        var handler = new RecordingHandler();
        return (new AccordionQ2Client(Url, new AccordionQ2ClientOptions { ApiKey = key, Handler = handler }), handler);
    }

    [TestMethod]
    public async Task EveryVerb_SendsTheKey()
    {
        var (client, handler) = Create();
        handler.Respond = r => RecordingHandler.Json(HttpStatusCode.OK,
            r.RequestUri!.AbsolutePath == "/api/lease" && r.Method == HttpMethod.Post ? "{\"leaseId\":\"L1\"}" : "{}");
        using (client)
        {
            await client.Capabilities.GetAsync();                              // GET JSON
            await client.Media.DownloadFileAsync("a.png");                     // GET bytes
            await client.Lease.AcquireAsync("tests");                          // POST JSON
            await client.Lease.RenewAsync();                                   // PUT JSON
            await client.Files.UploadAsync("config", "a.txt", new byte[] { 1 }); // PUT bytes
            await client.Firmware.UploadPackageAsync(new byte[] { 2 });       // POST bytes
            await client.Media.UploadFileAsync("a.png", new byte[] { 3 });     // multipart
            await client.Lease.ReleaseAsync();                                 // DELETE
        }

        Assert.AreEqual(8, handler.Requests.Count);
        foreach (var request in handler.Requests)
            Assert.AreEqual(Key, request.Header("X-Api-Key"), $"{request.Method} {request.Uri}");
    }

    [TestMethod]
    public async Task NoKey_SendsNoHeader()
    {
        var (client, handler) = Create(key: null);
        using (client)
            await client.Capabilities.GetAsync();
        Assert.IsNull(handler.Last.Header("X-Api-Key"));
    }

    [TestMethod]
    public async Task EventStream_SendsTheKey()
    {
        var (client, handler) = Create();
        handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("event: hello\ndata: {}\n\n", Encoding.UTF8, "text/event-stream"),
        };
        using var cts = new CancellationTokenSource();
        var events = 0;
        using (client)
            await client.Events.ListenAsync(_ => { events++; cts.Cancel(); return Task.CompletedTask; }, cts.Token);

        Assert.AreEqual(1, events);
        Assert.AreEqual(Key, handler.Last.Header("X-Api-Key"));
        Assert.AreEqual("/api/events", handler.Last.Uri!.AbsolutePath);
    }

    [TestMethod]
    public async Task CallersHttpClient_GetsTheKeyPerRequest_AndIsNotChanged()
    {
        var handler = new RecordingHandler();
        handler.Respond = r => RecordingHandler.Json(HttpStatusCode.OK, r.Method == HttpMethod.Post ? "{\"leaseId\":\"L1\"}" : "{}");
        using var http = new HttpClient(handler);
        using (var client = new AccordionQ2Client(Url, new AccordionQ2ClientOptions { ApiKey = Key, HttpClient = http }))
        {
            await client.Lease.AcquireAsync("tests");
            await client.Capabilities.GetAsync();
        }

        Assert.AreEqual(Key, handler.Last.Header("X-Api-Key"));
        Assert.AreEqual("L1", handler.Last.Header("X-Lease-Id"));
        Assert.AreEqual(0, http.DefaultRequestHeaders.Count(), "neither the key nor the lease id goes into the shared defaults");

        // The caller owns the HttpClient: the client's Dispose leaves it usable.
        await http.GetAsync("http://station:5000/still-usable");
    }

    [TestMethod]
    public async Task OldHttpClientConstructor_StillWorks()
    {
        var handler = new RecordingHandler();
        using var http = new HttpClient(handler);
        using (var client = new AccordionQ2Client(Url, http))
            await client.Capabilities.GetAsync();
        Assert.AreEqual(new Uri("http://station:5000/api/capabilities"), handler.Last.Uri);
        Assert.IsNull(handler.Last.Header("X-Api-Key"));
    }

    [TestMethod]
    [DataRow("acc_abc\r\nX-Evil: 1")]
    [DataRow("acc_abc def")]
    [DataRow("acc_äbc")]
    [DataRow("")]
    public void MalformedKey_IsRefused_WithoutRepeatingIt(string key)
    {
        var ex = Assert.ThrowsExactly<ArgumentException>(() =>
            new AccordionQ2Client(Url, new AccordionQ2ClientOptions { ApiKey = key, Handler = new RecordingHandler() }));
        if (key.Length > 0)
            Assert.DoesNotContain("acc_", ex.ToString());
    }

    [TestMethod]
    public void Options_ToString_HidesTheKey()
    {
        var text = new AccordionQ2ClientOptions { ApiKey = Key }.ToString();
        Assert.DoesNotContain(Key, text);
        Assert.Contains("ApiKey=set", text);
    }

    [TestMethod]
    public void CertificateTrust_WithCallersHttpClientOrHandler_IsRefused()
    {
        const string pin = "AB:CD:EF:01:23:45:67:89:AB:CD:EF:01:23:45:67:89:AB:CD:EF:01:23:45:67:89:AB:CD:EF:01:23:45:67:89";
        using var http = new HttpClient();
        Assert.ThrowsExactly<ArgumentException>(() =>
            new AccordionQ2Client(Url, new AccordionQ2ClientOptions { HttpClient = http, CertificateSha256 = pin }));
        Assert.ThrowsExactly<ArgumentException>(() =>
            new AccordionQ2Client(Url, new AccordionQ2ClientOptions { Handler = new RecordingHandler(), CertificateSha256 = pin }));
    }
}
