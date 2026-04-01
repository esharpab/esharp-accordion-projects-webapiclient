using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AccordionQ2.WebApiClient.Tests;

[TestClass]
[TestCategory("Integration")]
public class ConnectionTests
{

    [TestMethod]
    public async Task GetStatus_ReturnsConnected()
    {
        var status = await TestSetup.Client.Connection.GetStatusAsync();

        Console.WriteLine($"IsConnected: {status.IsConnected}");
        Console.WriteLine($"LastError: {status.LastError ?? "(none)"}");

        Assert.IsTrue(status.IsConnected, "API should be connected to the hardware manager");
        Assert.IsNull(status.LastError, $"Unexpected connection error: {status.LastError}");
    }
}
