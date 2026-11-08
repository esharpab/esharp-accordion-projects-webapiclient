using System.Net;
using System.Text;
using AccordionQ2.WebApiClient.Groups;
using AccordionQ2.WebApiClient.Models;
using AccordionQ2.WebApiClient.Tests.Fakes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace AccordionQ2.WebApiClient.Tests;

/// <summary>
/// <see cref="DrummerGroup"/> and <see cref="DrummerTarget"/> (Drummer/CONTROL.md section 11): operations as transactions
/// on Call channels, the capability check, job following, the SIB_ prefix, 409. No network: a fake handler answers.
/// </summary>
[TestClass]
public class DrummerTests
{
    private const string Url = "http://station:5000";
    private const string WithFeature = "{\"apiVersion\":1,\"features\":[\"events\",\"resources.transact\"]}";
    private const string WithoutFeature = "{\"apiVersion\":1,\"features\":[\"events\"]}";

    private static (AccordionQ2Client Client, RecordingHandler Handler) Create(Func<string, JObject?, HttpResponseMessage> answer)
    {
        var handler = new RecordingHandler
        {
            Respond = r =>
            {
                string path = r.RequestUri!.AbsolutePath;
                JObject? body = r.Content is null ? null : JObject.Parse(r.Content.ReadAsStringAsync().Result);
                return answer(path, body);
            },
        };
        return (new AccordionQ2Client(Url, new AccordionQ2ClientOptions { Handler = handler }), handler);
    }

    private static HttpResponseMessage Ok(string json) => RecordingHandler.Json(HttpStatusCode.OK, json);

    // {"value": "<the operation's answer as text>"}, as /api/resources/transact answers
    private static HttpResponseMessage Value(JToken answer) =>
        Ok(new JObject { ["value"] = answer.ToString(Newtonsoft.Json.Formatting.None) }.ToString());

    private static JObject Body(RecordingHandler.Recorded request) => JObject.Parse(Encoding.UTF8.GetString(request.Body));

    private static List<RecordingHandler.Recorded> Transactions(RecordingHandler h) =>
        h.Requests.Where(r => r.Uri!.AbsolutePath == "/api/resources/transact").ToList();

    [TestMethod]
    public async Task CallAsync_sends_the_operation_as_a_transaction_on_the_segments_Call_channel()
    {
        var (client, handler) = Create((path, _) => path == "/api/capabilities" ? Ok(WithFeature)
            : Value(new JObject { ["count"] = 600123, ["hz"] = 6001230.0, ["ms"] = 101.3, ["seq"] = 12 }));
        JObject r;
        using (client)
        {
            r = await client.Drummer.Target("Drummer").CallAsync("SIB_CLK", "clock_measure", new { gate = 1200000, channel = 2 });
        }

        Assert.AreEqual(600123, (int)r["count"]!);
        Assert.AreEqual(12, (long)r["seq"]!);
        CollectionAssert.AreEqual(new[] { "/api/capabilities", "/api/resources/transact" },
            handler.Requests.Select(x => x.Uri!.AbsolutePath).ToArray());
        var post = Body(handler.Last);
        Assert.AreEqual("Drummer.CLK.Call", (string)post["Name"]!, "SIB_ dropped");
        var request = JObject.Parse((string)post["Value"]!);
        Assert.AreEqual("clock_measure", (string)request["call"]!);
        Assert.AreEqual(1200000, (int)request["gate"]!);
        Assert.AreEqual(2, (int)request["channel"]!);
        Assert.AreEqual(DrummerTarget.WaitMs, (int)request["wait_ms"]!);
    }

    [TestMethod]
    public async Task Arguments_named_op_or_name_pass_through_and_a_JObject_or_dictionary_works_too()
    {
        var (client, handler) = Create((path, _) => path == "/api/capabilities" ? Ok(WithFeature) : Value(new JObject { ["ms"] = 1.0 }));
        using (client)
        {
            var t = client.Drummer.Target("Drummer");
            await t.CallAsync("MEM", "mem_start", new JObject { ["op"] = "write_read", ["count"] = 65536 });
            await t.CallAsync("MEM", "mem_start", new Dictionary<string, object> { ["op"] = "read", ["name"] = "x" });
            await t.ModuleCallAsync("la_group_read");
        }

        var tx = Transactions(handler);
        var first = JObject.Parse((string)Body(tx[0])["Value"]!);
        Assert.AreEqual("mem_start", (string)first["call"]!);
        Assert.AreEqual("write_read", (string)first["op"]!);
        Assert.AreEqual(65536, (int)first["count"]!);
        var second = JObject.Parse((string)Body(tx[1])["Value"]!);
        Assert.AreEqual("read", (string)second["op"]!);
        Assert.AreEqual("x", (string)second["name"]!);
        Assert.AreEqual("Drummer.Target.Call", (string)Body(tx[2])["Name"]!);
        Assert.AreEqual("la_group_read", (string)JObject.Parse((string)Body(tx[2])["Value"]!)["call"]!);
    }

    [TestMethod]
    public async Task A_running_job_is_followed_until_it_has_ended()
    {
        int n = 0;
        var (client, handler) = Create((path, _) =>
        {
            if (path == "/api/capabilities") return Ok(WithFeature);
            n++;
            return n < 3
                ? Value(new JObject { ["job"] = "3f9c0a1b2d4e5f60", ["state"] = "running", ["call"] = "bram_read", ["sib"] = "SIB_LA_MEM", ["progress"] = "", ["ms"] = 10000.4 })
                : Value(new JObject { ["words"] = new JArray(1, 2, 3), ["ms"] = 21000.0, ["seq"] = 7 });
        });
        JObject r;
        using (client)
        {
            r = await client.Drummer.Target("Drummer").CallAsync("LA_MEM", "bram_read", new { addr = 0, count = 3 });
        }

        CollectionAssert.AreEqual(new[] { 1, 2, 3 }, r["words"]!.Values<int>().ToArray());
        var tx = Transactions(handler);
        Assert.AreEqual(3, tx.Count);
        foreach (var follow in tx.Skip(1))
        {
            Assert.AreEqual("Drummer.LA_MEM.Call", (string)Body(follow)["Name"]!);
            var v = JObject.Parse((string)Body(follow)["Value"]!);
            Assert.AreEqual("job", (string)v["call"]!);
            Assert.AreEqual("3f9c0a1b2d4e5f60", (string)v["job"]!);
            Assert.AreEqual(DrummerTarget.WaitMs, (int)v["wait_ms"]!);
        }
    }

    [TestMethod]
    public async Task A_job_still_running_after_the_timeout_names_its_id_and_is_left_running()
    {
        var (client, handler) = Create((path, _) => path == "/api/capabilities" ? Ok(WithFeature)
            : Value(new JObject { ["job"] = "00aa", ["state"] = "running", ["call"] = "gt_ch_prbs", ["sib"] = "SIB_GT0", ["ms"] = 10000.0 }));
        using (client)
        {
            var t = client.Drummer.Target("Drummer");
            long now = 0;
            t.Clock = () => now += 7000;            // 7 s per reading: the second follow would pass 20 s
            var e = await Assert.ThrowsExactlyAsync<TimeoutException>(() =>
                t.CallAsync("GT0", "gt_ch_prbs", new { seconds = 30 }, TimeSpan.FromSeconds(20)));
            StringAssert.Contains(e.Message, "job 00aa");
            StringAssert.Contains(e.Message, "goes on in the module");
        }
        Assert.IsFalse(Transactions(handler).Any(x => (string?)JObject.Parse((string)Body(x)["Value"]!)["call"] == "job_cancel"));
    }

    [TestMethod]
    public async Task Without_the_capability_nothing_is_sent_and_the_error_says_why()
    {
        var (client, handler) = Create((path, _) => path == "/api/capabilities" ? Ok(WithoutFeature) : Value(new JObject()));
        using (client)
        {
            var e = await Assert.ThrowsExactlyAsync<NotSupportedException>(() =>
                client.Drummer.Target("Drummer").CallAsync("GPIO", "read"));
            StringAssert.Contains(e.Message, "this station's firmware runs transactions for Admin only, under the engine lock");
        }
        Assert.AreEqual(0, Transactions(handler).Count);
        Assert.AreEqual(CapabilitiesDto.ResourcesTransact, "resources.transact");
    }

    [TestMethod]
    public async Task The_capability_is_asked_once_and_again_after_a_failed_call()
    {
        int tx = 0;
        var (client, handler) = Create((path, _) =>
        {
            if (path == "/api/capabilities") return Ok(WithFeature);
            return ++tx == 2 ? RecordingHandler.Json(HttpStatusCode.InternalServerError, "{\"error\":\"boom\"}") : Value(new JObject());
        });
        using (client)
        {
            var t = client.Drummer.Target("Drummer");
            await t.CallAsync("GPIO", "read");
            await Assert.ThrowsExactlyAsync<AccordionQ2ApiException>(() => t.CallAsync("GPIO", "read"));
            await t.CallAsync("GPIO", "read");
        }
        CollectionAssert.AreEqual(
            new[] { "/api/capabilities", "/api/resources/transact", "/api/resources/transact", "/api/capabilities", "/api/resources/transact" },
            handler.Requests.Select(r => r.Uri!.AbsolutePath).ToArray());
    }

    [TestMethod]
    public async Task A_busy_target_is_a_409_with_the_stations_message()
    {
        var (client, _) = Create((path, _) => path == "/api/capabilities" ? Ok(WithFeature)
            : RecordingHandler.Json(HttpStatusCode.Conflict, "{\"error\":\"la_read on LA holds the JTAG now; try again when it has ended\"}"));
        using (client)
        {
            var e = await Assert.ThrowsExactlyAsync<AccordionQ2ApiException>(() =>
                client.Drummer.Target("Drummer").CallAsync("MMCM", "mmcm_info"));
            Assert.AreEqual(409, e.StatusCode);
            StringAssert.Contains(e.Message, "holds the JTAG");
        }
    }

    [TestMethod]
    public async Task Call_and_job_are_not_arguments()
    {
        var (client, handler) = Create((path, _) => Ok(WithFeature));
        using (client)
        {
            var t = client.Drummer.Target("Drummer");
            await Assert.ThrowsExactlyAsync<ArgumentException>(() => t.CallAsync("GPIO", "read", new { call = "x" }));
            await Assert.ThrowsExactlyAsync<ArgumentException>(() => t.CallAsync("GPIO", "read", new { job = "x" }));
            Assert.ThrowsExactly<ArgumentException>(() => DrummerTarget.Segment("SIB_"));
        }
        Assert.AreEqual(0, handler.Requests.Count, "refused before anything is sent");
    }

    [TestMethod]
    public async Task Reads_go_to_the_channels_below_the_module()
    {
        var (client, handler) = Create((path, body) => (string?)body?["Name"] switch
        {
            "Drummer.Target.Status" => Ok("{\"value\":\"{\\\"connected\\\":true}\"}"),
            "Drummer.Target.Jobs" => Ok("{\"value\":\"[]\"}"),
            "Drummer.MEM.Snapshot" => Ok("{\"value\":\"{\\\"stale\\\":\\\"busy\\\",\\\"fields\\\":null}\"}"),
            "Drummer.CLK.Freq0" => Ok("{\"value\":\"233250000\"}"),
            _ => Ok("{}"),
        });
        using (client)
        {
            var t = client.Drummer.Target("Drummer.");
            Assert.AreEqual("Drummer", t.Prefix);
            Assert.IsTrue((bool)(await t.StatusAsync())["connected"]!);
            Assert.AreEqual(0, (await t.JobsAsync()).Count);
            var snap = await t.SnapshotAsync("SIB_MEM");
            Assert.AreEqual(JTokenType.Null, snap["fields"]!.Type);
            Assert.AreEqual("233250000", await t.ReadAsync("CLK.Freq0"));
            await t.WriteAsync("GPIO.GPIO0", "True");
        }
        var set = handler.Last;
        Assert.AreEqual("/api/resources/value/set", set.Uri!.AbsolutePath);
        Assert.AreEqual("Drummer.GPIO.GPIO0", (string)Body(set)["Name"]!);
        Assert.AreEqual("True", (string)Body(set)["Value"]!);
    }

    [TestMethod]
    public async Task FindAsync_takes_the_Drummer_instruments_else_Target_Status_beside_Target_Package()
    {
        var (client, _) = Create((path, _) => path switch
        {
            "/api/instruments" => Ok("[{\"netName\":\"Drummer.Drummer\",\"instrumentName\":\"Drummer\",\"type\":\"Drummer\"},"
                                     + "{\"netName\":\"0.4.ESH10000662.PS\",\"type\":\"PowerSupply\"}]"),
            _ => Ok("[]"),
        });
        using (client)
        {
            var found = await client.Drummer.FindAsync();
            Assert.AreEqual(1, found.Count);
            Assert.AreEqual("Drummer", found[0].Prefix);
            Assert.AreEqual("Drummer", found[0].Name);
        }

        var (old, _) = Create((path, _) => path switch
        {
            "/api/instruments" => RecordingHandler.Json(HttpStatusCode.NotFound, "{}"),
            "/api/resources/names" => Ok("[\"Drummer.Target.Status\",\"Drummer.Target.Package\",\"0.2.X.Target.Status\"]"),
            _ => Ok("[]"),
        });
        using (old)
        {
            var found = await old.Drummer.FindAsync();
            CollectionAssert.AreEqual(new[] { "Drummer" }, found.Select(f => f.Prefix).ToArray());
        }
    }
}
