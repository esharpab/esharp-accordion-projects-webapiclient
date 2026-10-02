using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AccordionQ2.WebApiClient.Tests;

[TestClass]
[TestCategory("Integration")]
public class InstrumentsTests
{
    [TestMethod]
    public async Task GetAll_ReturnsInstrumentsWithFunctionMaps()
    {
        var instruments = await TestSetup.Client.Instruments.GetAllAsync();

        Console.WriteLine($"Instruments count: {instruments.Count}");
        foreach (var i in instruments)
            Console.WriteLine($"  {i.NetName} | {i.Type} | {string.Join(", ", i.FunctionMap.Keys)}");

        Assert.IsNotNull(instruments);
        foreach (var i in instruments)
        {
            Assert.IsFalse(string.IsNullOrEmpty(i.NetName), "Every instrument has a net name");
            Assert.IsFalse(string.IsNullOrEmpty(i.Type), $"{i.NetName} has a type");
        }
    }
}
