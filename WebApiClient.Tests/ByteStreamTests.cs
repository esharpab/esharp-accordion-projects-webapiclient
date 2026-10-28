using System.Net;
using System.Text;
using AccordionQ2.WebApiClient.Tests.Fakes;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AccordionQ2.WebApiClient.Tests;

/// <summary>The FPGA image upload (POST/GET /api/channels/bytestream). No network: a fake handler answers.</summary>
[TestClass]
public class ByteStreamTests
{
    private const string Url = "http://station:5000";
    private const string Channel = "1.MicroHAPS.JTAG.FPGA_IMAGE";

    private static (AccordionQ2Client Client, RecordingHandler Handler) Create(string? key = null)
    {
        var handler = new RecordingHandler();
        return (new AccordionQ2Client(Url, new AccordionQ2ClientOptions { ApiKey = key, Handler = handler }), handler);
    }

    [TestMethod]
    public async Task Upload_PostsTheStreamAsOctets_WithNameFileAndTarget()
    {
        var (client, handler) = Create("acc_0123456789abcdefTESTKEY");
        handler.Respond = _ => RecordingHandler.Json(HttpStatusCode.Accepted,
            "{\"state\":\"loading\",\"progress\":0,\"targets\":[\"sram\"],\"image\":{\"fileName\":\"top.bit\",\"size\":3,\"sha256\":\"ab\"}}");
        using (client)
        {
            var status = await client.Channels.UploadByteStreamAsync(Channel, new MemoryStream([1, 2, 3]), "top.bit");
            Assert.AreEqual("loading", status.State);
            Assert.IsTrue(status.IsRunning);
            Assert.AreEqual("top.bit", status.Image!.FileName);
        }
        var r = handler.Last;
        Assert.AreEqual(HttpMethod.Post, r.Method);
        Assert.AreEqual("/api/channels/bytestream", r.Uri!.AbsolutePath);
        StringAssert.Contains(r.Uri.Query, "name=1.MicroHAPS.JTAG.FPGA_IMAGE");
        StringAssert.Contains(r.Uri.Query, "fileName=top.bit");
        StringAssert.Contains(r.Uri.Query, "target=sram");
        Assert.IsFalse(r.Uri.Query.Contains("force"));
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, r.Body);
        Assert.AreEqual("acc_0123456789abcdefTESTKEY", r.Header("X-Api-Key"));
    }

    [TestMethod]
    public async Task Upload_FromAPath_SendsTheFileNameOnly_AndForce()
    {
        var path = Path.Combine(Path.GetTempPath(), $"bs-{Guid.NewGuid():N}.bit");
        await File.WriteAllBytesAsync(path, Encoding.ASCII.GetBytes("bits"));
        try
        {
            var (client, handler) = Create();
            handler.Respond = _ => RecordingHandler.Json(HttpStatusCode.Accepted, "{\"state\":\"loading\"}");
            using (client)
                await client.Channels.UploadByteStreamAsync(Channel, path, force: true);
            StringAssert.Contains(handler.Last.Uri!.Query, "fileName=" + Path.GetFileName(path));
            StringAssert.Contains(handler.Last.Uri.Query, "force=true");
            CollectionAssert.AreEqual(Encoding.ASCII.GetBytes("bits"), handler.Last.Body);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public async Task Upload_Refused_ThrowsWithTheStationsText()
    {
        var (client, handler) = Create();
        handler.Respond = _ => RecordingHandler.Json(HttpStatusCode.Conflict,
            "{\"error\":\"An FPGA image load is running (top.bit, 42 %); try again when it has ended\"}");
        using (client)
        {
            var ex = await Assert.ThrowsExactlyAsync<AccordionQ2ApiException>(
                () => client.Channels.UploadByteStreamAsync(Channel, new MemoryStream([1]), "x.bit"));
            Assert.AreEqual(409, ex.StatusCode);
            StringAssert.Contains(ex.Message, "An FPGA image load is running");
        }
    }

    [TestMethod]
    public async Task Wait_PollsUntilTheLoadHasEnded()
    {
        var (client, handler) = Create();
        var answers = new Queue<string>(["{\"state\":\"loading\",\"progress\":42}", "{\"state\":\"waitingDone\",\"progress\":100}",
                                         "{\"state\":\"done\",\"progress\":100,\"message\":\"Loaded\",\"image\":{\"result\":\"ok\",\"loaded\":true}}"]);
        handler.Respond = _ => RecordingHandler.Json(HttpStatusCode.OK, answers.Dequeue());
        using (client)
        {
            var status = await client.Channels.WaitByteStreamAsync(Channel, TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(1));
            Assert.AreEqual("done", status.State);
            Assert.AreEqual("ok", status.Image!.Result);
            Assert.IsTrue(status.Image.Loaded);
        }
        Assert.AreEqual(3, handler.Requests.Count);
        Assert.IsTrue(handler.Requests.All(r => r.Method == HttpMethod.Get && r.Uri!.Query.Contains("name=1.MicroHAPS.JTAG.FPGA_IMAGE")));
    }

    [TestMethod]
    public async Task Wait_TimesOut()
    {
        var (client, handler) = Create();
        handler.Respond = _ => RecordingHandler.Json(HttpStatusCode.OK, "{\"state\":\"loading\",\"progress\":5}");
        using (client)
            await Assert.ThrowsExactlyAsync<TimeoutException>(
                () => client.Channels.WaitByteStreamAsync(Channel, TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(5)));
    }
}
