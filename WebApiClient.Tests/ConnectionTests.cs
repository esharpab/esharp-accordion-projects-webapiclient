using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AccordionQ2.WebApiClient.Tests;

[TestClass]
[TestCategory("Integration")]
public class ConnectionTests
{
    private static AccordionQ2Client _client = null!;

    [ClassInitialize]
    public static void ClassInit(TestContext _) =>
        _client = new AccordionQ2Client(TestConfig.BaseUrl);

    [ClassCleanup]
    public static void ClassCleanup() => _client?.Dispose();

    [TestMethod]
    public async Task GetStatus_ReturnsConnected()
    {
        var status = await _client.Connection.GetStatusAsync();

        Console.WriteLine($"IsConnected: {status.IsConnected}");
        Console.WriteLine($"LastError: {status.LastError ?? "(none)"}");

        Assert.IsTrue(status.IsConnected, "API should be connected to the hardware manager");
        Assert.IsNull(status.LastError, $"Unexpected connection error: {status.LastError}");
    }
}
