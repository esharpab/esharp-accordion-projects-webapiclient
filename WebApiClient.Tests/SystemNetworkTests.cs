using System.Net;
using AccordionQ2.WebApiClient.Models;
using AccordionQ2.WebApiClient.Tests.Fakes;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AccordionQ2.WebApiClient.Tests;

/// <summary>
/// <c>System.GetNetworkAsync</c> (the firmware's FW7_PLAN NET-05): the station's addresses, IPv4 and IPv6. No network: a fake
/// handler answers.
/// </summary>
[TestClass]
public class SystemNetworkTests
{
    private const string Url = "http://station:5000";

    // As the WebApi answers (camelCase, NetworkView of WebApi/Services/StationNetwork.cs)
    private const string Network =
        "{\"interfaces\":[{\"name\":\"eth0\",\"mac\":\"d8:3a:dd:00:00:01\",\"state\":\"UP\",\"addresses\":[" +
        "{\"address\":\"192.0.2.10\",\"prefix\":24,\"family\":\"IPv4\",\"kind\":\"global\",\"origin\":\"dhcp\",\"deprecated\":false," +
        "\"tentative\":false,\"dadFailed\":false,\"validSeconds\":86000,\"configured\":false}," +
        "{\"address\":\"2001:db8::10\",\"prefix\":64,\"family\":\"IPv6\",\"kind\":\"global\",\"origin\":\"static\",\"deprecated\":true," +
        "\"tentative\":true,\"dadFailed\":true,\"validSeconds\":null,\"configured\":true}]}," +
        "{\"name\":\"wlan0\",\"mac\":null,\"state\":\"DOWN\",\"addresses\":[]}]}";

    [TestMethod]
    public async Task GetNetworkAsync_calls_GET_api_system_network_and_binds_every_field()
    {
        var handler = new RecordingHandler { Respond = _ => RecordingHandler.Json(HttpStatusCode.OK, Network) };
        NetworkDto network;
        using (var client = new AccordionQ2Client(Url, new AccordionQ2ClientOptions { Handler = handler }))
            network = await client.System.GetNetworkAsync();

        Assert.AreEqual(HttpMethod.Get, handler.Last.Method);
        Assert.AreEqual("/api/system/network", handler.Last.Uri!.AbsolutePath);

        Assert.AreEqual(2, network.Interfaces.Count);
        var eth0 = network.Interfaces[0];
        Assert.AreEqual("eth0", eth0.Name);
        Assert.AreEqual("d8:3a:dd:00:00:01", eth0.Mac);
        Assert.AreEqual("UP", eth0.State);
        Assert.AreEqual(2, eth0.Addresses.Count);

        var v4 = eth0.Addresses[0];
        Assert.AreEqual("192.0.2.10", v4.Address);
        Assert.AreEqual(24, v4.Prefix);
        Assert.AreEqual("IPv4", v4.Family);
        Assert.AreEqual("global", v4.Kind);
        Assert.AreEqual("dhcp", v4.Origin);
        Assert.IsFalse(v4.Deprecated);
        Assert.IsFalse(v4.Tentative);
        Assert.IsFalse(v4.DadFailed);
        Assert.AreEqual(86000, v4.ValidSeconds);
        Assert.IsFalse(v4.Configured);

        var v6 = eth0.Addresses[1];
        Assert.AreEqual("2001:db8::10", v6.Address);
        Assert.AreEqual(64, v6.Prefix);
        Assert.AreEqual("IPv6", v6.Family);
        Assert.AreEqual("static", v6.Origin);
        Assert.IsTrue(v6.Deprecated);
        Assert.IsTrue(v6.Tentative);
        Assert.IsTrue(v6.DadFailed);
        Assert.IsNull(v6.ValidSeconds, "null is for ever");
        Assert.IsTrue(v6.Configured);

        Assert.IsNull(network.Interfaces[1].Mac);
        Assert.AreEqual("DOWN", network.Interfaces[1].State);
        Assert.AreEqual(0, network.Interfaces[1].Addresses.Count);
    }

    [TestMethod]
    public async Task Older_firmware_without_the_call_throws_404()
    {
        var handler = new RecordingHandler { Respond = _ => RecordingHandler.Json(HttpStatusCode.NotFound, "{}") };
        using var client = new AccordionQ2Client(Url, new AccordionQ2ClientOptions { Handler = handler });

        var ex = await Assert.ThrowsExactlyAsync<AccordionQ2ApiException>(() => client.System.GetNetworkAsync());
        Assert.AreEqual(404, ex.StatusCode);
    }
}
