using System.Net;
using AccordionQ2.WebApiClient.Tests.Fakes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace AccordionQ2.WebApiClient.Tests;

/// <summary>
/// The recovery drive made by the client (firmware 7.1.0): the hash's encoding (the vector the station, the Web UI and the
/// Python client test too), the file (no secret), the key kept in the client and saved to a file, and a reset without a
/// key never sent.
/// </summary>
[TestClass]
public class RecoveryDriveTests
{
    /// <summary>The shared vector: secret 00 01 .. 1f, station "a002531" (upper-cased), created 2026-11-24T08:00:00Z.</summary>
    internal const string VectorHash = "c99869a7262e1fc9bf8806c9dde1ee147b87cf24ac8668913e7e934bb8227d2b";

    private static JObject Parse(string json) =>
        Newtonsoft.Json.JsonConvert.DeserializeObject<JObject>(json, new Newtonsoft.Json.JsonSerializerSettings { DateParseHandling = Newtonsoft.Json.DateParseHandling.None })!;

    [TestMethod]
    public void The_hash_is_SHA256_of_secret_0_station_upper_case_0_created()
    {
        var secret = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
        Assert.AreEqual(VectorHash, RecoveryDrive.Hash(secret, "a002531", "2026-11-24T08:00:00Z"));
        Assert.AreEqual(VectorHash, RecoveryDrive.Hash(secret, " A002531 ", "2026-11-24T08:00:00Z"));
        Assert.AreNotEqual(VectorHash, RecoveryDrive.Hash(secret, "A002531", "2026-11-24T08:00:01Z"));
        Assert.AreEqual("2026-11-24T08:00:00Z", RecoveryDrive.FormatCreated(new DateTimeOffset(2026, 11, 24, 9, 0, 0, 123, TimeSpan.FromHours(1))));
    }

    [TestMethod]
    public void A_drive_holds_the_station_its_time_and_hash_and_the_client_keeps_the_secret()
    {
        var client = new AccordionQ2Client("https://station:5001");
        using var _ = client;
        var bytes = client.Security.MakeRecoveryDrive("A002531", new DateTimeOffset(2026, 11, 24, 8, 0, 0, TimeSpan.Zero));
        var text = System.Text.Encoding.UTF8.GetString(bytes);
        StringAssert.EndsWith(text, "}\n");
        var file = Parse(text);
        CollectionAssert.AreEqual(new[] { "kind", "station", "created", "hash" }, file.Properties().Select(p => p.Name).ToArray());
        Assert.AreEqual("accordion-recovery", (string?)file["kind"]);
        Assert.AreEqual("A002531", (string?)file["station"]);
        Assert.AreEqual("2026-11-24T08:00:00Z", (string?)file["created"]);
        var key = client.Security.RecoveryKey!;
        Assert.AreEqual(64, key.Secret.Length);
        Assert.DoesNotContain(key.Secret, text, "never the secret");
        var secret = Enumerable.Range(0, 32).Select(i => Convert.ToByte(key.Secret.Substring(2 * i, 2), 16)).ToArray();
        Assert.AreEqual((string?)file["hash"], RecoveryDrive.Hash(secret, "A002531", "2026-11-24T08:00:00Z"));
        client.Security.MakeRecoveryDrive("A002531");
        Assert.AreNotEqual(key.Secret, client.Security.RecoveryKey!.Secret, "a new drive gets a new secret");
    }

    [TestMethod]
    public async Task The_key_saves_and_loads_and_a_reset_without_one_isnt_sent()
    {
        var path = Path.Combine(Path.GetTempPath(), $"recovery-key-{Guid.NewGuid():N}.json");
        try
        {
            var handler = new RecordingHandler { Respond = _ => RecordingHandler.Json(HttpStatusCode.OK, "{\"user\":{\"name\":\"admin\"},\"created\":false,\"message\":\"m\"}") };
            var maker = new AccordionQ2Client("https://station:5001", new AccordionQ2ClientOptions { Handler = handler });
            using var a = maker;
            maker.Security.MakeRecoveryDrive("A002531");
            maker.Security.SaveRecoveryKey(path);
            var saved = Parse(File.ReadAllText(path));
            Assert.AreEqual("accordion-recovery-key", (string?)saved["kind"]);
            Assert.AreEqual(maker.Security.RecoveryKey!.Secret, (string?)saved["secret"]);

            var other = new AccordionQ2Client("https://station:5001", new AccordionQ2ClientOptions { Handler = handler });
            using var b = other;
            var ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => other.Security.RecoveryResetAdminAsync(null, "pw-123456789012"));
            Assert.Contains("MakeRecoveryDrive", ex.Message);
            Assert.AreEqual(0, handler.Requests.Count, "nothing sent without a key");
            Assert.ThrowsExactly<InvalidOperationException>(() => other.Security.SaveRecoveryKey(path));

            other.Security.LoadRecoveryKey(path);
            await other.Security.RecoveryResetAdminAsync(null, "pw-123456789012");
            var body = Parse(System.Text.Encoding.UTF8.GetString(handler.Last.Body));
            Assert.AreEqual(maker.Security.RecoveryKey.Secret, (string?)body["secret"]);

            File.WriteAllText(path, "{\"kind\":\"accordion-recovery-key\",\"station\":\"A002531\",\"created\":\"x\",\"secret\":\"zz\"}");
            Assert.ThrowsExactly<FormatException>(() => other.Security.LoadRecoveryKey(path));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
