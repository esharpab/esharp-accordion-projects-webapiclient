using AccordionQ2.WebApiClient.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AccordionQ2.WebApiClient.Tests;

[TestClass]
[TestCategory("Integration")]
public class ModulesTests
{
    private static AccordionQ2Client _client = null!;

    [ClassInitialize]
    public static void ClassInit(TestContext _) =>
        _client = new AccordionQ2Client(TestConfig.BaseUrl);

    [ClassCleanup]
    public static void ClassCleanup() => _client?.Dispose();

    [TestMethod]
    public async Task GetAll_ReturnsModules()
    {
        var modules = await _client.Modules.GetAllAsync();

        Console.WriteLine($"Modules count: {modules?.Count}");
        foreach (var m in modules!)
            Console.WriteLine($"  Module: {m.Name} | Enabled={m.Enabled} | Class={m.ClassName}");

        Assert.IsNotNull(modules);
        Assert.IsTrue(modules.Count > 0, "Expected at least one module");
    }

    [TestMethod]
    public async Task GetLoaded_ReturnsList()
    {
        var loaded = await _client.Modules.GetLoadedAsync();

        Console.WriteLine($"Loaded modules count: {loaded?.Count}");
        foreach (var m in loaded!)
            Console.WriteLine($"  Loaded: {m.Name} | Class={m.ClassName}");

        Assert.IsNotNull(loaded);
        // May be empty if no modules are loaded, but should not be null
    }

    [TestMethod]
    public async Task GetPhysicalSystem_ReturnsHostInfo()
    {
        var system = await _client.Modules.GetPhysicalSystemAsync();

        Console.WriteLine($"Host: {system?.Host}");
        Console.WriteLine($"EthIpV4: {system?.EthIpV4}");
        Console.WriteLine($"EthIpV6: {system?.EthIpV6}");
        Console.WriteLine($"MAC: {system?.MAC}");
        Console.WriteLine($"Firmware: {system?.Firmware}");

        Assert.IsNotNull(system);
        Assert.AreEqual(TestConfig.ExpectedHostName, system.Host);
        Assert.IsFalse(string.IsNullOrWhiteSpace(system.EthIpV4), "Expected an IPv4 address");
    }

    [TestMethod]
    public async Task GetPhysicalSystem_ContainsBaseModule()
    {
        var system = await _client.Modules.GetPhysicalSystemAsync();

        Console.WriteLine($"Physical modules count: {system.Modules?.Count}");
        foreach (var m in system.Modules!)
            Console.WriteLine($"  Slot {m.Index}: {m.Name} | ProductID={m.ProductID} | Rev={m.Revision} | SN={m.SerialNumber}");

        Assert.IsNotNull(system.Modules);
        Assert.IsTrue(system.Modules.Count > 0, "Expected at least one physical module");

        var baseModule = system.Modules.Find(m => m.ProductID == TestConfig.BaseModuleProductId);
        Assert.IsNotNull(baseModule, $"Expected base module with ProductID '{TestConfig.BaseModuleProductId}'");
        Assert.AreEqual(TestConfig.BaseModuleName, baseModule.Name);
    }

    [TestMethod]
    public async Task GetAllApps_ReturnsList()
    {
        try
        {
            var apps = await _client.Modules.GetAllAppsAsync();

            Console.WriteLine($"All apps count: {apps?.Count}");
            foreach (var a in apps!)
                Console.WriteLine($"  App: {a.Name} | Key={a.Key} | Type={a.Type} | Expires={a.Expires:O}");

            Assert.IsNotNull(apps);
        }
        catch (AccordionQ2ApiException ex) when (ex.Message.Contains("Timeout", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"Server-side timeout: {ex.Message}");
            Assert.Inconclusive($"Server-side timeout — backend did not respond in time: {ex.Message}");
        }
    }

    [TestMethod]
    public async Task GetLicensedApps_ReturnsList()
    {
        try
        {
            var apps = await _client.Modules.GetLicensedAppsAsync();

            Console.WriteLine($"Licensed apps count: {apps?.Count}");
            foreach (var a in apps!)
                Console.WriteLine($"  Licensed: {a.Name} | Key={a.Key} | Type={a.Type} | Expires={a.Expires:O}");

            Assert.IsNotNull(apps);
        }
        catch (AccordionQ2ApiException ex) when (ex.Message.Contains("Timeout", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"Server-side timeout: {ex.Message}");
            Assert.Inconclusive($"Server-side timeout — backend did not respond in time: {ex.Message}");
        }
    }
}
