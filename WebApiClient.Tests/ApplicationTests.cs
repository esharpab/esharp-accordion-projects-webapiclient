using AccordionQ2.WebApiClient.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AccordionQ2.WebApiClient.Tests;

[TestClass]
[TestCategory("Integration")]
public class ApplicationTests
{
    private static AccordionQ2Client _client = null!;

    [ClassInitialize]
    public static void ClassInit(TestContext _) =>
        _client = new AccordionQ2Client(TestConfig.BaseUrl);

    [ClassCleanup]
    public static void ClassCleanup() => _client?.Dispose();

    [TestMethod]
    public async Task GetName_ReturnsNonEmpty()
    {
        var name = await _client.Application.GetNameAsync();

        Console.WriteLine($"Application name: '{name}'");

        Assert.IsNotNull(name);
        Assert.IsFalse(string.IsNullOrWhiteSpace(name), "Application name should not be empty");
    }

    [TestMethod]
    public async Task GetIdentification_ReturnsNonEmpty()
    {
        var id = await _client.Application.GetIdentificationAsync();

        Console.WriteLine($"Identification: '{id}'");

        Assert.IsNotNull(id);
        Assert.IsFalse(string.IsNullOrWhiteSpace(id), "Identification should not be empty");
    }

    [TestMethod]
    public async Task GetStatus_ReturnsValidEnum()
    {
        var status = await _client.Application.GetStatusAsync();

        Console.WriteLine($"Application status: {status}");

        Assert.IsTrue(Enum.IsDefined(typeof(ModuleStatus), status),
            $"Status '{status}' is not a defined ModuleStatus value");
    }

    [TestMethod]
    public async Task ListConfigFiles_ReturnsArray()
    {
        var files = await _client.Application.ListConfigFilesAsync();

        Console.WriteLine($"Config files count: {files?.Length}");
        foreach (var f in files!)
            Console.WriteLine($"  File: {f}");

        Assert.IsNotNull(files);
        // May be empty, but should not be null
    }

    [TestMethod]
    public async Task GetLoadedConfigFiles_ReturnsList()
    {
        var files = await _client.Application.GetLoadedConfigFilesAsync();

        Console.WriteLine($"Loaded config files count: {files?.Count}");
        foreach (var f in files!)
            Console.WriteLine($"  Loaded: {f}");

        Assert.IsNotNull(files);
    }
}
