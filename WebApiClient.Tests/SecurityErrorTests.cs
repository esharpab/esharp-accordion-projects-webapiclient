using System.Net;
using AccordionQ2.WebApiClient.Tests.Fakes;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AccordionQ2.WebApiClient.Tests;

/// <summary>
/// 401 and 403 from a station in security mode On become <see cref="AccordionQ2SecurityException"/> naming the
/// cause, with bodies as the WebApi's SecurityMiddleware writes them. The key never appears in the exception.
/// </summary>
[TestClass]
public class SecurityErrorTests
{
    private const string Key = "acc_SECRETSECRETSECRET";

    private const string Body401NoKey =
        "{\"error\":\"This station needs an API key in X-Api-Key (no API key was sent)\",\"requires\":\"Viewer\"}";
    private const string Body401Unknown =
        "{\"error\":\"This station needs an API key in X-Api-Key (an API key this station doesn't know was sent)\",\"requires\":\"Operator\"}";
    private const string Body403 =
        "{\"error\":\"This needs the Admin role; key 'line-3' is Operator\",\"requires\":\"Admin\"}";

    private static AccordionQ2Client Create(string? key, HttpStatusCode status, string body)
    {
        var handler = new RecordingHandler { Respond = _ => RecordingHandler.Json(status, body) };
        return new AccordionQ2Client("http://station:5000", new AccordionQ2ClientOptions { ApiKey = key, Handler = handler });
    }

    private static void AssertKeyless(Exception ex)
    {
        Assert.DoesNotContain(Key, ex.Message);
        Assert.DoesNotContain(Key, ex.ToString());
    }

    [TestMethod]
    public async Task Unauthorized_WithoutKey_IsMissingApiKey()
    {
        using var client = Create(null, HttpStatusCode.Unauthorized, Body401NoKey);
        var ex = await Assert.ThrowsExactlyAsync<AccordionQ2SecurityException>(() => client.Capabilities.GetAsync());
        Assert.AreEqual(401, ex.StatusCode);
        Assert.AreEqual(AccordionQ2SecurityReason.MissingApiKey, ex.Reason);
        Assert.AreEqual("Viewer", ex.RequiredRole);
        Assert.Contains("has none", ex.Message);
        StringAssert.EndsWith(ex.Message, "(station: This station needs an API key in X-Api-Key (no API key was sent))");
    }

    [TestMethod]
    public async Task Unauthorized_KeepsTheServersReason()
    {
        // A 401 needn't be the station's: here a proxy in front of it refuses the request.
        using var client = Create(null, HttpStatusCode.Unauthorized, "{\"error\":\"Proxy: Basic credentials rejected\"}");
        var ex = await Assert.ThrowsExactlyAsync<AccordionQ2SecurityException>(() => client.Capabilities.GetAsync());
        Assert.Contains("(station: Proxy: Basic credentials rejected)", ex.Message);
    }

    [TestMethod]
    public async Task Unauthorized_TextWithTheKey_IsWithheld()
    {
        using var client = Create(Key, HttpStatusCode.Unauthorized, "{\"error\":\"unknown key " + Key + "\"}");
        var ex = await Assert.ThrowsExactlyAsync<AccordionQ2SecurityException>(() => client.Capabilities.GetAsync());
        Assert.Contains("(station: (withheld: it contained the API key))", ex.Message);
        AssertKeyless(ex);
    }

    [TestMethod]
    public async Task Unauthorized_WithKey_IsInvalidApiKey()
    {
        using var client = Create(Key, HttpStatusCode.Unauthorized, Body401Unknown);
        var ex = await Assert.ThrowsExactlyAsync<AccordionQ2SecurityException>(() => client.Resources.GetNamesAsync());
        Assert.AreEqual(AccordionQ2SecurityReason.InvalidApiKey, ex.Reason);
        Assert.Contains("doesn't accept", ex.Message);
        AssertKeyless(ex);
    }

    [TestMethod]
    public async Task Forbidden_WithRequires_IsRoleTooLow()
    {
        using var client = Create(Key, HttpStatusCode.Forbidden, Body403);
        var ex = await Assert.ThrowsExactlyAsync<AccordionQ2SecurityException>(() => client.System.RebootAsync());
        Assert.AreEqual(403, ex.StatusCode);
        Assert.AreEqual(AccordionQ2SecurityReason.RoleTooLow, ex.Reason);
        Assert.AreEqual("Admin", ex.RequiredRole);
        Assert.Contains("Admin role", ex.Message);
        Assert.Contains("line-3", ex.Message, "the server's text names the key, not the secret, so it is kept");
        AssertKeyless(ex);
    }

    [TestMethod]
    public async Task Forbidden_WithoutRequires_StaysAPlainApiException()
    {
        // The Files endpoints answer 403 for a change in a read-only folder; that isn't about the key.
        using var client = Create(Key, HttpStatusCode.Forbidden, "{\"error\":\"The folder 'logs' is read-only\"}");
        var ex = await Assert.ThrowsExactlyAsync<AccordionQ2ApiException>(() => client.Files.DeleteAsync("logs", "a.log"));
        Assert.AreEqual(403, ex.StatusCode);
        Assert.AreEqual("The folder 'logs' is read-only", ex.Message);
    }

    [TestMethod]
    public async Task ServerTextThatRepeatsTheKey_IsWithheld()
    {
        using var client = Create(Key, HttpStatusCode.Forbidden, $"{{\"error\":\"key {Key} is Viewer\",\"requires\":\"Admin\"}}");
        var ex = await Assert.ThrowsExactlyAsync<AccordionQ2SecurityException>(() => client.Capabilities.GetAsync());
        AssertKeyless(ex);
    }

    [TestMethod]
    public async Task OtherErrors_AreUnchanged()
    {
        using var client = Create(Key, HttpStatusCode.Conflict, "{\"error\":\"The lease is held by bench-2\"}");
        var ex = await Assert.ThrowsExactlyAsync<AccordionQ2ApiException>(() => client.Lease.AcquireAsync("tests"));
        Assert.AreEqual(409, ex.StatusCode);
        Assert.AreEqual("The lease is held by bench-2", ex.Message);
    }

    [TestMethod]
    public async Task EventStream_Unauthorized_IsASecurityException()
    {
        using var client = Create(Key, HttpStatusCode.Unauthorized, Body401Unknown);
        var ex = await Assert.ThrowsExactlyAsync<AccordionQ2SecurityException>(() =>
            client.Events.ListenAsync(_ => Task.CompletedTask));
        Assert.AreEqual(AccordionQ2SecurityReason.InvalidApiKey, ex.Reason);
        AssertKeyless(ex);
    }

    [TestMethod]
    public async Task EventStream_Forbidden_IsRoleTooLow()
    {
        using var client = Create(Key, HttpStatusCode.Forbidden, Body403.Replace("Admin", "Viewer"));
        var ex = await Assert.ThrowsExactlyAsync<AccordionQ2SecurityException>(() =>
            client.Events.ListenAsync(_ => Task.CompletedTask));
        Assert.AreEqual(AccordionQ2SecurityReason.RoleTooLow, ex.Reason);
        Assert.AreEqual("Viewer", ex.RequiredRole);
    }

    [TestMethod]
    public async Task EventStream_NotFound_KeepsItsMessage()
    {
        using var client = Create(Key, HttpStatusCode.NotFound, "");
        var ex = await Assert.ThrowsExactlyAsync<AccordionQ2ApiException>(() =>
            client.Events.ListenAsync(_ => Task.CompletedTask));
        Assert.AreEqual(404, ex.StatusCode);
        Assert.AreEqual("The event stream couldn't be opened: HTTP 404", ex.Message);
    }

    [TestMethod]
    public void Capabilities_ReadSecurityMode()
    {
        var dto = Newtonsoft.Json.JsonConvert.DeserializeObject<Models.CapabilitiesDto>(
            "{\"apiVersion\":7,\"features\":[\"events\",\"security.apikey\"],\"securityMode\":\"Report\"}")!;
        Assert.AreEqual("Report", dto.SecurityMode);
        Assert.IsTrue(dto.Has(Models.CapabilitiesDto.SecurityApiKey));
        Assert.AreEqual("Off", Models.CapabilitiesDto.None.SecurityMode);
    }
}
