using System.Net;
using System.Text;
using AccordionQ2.WebApiClient.Groups;
using AccordionQ2.WebApiClient.Models;
using AccordionQ2.WebApiClient.Tests.Fakes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace AccordionQ2.WebApiClient.Tests;

/// <summary>
/// Stable module names and JTAG ports (the firmware's Drummer/PORTS.md): <c>Modules.GetPresentAsync</c>,
/// <c>client.JtagPorts</c>, <c>DrummerTarget.GetPortAsync</c> and <c>BindPortAsync</c>, the capabilities' <c>names</c>.
/// No network: a fake handler answers.
/// </summary>
[TestClass]
public class JtagPortsTests
{
    private const string Url = "http://station:5000";

    private const string PortState =
        "{\"port\":\"MicroHAPS.JTAG.A\",\"owner\":\"MicroHAPS\",\"label\":\"FT4232H J020324 interface A\",\"seq\":42,\"state\":\"held\"," +
        "\"holder\":{\"kind\":\"borrower\",\"name\":\"Drummer\",\"what\":null,\"since\":\"2026-11-20T10:00:00.0000000Z\"},\"why\":null," +
        "\"bound\":\"Drummer\",\"max_tck_hz\":7500000,\"xvc\":{\"enabled\":true,\"engine\":\"drummer\",\"listen\":\"0.0.0.0:2542\"," +
        "\"allow\":[\"192.168.0.12\"],\"client\":null,\"waiting\":false,\"sessions\":3,\"last_end\":\"the client closed the connection\"}," +
        "\"since\":\"2026-11-20T10:00:00.0000000Z\"}";

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

    private static JObject Body(RecordingHandler.Recorded request) => JObject.Parse(Encoding.UTF8.GetString(request.Body));

    [TestMethod]
    public async Task GetPresentAsync_lists_hardware_and_software_modules()
    {
        var (client, handler) = Create((path, _) => path == "/api/modules/present" ? Ok(
            "[{\"kind\":\"hardware\",\"address\":\"0.2\",\"prefix\":\"0.2.ESH10000560\",\"name\":\"MPIO-96 SPI Module\"," +
            "\"productId\":\"ESH10000560\",\"revision\":3,\"serialNumber\":\"A001234\",\"className\":null}," +
            "{\"kind\":\"software\",\"address\":\"MicroHAPS\",\"prefix\":\"MicroHAPS\",\"name\":\"MicroHAPS\",\"productId\":null," +
            "\"revision\":null,\"serialNumber\":null,\"className\":\"MicroHapsHandler.MicroHaps\"}]")
            : RecordingHandler.Json(HttpStatusCode.NotFound, "{}"));
        List<ModulePresenceDto> present;
        using (client)
            present = await client.Modules.GetPresentAsync();

        Assert.AreEqual("/api/modules/present", handler.Last.Uri!.AbsolutePath);
        Assert.AreEqual(2, present.Count);
        Assert.IsTrue(present[0].IsHardware);
        Assert.AreEqual("0.2.ESH10000560", present[0].Prefix);
        Assert.AreEqual(3, present[0].Revision);
        Assert.IsFalse(present[1].IsHardware);
        Assert.AreEqual("MicroHAPS", present[1].Address);
        Assert.IsNull(present[1].Revision);
        Assert.AreEqual("MicroHapsHandler.MicroHaps", present[1].ClassName);
    }

    [TestMethod]
    public async Task GetPresentAsync_throws_404_on_older_firmware()
    {
        var (client, _) = Create((_, _) => RecordingHandler.Json(HttpStatusCode.NotFound, "{\"error\":\"not found\"}"));
        using (client)
        {
            var ex = await Assert.ThrowsExactlyAsync<AccordionQ2ApiException>(() => client.Modules.GetPresentAsync());
            Assert.AreEqual(404, ex.StatusCode);
        }
    }

    [TestMethod]
    public async Task GetAllAsync_finds_the_ports_by_the_Unit_of_their_State_channels_and_reads_them()
    {
        var (client, handler) = Create((path, body) => path switch
        {
            "/api/channels" => Ok(
                "[{\"netName\":\"MicroHAPS.JTAG.A.State\",\"unit\":\"jtag-port\",\"channelType\":\"Register\"}," +
                "{\"netName\":\"MicroHAPS.JTAG.A.Xvc\",\"unit\":\"jtag-port:xvc\",\"channelType\":\"Digital\"}," +
                "{\"netName\":\"JTAG_FTDI.JTAG.A.State\",\"unit\":\"jtag-port\",\"channelType\":\"Register\"}," +
                "{\"netName\":\"0.2.ESH10000560.CH1\",\"unit\":\"V\",\"channelType\":\"Analog\"}]"),
            "/api/resources/values/get" => Ok(new JObject
            {
                ["resources"] = new JObject { ["MicroHAPS.JTAG.A.State"] = PortState, ["JTAG_FTDI.JTAG.A.State"] = "not json" },
            }.ToString()),
            _ => RecordingHandler.Json(HttpStatusCode.NotFound, "{}"),
        });
        List<JtagPortDto> ports;
        using (client)
            ports = await client.JtagPorts.GetAllAsync();

        CollectionAssert.AreEqual(new[] { "JTAG_FTDI.JTAG.A", "MicroHAPS.JTAG.A" }, ports.Select(p => p.Name).ToArray());
        var names = (JArray)Body(handler.Last)["Names"]!;
        CollectionAssert.AreEquivalent(new[] { "MicroHAPS.JTAG.A.State", "JTAG_FTDI.JTAG.A.State" }, names.Select(n => (string)n!).ToArray());
        Assert.IsNull(ports[0].State, "a value that is not the JSON expected");
        var s = ports[1].State!;
        Assert.AreEqual("held", s.State);
        Assert.AreEqual(42, s.Seq);
        Assert.AreEqual("borrower", s.Holder!.Kind);
        Assert.AreEqual("Drummer", s.Holder.Name);
        Assert.AreEqual("Drummer", s.Bound);
        Assert.AreEqual(7500000, s.MaxTckHz);
        Assert.IsTrue(s.Xvc!.Enabled);
        Assert.AreEqual("0.0.0.0:2542", s.Xvc.Listen);
        CollectionAssert.AreEqual(new[] { "192.168.0.12" }, s.Xvc.Allow);
        Assert.AreEqual("MicroHAPS.JTAG.A.Xvc", ports[1].XvcChannel);
    }

    [TestMethod]
    public async Task GetAllAsync_is_empty_without_ports_and_reads_nothing()
    {
        var (client, handler) = Create((path, _) => path == "/api/channels"
            ? Ok("[{\"netName\":\"0.2.ESH10000560.CH1\",\"unit\":\"V\",\"channelType\":\"Analog\"}]")
            : RecordingHandler.Json(HttpStatusCode.NotFound, "{}"));
        using (client)
            Assert.AreEqual(0, (await client.JtagPorts.GetAllAsync()).Count);
        Assert.AreEqual(1, handler.Requests.Count);
    }

    [TestMethod]
    public async Task SetXvcAsync_writes_the_ports_switch()
    {
        var (client, handler) = Create((_, _) => Ok("{}"));
        using (client)
        {
            await client.JtagPorts.SetXvcAsync("MicroHAPS.JTAG.A", true);
            await client.JtagPorts.SetXvcAsync("MicroHAPS.JTAG.A.State", false);
        }
        Assert.AreEqual("/api/resources/value/set", handler.Requests[0].Uri!.AbsolutePath);
        Assert.AreEqual("MicroHAPS.JTAG.A.Xvc", (string)Body(handler.Requests[0])["Name"]!);
        Assert.AreEqual("True", (string)Body(handler.Requests[0])["Value"]!);
        Assert.AreEqual("MicroHAPS.JTAG.A.Xvc", (string)Body(handler.Requests[1])["Name"]!, "a channel's name is taken as its port");
        Assert.AreEqual("False", (string)Body(handler.Requests[1])["Value"]!);
        Assert.ThrowsExactly<ArgumentException>(() => JtagPortsGroup.PortName(" "));
    }

    [TestMethod]
    public async Task GetStateAsync_reads_one_port()
    {
        var (client, handler) = Create((_, _) => Ok(new JObject { ["value"] = PortState }.ToString()));
        JtagPortStateDto? s;
        using (client)
            s = await client.JtagPorts.GetStateAsync("MicroHAPS.JTAG.A");
        Assert.AreEqual("MicroHAPS.JTAG.A.State", (string)Body(handler.Last)["Name"]!);
        Assert.AreEqual("MicroHAPS", s!.Owner);
        Assert.AreEqual("", s.Incarnation, "older firmware: no incarnation");
        Assert.IsNull(s.Disturbed);
    }

    [TestMethod]
    public async Task The_state_carries_the_ports_incarnation_and_disturbances()
    {
        var state = PortState.Replace("\"since\":\"2026-11-20T10:00:00.0000000Z\"}",
            "\"since\":\"2026-11-20T10:00:00.0000000Z\",\"incarnation\":\"9f2c4e1a7b3d5f60\"," +
            "\"disturbed\":{\"count\":7,\"why\":\"MicroHAPS's use: a POWER cycle\",\"since\":\"2026-11-08T15:02:00.0000000Z\"}}");
        var (client, _) = Create((_, _) => Ok(new JObject { ["value"] = state }.ToString()));
        JtagPortStateDto? s;
        using (client)
            s = await client.JtagPorts.GetStateAsync("MicroHAPS.JTAG.A");
        Assert.AreEqual("9f2c4e1a7b3d5f60", s!.Incarnation);
        Assert.AreEqual(7, s.Disturbed!.Count);
        Assert.AreEqual("MicroHAPS's use: a POWER cycle", s.Disturbed.Why);
        Assert.AreEqual(new DateTimeOffset(2026, 11, 8, 15, 2, 0, TimeSpan.Zero), s.Disturbed.Since);
    }

    [TestMethod]
    public async Task A_Drummer_instance_binds_and_unbinds_with_Target_Port()
    {
        var (client, handler) = Create((path, _) => path == "/api/resources/value/get"
            ? Ok("{\"value\":\" MicroHAPS.JTAG.A \"}") : Ok("{}"));
        string port;
        using (client)
        {
            var t = client.Drummer.Target("Drummer");
            port = await t.GetPortAsync();
            await t.BindPortAsync("JTAG_FTDI.JTAG.A");
            await t.BindPortAsync(null);
        }
        Assert.AreEqual("MicroHAPS.JTAG.A", port);
        Assert.AreEqual("Drummer.Target.Port", (string)Body(handler.Requests[0])["Name"]!);
        Assert.AreEqual("Drummer.Target.Port", (string)Body(handler.Requests[1])["Name"]!);
        Assert.AreEqual("JTAG_FTDI.JTAG.A", (string)Body(handler.Requests[1])["Value"]!);
        Assert.AreEqual("", (string)Body(handler.Requests[2])["Value"]!, "null unbinds");
    }

    [TestMethod]
    public async Task The_capabilities_say_whether_names_are_stable_and_give_the_old_names_map()
    {
        var (client, _) = Create((_, _) => Ok(
            "{\"apiVersion\":1,\"features\":[],\"names\":{\"stable\":true,\"oldNamesAccepted\":true,\"legacy\":{" +
            "\"prefixes\":{\"MicroHAPS\":\"MicroHAPS\",\"MicroHAPS_PMBUS_LTPP\":\"MicroHAPS.MicroHAPS_PMBUS_LTPP\"},\"positions\":{\"1\":\"MicroHAPS\"}}}}"));
        CapabilitiesDto caps;
        using (client)
            caps = await client.Capabilities.GetAsync();
        Assert.IsTrue(caps.Names!.Stable);
        Assert.IsTrue(caps.Names.OldNamesAccepted);
        Assert.AreEqual("MicroHAPS.MicroHAPS_PMBUS_LTPP", caps.Names.Legacy!.Prefixes["MicroHAPS_PMBUS_LTPP"]);
        Assert.AreEqual("MicroHAPS", caps.Names.Legacy.Positions["1"]);

        var (older, _) = Create((_, _) => Ok("{\"apiVersion\":1,\"features\":[]}"));
        using (older)
            Assert.IsNull((await older.Capabilities.GetAsync()).Names);
    }
}
