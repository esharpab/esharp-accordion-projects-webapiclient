using AccordionQ2.WebApiClient.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AccordionQ2.WebApiClient.Tests;

[TestClass]
[TestCategory("Integration")]
public class ChannelsTests
{

    [TestMethod]
    public async Task GetAll_ReturnsNonEmptyList()
    {
        var channels = await TestSetup.Client.Channels.GetAllAsync();

        Console.WriteLine($"Channels count: {channels?.Count}");
        foreach (var ch in channels!)
            Console.WriteLine($"  Ch {ch.ChannelIndex}: Alias={ch.Alias} | Type={ch.ChannelType} | Unit={ch.Unit}");

        Assert.IsNotNull(channels);
        Assert.IsTrue(channels.Count > 0, "Expected at least one channel");
    }

    [TestMethod]
    public async Task GetAll_ChannelsHaveAliases()
    {
        var channels = await TestSetup.Client.Channels.GetAllAsync();

        foreach (var ch in channels)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(ch.Alias),
                $"Channel index {ch.ChannelIndex} has no alias");
        }
    }

    [TestMethod]
    public async Task GetChannel_ByAlias_AnalogChannel_ReturnsCorrectType()
    {
        var ch = await TestSetup.Client.Channels.GetChannelAsync(alias: TestConfig.AnalogChannelAlias);

        Console.WriteLine($"Analog channel: Alias={ch?.Alias} | Type={ch?.ChannelType} | Unit={ch?.Unit}");

        Assert.IsNotNull(ch);
        Assert.AreEqual(TestConfig.AnalogChannelAlias, ch.Alias);
        Assert.IsTrue(ch.ChannelType.HasFlag(ChannelTypes.Analog),
            $"Expected Analog channel type, got {ch.ChannelType}");
        Assert.AreEqual("V", ch.Unit, "Analog voltage channel should have unit 'V'");
    }

    [TestMethod]
    public async Task GetChannel_ByAlias_AdcChannel_ReturnsCorrectType()
    {
        var ch = await TestSetup.Client.Channels.GetChannelAsync(alias: TestConfig.AdcChannelAlias);

        Console.WriteLine($"ADC channel: Alias={ch?.Alias} | Type={ch?.ChannelType} | Unit={ch?.Unit}");

        Assert.IsNotNull(ch);
        Assert.AreEqual(TestConfig.AdcChannelAlias, ch.Alias);
        Assert.IsTrue(ch.ChannelType.HasFlag(ChannelTypes.Analog),
            $"ADC channel should be Analog, got {ch.ChannelType}");
    }

    [TestMethod]
    public async Task GetChannel_ByAlias_I2cChannel_ReturnsCorrectType()
    {
        var ch = await TestSetup.Client.Channels.GetChannelAsync(alias: TestConfig.I2cChannelAlias);

        Console.WriteLine($"I2C channel: Alias={ch?.Alias} | Type={ch?.ChannelType} | Unit={ch?.Unit}");

        Assert.IsNotNull(ch);
        Assert.AreEqual(TestConfig.I2cChannelAlias, ch.Alias);
        Assert.IsTrue(ch.ChannelType.HasFlag(ChannelTypes.I2C),
            $"Expected I2C channel type, got {ch.ChannelType}");
    }

    [TestMethod]
    public async Task GetChannel_InvalidAlias_ThrowsApiException()
    {
        await Assert.ThrowsExceptionAsync<AccordionQ2ApiException>(
            () => TestSetup.Client.Channels.GetChannelAsync(alias: "NonExistent.Channel.12345"));
    }
}
