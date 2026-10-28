using System.Globalization;
using System.Text;
using AccordionQ2.WebApiClient.Models;
using AccordionQ2.WebApiClient.Tests.Fakes;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AccordionQ2.WebApiClient.Tests;

/// <summary>
/// <see cref="ChannelConfigRequest.Value"/>: an output's value goes in the same configure as its direction and type
/// (firmware 7.0.5). No network: a fake handler records the body.
/// </summary>
[TestClass]
public class ChannelConfigValueTests
{
    private static (AccordionQ2Client Client, RecordingHandler Handler) Create()
    {
        var handler = new RecordingHandler();
        return (new AccordionQ2Client("http://station:5000", new AccordionQ2ClientOptions { Handler = handler }), handler);
    }

    private static string Body(RecordingHandler handler) => Encoding.UTF8.GetString(handler.Last.Body);

    [TestMethod]
    public async Task Value_IsSentWithTheDirection()
    {
        var (client, handler) = Create();
        using (client)
            await client.Channels.ConfigureAsync(new ChannelConfigRequest { NetName = "MPIO04", Direction = DirectionTypes.OUT, Value = "1.8" });

        var body = Body(handler);
        StringAssert.Contains(body, "\"Value\":\"1.8\"");
        StringAssert.Contains(body, "\"Direction\":");
        Assert.AreEqual("/api/channels/channel/configure", handler.Last.Uri!.AbsolutePath);
    }

    [TestMethod]
    public async Task NoValue_IsLeftOut()
    {
        var (client, handler) = Create();
        using (client)
            await client.Channels.ConfigureAsync(new ChannelConfigRequest { NetName = "MPIO04", Direction = DirectionTypes.OUT });

        Assert.IsFalse(Body(handler).Contains("\"Value\""), Body(handler));
    }

    [TestMethod]
    public async Task ConfigureMany_SendsEachValue()
    {
        var (client, handler) = Create();
        using (client)
            await client.Channels.ConfigureManyAsync(new List<ChannelConfigRequest>
            {
                new ChannelConfigRequest { NetName = "MPIO04", Direction = DirectionTypes.OUT }.WithValue(0.8),
                new ChannelConfigRequest { NetName = "MPIO05", Direction = DirectionTypes.OUT, ChannelType = ChannelTypes.PseudoDigital }.WithValue(false),
            });

        var body = Body(handler);
        StringAssert.Contains(body, "\"Value\":\"0.8\"");
        StringAssert.Contains(body, "\"Value\":\"False\"");
    }

    [TestMethod]
    public void WithValue_IsInvariant()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("sv-SE");
            Assert.AreEqual("1.25", new ChannelConfigRequest().WithValue(1.25).Value);
            Assert.AreEqual("True", new ChannelConfigRequest().WithValue(true).Value);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
