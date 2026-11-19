using System.Net;
using AccordionQ2.WebApiClient.Models;
using AccordionQ2.WebApiClient.Tests.Fakes;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AccordionQ2.WebApiClient.Tests;

/// <summary>
/// The firmware's security updates in the client (the firmware's FW7_PLAN CRA-05, CRA-11): the new DTO members bind as the
/// WebApi answers them, older firmware leaves them at their defaults, and the new calls use their routes. No network.
/// </summary>
[TestClass]
public class FirmwareSecurityTests
{
    private const string Url = "http://station:5000";

    // As the WebApi answers (camelCase; FirmwareState, AutoUpdateInfo, OsUpdatesInfo, RuntimeInfo)
    private const string State =
        "{\"current\":\"7.1.0\",\"source\":{\"kind\":\"url\",\"location\":\"https://x\",\"isDefault\":true},\"defaultSource\":\"https://x\"," +
        "\"supported\":true,\"minimumVersion\":\"6.0.0\",\"update\":{\"state\":\"idle\"}," +
        "\"runtime\":{\"netCore\":\"10.0.14\",\"aspNetCore\":\"10.0.14\",\"root\":\"/opt/accordion/runtimes/10.0.14\",\"bundled\":\"10.0.14\",\"active\":\"10.0.14\"," +
        "\"installed\":[\"10.0.14\"],\"system\":null,\"enabled\":true,\"applicable\":null,\"waiting\":null,\"trialLinkPresent\":false," +
        "\"install\":{\"state\":\"installed\",\"version\":\"10.0.14\",\"attempt\":1,\"trigger\":\"update\",\"message\":\"ok\",\"time\":\"2026-11-19T10:00:00+00:00\"},\"fallback\":\"sudo rm x\"}," +
        "\"autoUpdate\":{\"mode\":\"security\",\"modeIsDefault\":false,\"window\":\"02:00-05:00\",\"lastCheck\":\"2026-11-19T10:00:00+00:00\",\"lastCheckError\":null," +
        "\"available\":\"7.1.1\",\"availableIsSecurity\":true,\"waiting\":\"outside the window 02:00-05:00\",\"lastAttempt\":null,\"lastResult\":null,\"nextCheck\":null}," +
        "\"osUpdates\":{\"bundle\":\"bookworm-20261119T1200\",\"bundleCreated\":\"2026-11-19T12:00:00+00:00\",\"packages\":3,\"state\":\"partial\",\"message\":\"m\"," +
        "\"time\":null,\"upgraded\":[\"libssl3 a -> b\"],\"keptBack\":[\"curl: not in the bundle\"],\"rebootRecommended\":true," +
        "\"runtimesRemoved\":[\"/home/accordion/.dotnet/shared/Microsoft.NETCore.App/8.0.11\"],\"applicable\":null,\"waiting\":null,\"enabled\":true," +
        "\"removeUnusedRuntimes\":true,\"bytesFreed\":70000000,\"unusedRuntimes\":[]},\"sbom\":true}";

    [TestMethod]
    public async Task The_firmware_state_binds_the_new_members()
    {
        var handler = new RecordingHandler { Respond = _ => RecordingHandler.Json(HttpStatusCode.OK, State) };
        using var client = new AccordionQ2Client(Url, new AccordionQ2ClientOptions { Handler = handler });
        var state = await client.Firmware.GetStateAsync();
        Assert.IsTrue(state.Sbom);
        Assert.AreEqual("10.0.14", state.Runtime!.Active);
        Assert.AreEqual("installed", state.Runtime.Install!.State);
        Assert.AreEqual("security", state.AutoUpdate!.Mode);
        Assert.AreEqual("7.1.1", state.AutoUpdate.Available);
        Assert.IsTrue(state.AutoUpdate.AvailableIsSecurity);
        Assert.AreEqual("partial", state.OsUpdates!.State);
        Assert.IsTrue(state.OsUpdates.RebootRecommended);
        Assert.AreEqual("libssl3 a -> b", state.OsUpdates.Upgraded.Single());
        Assert.AreEqual(70000000, state.OsUpdates.BytesFreed);
    }

    [TestMethod]
    public async Task Older_firmware_leaves_the_new_members_at_their_defaults()
    {
        var handler = new RecordingHandler
        {
            Respond = r => r.RequestUri!.AbsolutePath.EndsWith("/releases")
                ? RecordingHandler.Json(HttpStatusCode.OK, "{\"source\":{\"kind\":\"url\",\"location\":\"x\",\"isDefault\":true},\"releases\":[{\"version\":\"7.0.5\",\"fileName\":\"rel-7.0.5.zip\"}],\"error\":null}")
                : RecordingHandler.Json(HttpStatusCode.OK, "{\"current\":\"7.0.4\",\"supported\":true,\"minimumVersion\":\"6.0.0\",\"update\":{\"state\":\"idle\"}}"),
        };
        using var client = new AccordionQ2Client(Url, new AccordionQ2ClientOptions { Handler = handler });
        var state = await client.Firmware.GetStateAsync();
        Assert.IsFalse(state.Sbom);
        Assert.IsNull(state.AutoUpdate);
        Assert.IsNull(state.OsUpdates);
        var releases = await client.Firmware.GetReleasesAsync();
        Assert.IsNull(releases.SecurityUpdate);
        Assert.IsFalse(releases.Releases.Single().Security);
    }

    [TestMethod]
    public async Task The_new_calls_use_their_routes()
    {
        var handler = new RecordingHandler
        {
            Respond = r => r.RequestUri!.AbsolutePath switch
            {
                "/api/system/firmware/sbom" => RecordingHandler.Json(HttpStatusCode.OK, "{\"bomFormat\":\"CycloneDX\"}"),
                "/api/system/firmware/os-updates/install" => RecordingHandler.Json(HttpStatusCode.Accepted, "{\"bundleId\":\"b\",\"state\":\"requested\",\"attempt\":1,\"mode\":\"apply\"}"),
                _ => RecordingHandler.Json(HttpStatusCode.OK, "{\"mode\":\"off\",\"modeIsDefault\":false}"),
            },
        };
        using var client = new AccordionQ2Client(Url, new AccordionQ2ClientOptions { Handler = handler });

        Assert.AreEqual("off", (await client.Firmware.SetAutoUpdateAsync("off", null)).Mode);
        Assert.AreEqual(HttpMethod.Put, handler.Last.Method);
        Assert.AreEqual("/api/system/firmware/auto-update", handler.Last.Uri!.AbsolutePath);

        await client.Firmware.CheckAutoUpdateAsync();
        Assert.AreEqual(HttpMethod.Post, handler.Last.Method);
        Assert.AreEqual("/api/system/firmware/auto-update/check", handler.Last.Uri!.AbsolutePath);

        var record = await client.Firmware.InstallOsUpdatesAsync(force: true);
        Assert.AreEqual("requested", record.State);
        Assert.AreEqual("apply", record.Mode);
        Assert.AreEqual("/api/system/firmware/os-updates/install", handler.Last.Uri!.AbsolutePath);

        StringAssert.Contains(await client.Firmware.GetSbomAsync(), "CycloneDX");
        Assert.AreEqual(HttpMethod.Get, handler.Last.Method);
    }
}
