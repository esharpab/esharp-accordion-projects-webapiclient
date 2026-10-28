using System.Net;
using System.Text;
using AccordionQ2.WebApiClient.Models;
using AccordionQ2.WebApiClient.Tests.Fakes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace AccordionQ2.WebApiClient.Tests;

/// <summary>
/// <see cref="Groups.ResourcesGroup.ReadCachedValuesAsync"/> (accordionq2 contract section 5.2, <c>cacheOnly</c>).
/// No network: a fake handler answers.
/// </summary>
[TestClass]
public class CacheOnlyReadTests
{
    private const string Url = "http://station:5000";
    private const string WithFeature = "{\"apiVersion\":1,\"features\":[\"events\",\"values.cacheOnly\"]}";
    private const string WithoutFeature = "{\"apiVersion\":1,\"features\":[\"events\"]}";

    private static (AccordionQ2Client Client, RecordingHandler Handler) Create(string capabilities, string values)
    {
        var handler = new RecordingHandler
        {
            Respond = r => RecordingHandler.Json(HttpStatusCode.OK,
                r.RequestUri!.AbsolutePath == "/api/capabilities" ? capabilities : values),
        };
        return (new AccordionQ2Client(Url, new AccordionQ2ClientOptions { Handler = handler }), handler);
    }

    private static JObject Body(RecordingHandler.Recorded request) => JObject.Parse(Encoding.UTF8.GetString(request.Body));

    [TestMethod]
    public async Task Sends_cacheOnly_and_returns_values_ages_and_the_names_not_cached()
    {
        var (client, handler) = Create(WithFeature,
            "{\"resources\":{\"VMON1\":\"10.962\"},\"ageMs\":{\"VMON1\":320},\"notCached\":[\"PEAK1\",\"UART\"]}");
        using (client)
        {
            var r = await client.Resources.ReadCachedValuesAsync(new[] { "VMON1", "PEAK1", "UART" }, maxAgeMs: 5000);

            Assert.AreEqual("10.962", r.Resources["VMON1"]);
            Assert.AreEqual(320, r.AgeMs["VMON1"]);
            CollectionAssert.AreEqual(new[] { "PEAK1", "UART" }, r.NotCached);
            Assert.IsFalse(r.Resources.ContainsKey("PEAK1"), "a not-cached name has no value");
        }

        var post = handler.Last;
        Assert.AreEqual(new Uri("http://station:5000/api/resources/values/get"), post.Uri);
        var body = Body(post);
        Assert.IsTrue(body.GetValue("cacheOnly", StringComparison.OrdinalIgnoreCase)!.Value<bool>());
        Assert.AreEqual(5000, body.GetValue("maxAgeMs", StringComparison.OrdinalIgnoreCase)!.Value<int>());
        CollectionAssert.AreEqual(new[] { "VMON1", "PEAK1", "UART" },
            body.GetValue("names", StringComparison.OrdinalIgnoreCase)!.Values<string>().ToArray());
    }

    [TestMethod]
    public async Task The_capabilities_are_asked_once()
    {
        var (client, handler) = Create(WithFeature, "{\"resources\":{},\"ageMs\":{},\"notCached\":[\"A\"]}");
        using (client)
        {
            await client.Resources.ReadCachedValuesAsync(new[] { "A" });
            await client.Resources.ReadCachedValuesAsync(new[] { "A" });
        }

        CollectionAssert.AreEqual(
            new[] { "/api/capabilities", "/api/resources/values/get", "/api/resources/values/get" },
            handler.Requests.Select(r => r.Uri!.AbsolutePath).ToArray());
    }

    [TestMethod]
    public async Task A_station_without_the_feature_is_never_asked_for_values()
    {
        var (client, handler) = Create(WithoutFeature, "{\"resources\":{\"A\":\"1\"}}");
        using (client)
        {
            var ex = await Assert.ThrowsExactlyAsync<NotSupportedException>(() => client.Resources.ReadCachedValuesAsync(new[] { "A" }));
            StringAssert.Contains(ex.Message, CapabilitiesDto.ValuesCacheOnly);
        }

        Assert.IsFalse(handler.Requests.Any(r => r.Uri!.AbsolutePath == "/api/resources/values/get"),
            "an older WebApi would ignore cacheOnly and read the hardware");
    }

    [TestMethod]
    public async Task A_station_older_than_capabilities_is_never_asked_for_values()
    {
        var handler = new RecordingHandler { Respond = _ => RecordingHandler.Json(HttpStatusCode.NotFound, "{}") };
        using (var client = new AccordionQ2Client(Url, new AccordionQ2ClientOptions { Handler = handler }))
            await Assert.ThrowsExactlyAsync<NotSupportedException>(() => client.Resources.ReadCachedValuesAsync(new[] { "A" }));

        Assert.AreEqual(1, handler.Requests.Count);
    }

    [TestMethod]
    public async Task An_answer_without_notCached_means_cacheOnly_was_ignored()
    {
        var (client, _) = Create(WithFeature, "{\"resources\":{\"A\":\"1\"},\"ageMs\":{\"A\":0}}");
        using (client)
            await Assert.ThrowsExactlyAsync<NotSupportedException>(() => client.Resources.ReadCachedValuesAsync(new[] { "A" }));
    }

    [TestMethod]
    public async Task Every_name_cached_gives_an_empty_list()
    {
        var (client, _) = Create(WithFeature, "{\"resources\":{\"A\":\"1\"},\"ageMs\":{\"A\":40},\"notCached\":[]}");
        using (client)
        {
            var r = await client.Resources.ReadCachedValuesAsync(new[] { "A" });
            Assert.IsNotNull(r.NotCached);
            Assert.AreEqual(0, r.NotCached.Length);
            Assert.AreEqual("1", r.Resources["A"]);
        }
    }

    [TestMethod]
    public async Task ReadValuesAsync_is_unchanged()
    {
        var (client, handler) = Create(WithFeature, "{\"resources\":{\"A\":\"1\"},\"ageMs\":{\"A\":0}}");
        using (client)
        {
            var r = await client.Resources.ReadValuesAsync(new[] { "A" }, 1000);
            Assert.IsNull(r.NotCached);
        }

        Assert.AreEqual(1, handler.Requests.Count, "no capabilities call");
        Assert.IsNull(Body(handler.Last).GetValue("cacheOnly", StringComparison.OrdinalIgnoreCase));
    }
}
