using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using AccordionQ2.WebApiClient.Tests.Fakes;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AccordionQ2.WebApiClient.Tests;

/// <summary>
/// A station that slows a client down answers 429 with Retry-After (firmware 7.1.0 in mode On; FW7 SEC-06, CRA-08). The
/// client names it (<see cref="AccordionQ2ThrottledException"/>) and, when asked (<see cref="AccordionQ2ClientOptions.MaxThrottleWait"/>),
/// waits it out and resends a few times (FW7 CLI-04 follow-up). Bodies as the WebApi's SecurityMiddleware writes them.
/// </summary>
[TestClass]
public class ThrottleTests
{
    private const string Url = "http://station:5000";
    private const string Key = "acc_SECRETSECRETSECRET";
    private const string Body429 = "{\"error\":\"Too many failed keys from 192.168.0.58; try again in 2 s\",\"requires\":\"Viewer\"}";

    private static HttpResponseMessage Throttled(TimeSpan? retryAfter, string body = Body429)
    {
        var response = RecordingHandler.Json(HttpStatusCode.TooManyRequests, body);
        if (retryAfter is { } wait)
            response.Headers.RetryAfter = new RetryConditionHeaderValue(wait);
        return response;
    }

    private static (AccordionQ2Client Client, RecordingHandler Handler) Create(Func<int, HttpResponseMessage> respond, TimeSpan maxWait = default)
    {
        var handler = new RecordingHandler();
        handler.Respond = _ => respond(handler.Requests.Count);
        return (new AccordionQ2Client(Url, new AccordionQ2ClientOptions { ApiKey = Key, Handler = handler, MaxThrottleWait = maxWait }), handler);
    }

    [TestMethod]
    public async Task A_429_is_a_throttled_exception_naming_the_slow_down()
    {
        var (client, handler) = Create(_ => Throttled(TimeSpan.FromSeconds(2)));
        using var _ = client;
        var ex = await Assert.ThrowsExactlyAsync<AccordionQ2ThrottledException>(() => client.Capabilities.GetAsync());
        Assert.AreEqual(429, ex.StatusCode);
        Assert.AreEqual(TimeSpan.FromSeconds(2), ex.RetryAfter);
        Assert.AreEqual("Too many failed keys from 192.168.0.58; try again in 2 s", ex.StationMessage);
        Assert.AreEqual("Viewer", ex.RequiredRole);
        Assert.Contains("slowing this client down", ex.Message);
        Assert.Contains("try again in 2 s", ex.Message);
        Assert.IsInstanceOfType<AccordionQ2ApiException>(ex, "callers catching the base class still catch it");
        Assert.HasCount(1, handler.Requests, "no wait unless asked");
    }

    [TestMethod]
    public async Task Retry_After_as_an_HTTP_date_is_read_as_a_wait()
    {
        var (client, _) = Create(_ =>
        {
            var response = Throttled(null);
            response.Headers.RetryAfter = new RetryConditionHeaderValue(DateTimeOffset.UtcNow.AddSeconds(30));
            return response;
        });
        using var _ = client;
        var ex = await Assert.ThrowsExactlyAsync<AccordionQ2ThrottledException>(() => client.Capabilities.GetAsync());
        Assert.IsTrue(ex.RetryAfter is { } wait && wait > TimeSpan.FromSeconds(20) && wait <= TimeSpan.FromSeconds(30), $"{ex.RetryAfter}");
    }

    [TestMethod]
    public void A_date_in_the_past_waits_nothing()
    {
        using var response = Throttled(null);
        var now = DateTimeOffset.UtcNow;
        response.Headers.RetryAfter = new RetryConditionHeaderValue(now.AddSeconds(-5));
        Assert.AreEqual(TimeSpan.Zero, Internal.ApiTransport.RetryAfterOf(response, now));
        response.Headers.RetryAfter = null;
        Assert.IsNull(Internal.ApiTransport.RetryAfterOf(response, now));
    }

    [TestMethod]
    public async Task Without_Retry_After_nothing_is_waited_and_the_wait_is_unknown()
    {
        var (client, handler) = Create(_ => Throttled(null, "{\"error\":\"Too many requests without a key from 10.0.0.9\",\"requires\":\"Anonymous\"}"),
            TimeSpan.FromSeconds(10));
        using var _ = client;
        var ex = await Assert.ThrowsExactlyAsync<AccordionQ2ThrottledException>(() => client.Capabilities.GetAsync());
        Assert.IsNull(ex.RetryAfter);
        Assert.DoesNotContain("try again in", ex.Message.Replace("(station:", "|").Split('|')[0]);
        Assert.HasCount(1, handler.Requests);
    }

    [TestMethod]
    public async Task Off_never_resends_not_even_a_Retry_After_of_zero_or_a_past_date()
    {
        var (client, handler) = Create(_ => Throttled(TimeSpan.Zero));
        using var _ = client;
        await Assert.ThrowsExactlyAsync<AccordionQ2ThrottledException>(() => client.Capabilities.GetAsync());
        Assert.HasCount(1, handler.Requests, "zero is off");

        handler.Respond = _ =>
        {
            var response = Throttled(null);
            response.Headers.RetryAfter = new RetryConditionHeaderValue(DateTimeOffset.UtcNow.AddSeconds(-5));
            return response;
        };
        await Assert.ThrowsExactlyAsync<AccordionQ2ThrottledException>(() => client.Capabilities.GetAsync());
        Assert.HasCount(2, handler.Requests);
    }

    [TestMethod]
    public async Task A_429_that_isnt_a_slow_down_stays_the_stations_error()
    {
        // SubscriptionsController's limit per stream: no Retry-After, no SecurityError; waiting won't lift it
        const string Limit = "A stream can have at most 32 subscriptions";
        var (client, handler) = Create(_ => RecordingHandler.Json(HttpStatusCode.TooManyRequests, "{\"error\":\"" + Limit + "\"}"), TimeSpan.FromSeconds(10));
        using var _ = client;
        var ex = await Assert.ThrowsExactlyAsync<AccordionQ2ApiException>(() => client.Subscriptions.CreateAsync("s1", ["Mod.Ch"], 100));
        Assert.AreEqual(429, ex.StatusCode);
        Assert.AreEqual(Limit, ex.Message);
        Assert.HasCount(1, handler.Requests);
    }

    [TestMethod]
    public async Task With_MaxThrottleWait_the_request_is_resent_with_the_same_body()
    {
        var (client, handler) = Create(n => n == 1 ? Throttled(TimeSpan.Zero) : RecordingHandler.Json(HttpStatusCode.OK, "{\"leaseId\":\"l1\",\"holder\":\"t\"}"),
            TimeSpan.FromSeconds(5));
        using var _ = client;
        await client.Lease.AcquireAsync("tests");
        Assert.HasCount(2, handler.Requests);
        CollectionAssert.AreEqual(handler.Requests[0].Body, handler.Requests[1].Body);
        Assert.IsNotEmpty(handler.Requests[1].Body);
        Assert.AreEqual(Key, handler.Requests[1].Header("X-Api-Key"));
    }

    [TestMethod]
    public async Task A_multipart_upload_is_resent_whole()
    {
        var (client, handler) = Create(n => n == 1 ? Throttled(TimeSpan.Zero) : RecordingHandler.Json(HttpStatusCode.OK, "{}"), TimeSpan.FromSeconds(5));
        using var _ = client;
        await client.Media.UploadFileAsync("a.png", [1, 2, 3]);
        Assert.HasCount(2, handler.Requests);
        Assert.AreEqual(handler.Requests[0].Body.Length, handler.Requests[1].Body.Length);
    }

    [TestMethod]
    public async Task A_longer_Retry_After_than_allowed_throws_at_once()
    {
        var (client, handler) = Create(_ => Throttled(TimeSpan.FromSeconds(60)), TimeSpan.FromSeconds(5));
        using var _ = client;
        var ex = await Assert.ThrowsExactlyAsync<AccordionQ2ThrottledException>(() => client.Capabilities.GetAsync());
        Assert.AreEqual(TimeSpan.FromSeconds(60), ex.RetryAfter);
        Assert.HasCount(1, handler.Requests);
    }

    [TestMethod]
    public async Task It_resends_at_most_three_times()
    {
        var (client, handler) = Create(_ => Throttled(TimeSpan.Zero), TimeSpan.FromSeconds(5));
        using var _ = client;
        await Assert.ThrowsExactlyAsync<AccordionQ2ThrottledException>(() => client.Capabilities.GetAsync());
        Assert.HasCount(1 + Internal.ApiTransport.MaxThrottleRetries, handler.Requests);
    }

    [TestMethod]
    public async Task A_stream_upload_is_never_resent()
    {
        var (client, handler) = Create(_ => Throttled(TimeSpan.Zero), TimeSpan.FromSeconds(5));
        using var _ = client;
        using var stream = new MemoryStream([1, 2, 3]);
        await Assert.ThrowsExactlyAsync<AccordionQ2ThrottledException>(() => client.Channels.UploadByteStreamAsync("Mod.Ch", stream, "a.bin"));
        Assert.HasCount(1, handler.Requests);
    }

    [TestMethod]
    public async Task The_event_streams_opening_names_the_slow_down_and_waits_when_asked()
    {
        var (plain, _) = Create(_ => Throttled(TimeSpan.FromSeconds(3)));
        using (plain)
        {
            var ex = await Assert.ThrowsExactlyAsync<AccordionQ2ThrottledException>(() => plain.Events.ListenAsync(_ => Task.CompletedTask));
            Assert.AreEqual(TimeSpan.FromSeconds(3), ex.RetryAfter, "what a reconnect loop waits");
        }

        var (waiting, handler) = Create(n => n == 1 ? Throttled(TimeSpan.Zero) : RecordingHandler.Json(HttpStatusCode.NotFound, ""), TimeSpan.FromSeconds(5));
        using (waiting)
        {
            var ex = await Assert.ThrowsExactlyAsync<AccordionQ2ApiException>(() => waiting.Events.ListenAsync(_ => Task.CompletedTask));
            Assert.AreEqual(404, ex.StatusCode, "opened again after the wait");
            Assert.HasCount(2, handler.Requests);
            Assert.AreEqual("text/event-stream", handler.Requests[1].Header("Accept"));
        }
    }

    [TestMethod]
    public async Task Station_text_with_the_key_is_withheld()
    {
        var (client, _) = Create(_ => Throttled(TimeSpan.FromSeconds(1), "{\"error\":\"key " + Key + " blocked\",\"requires\":\"Viewer\"}"));
        using var _ = client;
        var ex = await Assert.ThrowsExactlyAsync<AccordionQ2ThrottledException>(() => client.Capabilities.GetAsync());
        Assert.DoesNotContain(Key, ex.ToString());
        Assert.DoesNotContain(Key, ex.StationMessage!);
    }

    [TestMethod]
    public void A_negative_MaxThrottleWait_is_refused()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new AccordionQ2Client(Url, new AccordionQ2ClientOptions { Handler = new RecordingHandler(), MaxThrottleWait = TimeSpan.FromSeconds(-1) }));
    }

    [TestMethod]
    [TestCategory("Slow")] // waits a real Retry-After of 1 s
    public async Task The_wait_is_the_Retry_After()
    {
        var (client, handler) = Create(n => n == 1 ? Throttled(TimeSpan.FromSeconds(1)) : RecordingHandler.Json(HttpStatusCode.OK, "{\"apiVersion\":7}"),
            TimeSpan.FromSeconds(2));
        using var _ = client;
        var clock = Stopwatch.StartNew();
        await client.Capabilities.GetAsync();
        Assert.IsGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(900), clock.Elapsed);
        Assert.HasCount(2, handler.Requests);
    }

    [TestMethod]
    public async Task Cancelling_ends_the_wait()
    {
        var (client, _) = Create(_ => Throttled(TimeSpan.FromSeconds(30)), TimeSpan.FromMinutes(1));
        using var _ = client;
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        var call = client.Capabilities.GetAsync(cts.Token);
        Assert.AreSame(call, await Task.WhenAny(call, Task.Delay(TimeSpan.FromSeconds(10))));
        await Assert.ThrowsAsync<OperationCanceledException>(() => call);
    }
}
