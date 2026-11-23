using System.Net;
using AccordionQ2.WebApiClient.Models;
using AccordionQ2.WebApiClient.Tests.Fakes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace AccordionQ2.WebApiClient.Tests;

/// <summary>
/// client.Security (firmware 7.1.0, FW7 CLI-04 follow-up): the routes and verbs of the WebApi's security controllers,
/// the login token in <c>Authorization: Bearer</c>, which credential each login call carries, and how a refused password
/// or token surfaces. The bodies against the server's request records: WebApi.Tests SecurityContractTests.
/// </summary>
[TestClass]
public class SecurityGroupTests
{
    // HTTPS: the calls that carry a secret don't go over plain HTTP to another host
    private const string Url = "https://station:5001";
    private const string Key = "acc_SECRETSECRETSECRET";
    private const string Token = "acs_TOKENTOKENTOKEN";

    private const string Login =
        "{\"token\":\"" + Token + "\",\"expiresAt\":\"2026-11-19T10:15:00+00:00\",\"user\":{\"id\":\"u1\",\"name\":\"ann\",\"role\":\"Admin\"," +
        "\"created\":\"2026-11-19T10:00:00+00:00\",\"createdBy\":\"claim\"},\"expiresIn\":900}";

    private static (AccordionQ2Client Client, RecordingHandler Handler) Create(string? key = null, Func<HttpRequestMessage, HttpResponseMessage>? respond = null,
        string url = Url)
    {
        var handler = new RecordingHandler();
        if (respond is not null)
            handler.Respond = respond;
        return (new AccordionQ2Client(url, new AccordionQ2ClientOptions { ApiKey = key, Handler = handler }), handler);
    }

    private static HttpResponseMessage Ok(string json) => RecordingHandler.Json(HttpStatusCode.OK, json);

    private static JObject Body(RecordingHandler.Recorded request) => JObject.Parse(System.Text.Encoding.UTF8.GetString(request.Body));

    [TestMethod]
    public async Task A_login_goes_without_credentials_and_its_token_rides_along_after()
    {
        var (client, handler) = Create(Key, _ => Ok(Login));
        using var _ = client;
        client.Security.Token = "acs_OLD";

        var login = await client.Security.LoginAsync("ann", "pw-123456789012");
        Assert.AreEqual(HttpMethod.Post, handler.Last.Method);
        Assert.AreEqual("/api/security/login", handler.Last.Uri!.AbsolutePath);
        Assert.IsNull(handler.Last.Header("X-Api-Key"), "a stale credential would only count as a failure");
        Assert.IsNull(handler.Last.Header("Authorization"));
        Assert.AreEqual("ann", (string?)Body(handler.Last)["name"]);
        Assert.AreEqual("Admin", login.User.Role);
        Assert.AreEqual(900, login.ExpiresIn);

        Assert.AreEqual(Token, client.Security.Token);
        var expires = client.Security.TokenExpiresAt!.Value;
        Assert.IsTrue(Math.Abs((expires - DateTimeOffset.UtcNow.AddSeconds(900)).TotalSeconds) < 30, "timed by expiresIn on this clock");

        await client.Security.GetMeAsync();
        Assert.AreEqual("Bearer " + Token, handler.Last.Header("Authorization"));
        Assert.AreEqual(Key, handler.Last.Header("X-Api-Key"), "both are sent; the station uses the key");
    }

    [TestMethod]
    public async Task A_wrong_password_is_named_as_such()
    {
        var (client, handler) = Create(null, _ => RecordingHandler.Json(HttpStatusCode.Unauthorized,
            "{\"error\":\"Wrong user name or password\",\"requires\":\"Anonymous\"}"));
        using var _ = client;
        var ex = await Assert.ThrowsExactlyAsync<AccordionQ2SecurityException>(() => client.Security.LoginAsync("ann", "nope"));
        Assert.AreEqual(AccordionQ2SecurityReason.WrongPassword, ex.Reason);
        Assert.AreEqual("Wrong user name or password", ex.Message);
        Assert.IsNull(client.Security.Token);
    }

    [TestMethod]
    public async Task Renew_logout_and_the_own_password_carry_the_token_only()
    {
        var (client, handler) = Create(Key, _ => Ok(Login.Replace(Token, "acs_NEW")));
        using var _ = client;
        client.Security.Token = Token;

        await client.Security.RenewAsync();
        Assert.AreEqual("/api/security/token/renew", handler.Last.Uri!.AbsolutePath);
        Assert.AreEqual("Bearer " + Token, handler.Last.Header("Authorization"));
        Assert.IsNull(handler.Last.Header("X-Api-Key"), "a key sent along would win, and renew would refuse it");
        Assert.AreEqual("acs_NEW", client.Security.Token);

        await client.Security.ChangeOwnPasswordAsync("old-password-1", "new-password-1");
        Assert.AreEqual(HttpMethod.Put, handler.Last.Method);
        Assert.AreEqual("/api/security/password", handler.Last.Uri!.AbsolutePath);
        Assert.IsNull(handler.Last.Header("X-Api-Key"));
        Assert.AreEqual("new-password-1", (string?)Body(handler.Last)["newPassword"]);

        handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.NoContent);
        await client.Security.LogoutAsync();
        Assert.AreEqual("/api/security/logout", handler.Last.Uri!.AbsolutePath);
        Assert.IsNull(handler.Last.Header("X-Api-Key"));
        Assert.IsNull(client.Security.Token);
        Assert.IsNull(client.Security.TokenExpiresAt);

        var count = handler.Requests.Count;
        await client.Security.LogoutAsync();
        Assert.HasCount(count, handler.Requests, "nothing to log out");
    }

    [TestMethod]
    public async Task Logout_forgets_the_token_even_when_the_station_cant_be_reached()
    {
        var (client, _) = Create(null, _ => throw new HttpRequestException("down"));
        using var _ = client;
        client.Security.Token = Token;
        await Assert.ThrowsAsync<HttpRequestException>(() => client.Security.LogoutAsync());
        Assert.IsNull(client.Security.Token);
    }

    [TestMethod]
    public async Task Renewing_or_changing_the_own_password_without_a_login_is_refused_here()
    {
        var (client, handler) = Create(Key);
        using var _ = client;
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => client.Security.RenewAsync());
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => client.Security.ChangeOwnPasswordAsync("a", "b"));
        Assert.IsEmpty(handler.Requests);
    }

    [TestMethod]
    public async Task A_wrong_current_password_is_not_a_dead_token()
    {
        var (client, handler) = Create(null, _ => RecordingHandler.Json(HttpStatusCode.Unauthorized,
            "{\"error\":\"The current password is wrong\",\"requires\":\"Anonymous\"}"));
        using var _ = client;
        client.Security.Token = Token;
        var wrong = await Assert.ThrowsExactlyAsync<AccordionQ2SecurityException>(() => client.Security.ChangeOwnPasswordAsync("x", "new-password-1"));
        Assert.AreEqual(AccordionQ2SecurityReason.WrongPassword, wrong.Reason);
        Assert.AreEqual(Token, client.Security.Token, "the token stays valid");

        // The middleware's 401 carries its challenge: the token itself was refused
        handler.Respond = _ =>
        {
            var response = RecordingHandler.Json(HttpStatusCode.Unauthorized,
                "{\"error\":\"This station needs an API key in X-Api-Key or a login (a login token that expired or isn't valid was sent)\",\"requires\":\"Viewer\"}");
            response.Headers.WwwAuthenticate.ParseAdd("Bearer realm=\"accordion\", error=\"invalid_token\"");
            return response;
        };
        var dead = await Assert.ThrowsExactlyAsync<AccordionQ2SecurityException>(() => client.Security.ChangeOwnPasswordAsync("x", "new-password-1"));
        Assert.AreEqual(AccordionQ2SecurityReason.InvalidToken, dead.Reason);
    }

    [TestMethod]
    public async Task A_refused_token_is_InvalidToken_and_never_shown()
    {
        var (client, _) = Create(null, _ => RecordingHandler.Json(HttpStatusCode.Unauthorized,
            "{\"error\":\"token " + Token + " expired\",\"requires\":\"Viewer\"}"));
        using var _ = client;
        client.Security.Token = Token;
        var ex = await Assert.ThrowsExactlyAsync<AccordionQ2SecurityException>(() => client.Resources.GetNamesAsync());
        Assert.AreEqual(AccordionQ2SecurityReason.InvalidToken, ex.Reason);
        Assert.Contains("log in again", ex.Message);
        Assert.Contains("withheld", ex.Message);
        Assert.DoesNotContain(Token, ex.ToString());
    }

    [TestMethod]
    public async Task A_login_below_the_role_says_login_not_key()
    {
        var (client, _) = Create(null, _ => RecordingHandler.Json(HttpStatusCode.Forbidden,
            "{\"error\":\"This needs the Admin role; user 'ann' is Viewer\",\"requires\":\"Admin\"}"));
        using var _ = client;
        client.Security.Token = Token;
        var ex = await Assert.ThrowsExactlyAsync<AccordionQ2SecurityException>(() => client.Security.GetStateAsync());
        Assert.AreEqual(AccordionQ2SecurityReason.RoleTooLow, ex.Reason);
        Assert.Contains("client's login doesn't have", ex.Message);
    }

    [TestMethod]
    public void A_token_a_header_cant_carry_is_refused_without_repeating_it()
    {
        var (client, _) = Create();
        using var _ = client;
        var ex = Assert.ThrowsExactly<ArgumentException>(() => client.Security.Token = "acs_bad token");
        Assert.DoesNotContain("acs_bad", ex.Message);
        Assert.ThrowsExactly<ArgumentException>(() => client.Security.Token = "");
    }

    [TestMethod]
    public async Task A_claim_goes_without_credentials_and_logs_in()
    {
        var (client, handler) = Create(Key, _ => RecordingHandler.Json(HttpStatusCode.Created, Login));
        using var _ = client;
        await client.Security.ClaimAsync("ABCD-EFGH", null, "pw-123456789012");
        Assert.AreEqual("/api/security/claim", handler.Last.Uri!.AbsolutePath);
        Assert.IsNull(handler.Last.Header("X-Api-Key"));
        Assert.AreEqual("ABCD-EFGH", (string?)Body(handler.Last)["setupCode"]);
        Assert.AreEqual(Token, client.Security.Token);
    }

    [TestMethod]
    public async Task A_claim_from_another_network_is_the_stations_reason_not_a_role()
    {
        // AccountsController fills requires with the endpoint's role, Anonymous for the claim: no role is missing
        var (client, _) = Create(null, _ => RecordingHandler.Json(HttpStatusCode.Forbidden,
            "{\"error\":\"A station is claimed from its own network segment only\",\"requires\":\"Anonymous\"}"));
        using var _ = client;
        var ex = await Assert.ThrowsExactlyAsync<AccordionQ2ApiException>(() => client.Security.ClaimAsync("ABCD-EFGH", null, "pw-123456789012"));
        Assert.AreEqual(403, ex.StatusCode);
        Assert.AreEqual("A station is claimed from its own network segment only", ex.Message);
    }

    [TestMethod]
    public async Task Secrets_never_leave_over_plain_HTTP_to_another_host()
    {
        var (client, handler) = Create(Key, _ => Ok(Login), "http://station:5000");
        using var _ = client;
        client.Security.Token = Token;
        var s = client.Security;
        Func<Task>[] calls =
        [
            () => s.LoginAsync("ann", "pw-123456789012"),
            () => s.ClaimAsync("ABCD-EFGH", null, "pw-123456789012"),
            () => s.ChangeOwnPasswordAsync("old-password-1", "new-password-1"),
            () => s.CreateUserAsync("bob", StationRole.Viewer, "pw-123456789012"),
            () => s.SetUserPasswordAsync("u1", "pw-123456789012"),
            () => s.InstallCertificateAsync(new InstallCertificateDto { CertificatePem = "C", PrivateKeyPem = "K" }),
            () => s.RecoveryResetAdminAsync(null, "pw-123456789012"),
        ];
        foreach (var call in calls)
        {
            var ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(call);
            Assert.Contains("https://station:5001", ex.Message);
            Assert.DoesNotContain("pw-123456789012", ex.Message);
        }
        Assert.IsEmpty(handler.Requests, "nothing crossed the network");
        Assert.AreEqual(Token, client.Security.Token);

        handler.Respond = _ => Ok("[]");
        await s.GetUsersAsync(); // a call without a secret goes as before
        Assert.HasCount(1, handler.Requests);
    }

    [TestMethod]
    public async Task On_the_station_plain_HTTP_carries_them()
    {
        foreach (var url in new[] { "http://localhost:5000", "http://127.0.0.1:5000", "http://[::1]:5000" })
        {
            var (client, handler) = Create(null, _ => Ok(Login), url);
            using var _ = client;
            await client.Security.LoginAsync("ann", "pw-123456789012");
            Assert.HasCount(1, handler.Requests, url);
        }
    }

    [TestMethod]
    public async Task Each_call_uses_its_controllers_route_and_verb()
    {
        var (client, handler) = Create(Key, _ => Ok("{}"));
        using var _ = client;
        var s = client.Security;

        async Task Is(Func<Task> call, string method, string pathAndQuery)
        {
            await call();
            Assert.AreEqual(method, handler.Last.Method.Method, pathAndQuery);
            Assert.AreEqual(pathAndQuery, handler.Last.Uri!.PathAndQuery);
            Assert.AreEqual(Key, handler.Last.Header("X-Api-Key"), pathAndQuery);
        }

        await Is(() => s.GetMeAsync(), "GET", "/api/security/me");
        handler.Respond = _ => Ok("[]");
        await Is(() => s.GetUsersAsync(), "GET", "/api/security/users");
        handler.Respond = _ => Ok("{}");
        await Is(() => s.CreateUserAsync("bob", StationRole.Viewer, "pw-123456789012"), "POST", "/api/security/users");
        await Is(() => s.SetUserPasswordAsync("u 1", "pw-123456789012"), "PUT", "/api/security/users/u%201/password");
        await Is(() => s.SetUserRoleAsync("u1", StationRole.Operator), "PUT", "/api/security/users/u1/role");
        await Is(() => s.RemoveUserAsync("u1"), "DELETE", "/api/security/users/u1");
        await Is(() => s.GetStateAsync(), "GET", "/api/security");
        await Is(() => s.SetModeAsync(SecurityMode.Report), "PUT", "/api/security/mode");
        await Is(() => s.CreateKeyAsync("assert", StationRole.Operator), "POST", "/api/security/keys");
        await Is(() => s.RevokeKeyAsync("1a2b3c4d"), "DELETE", "/api/security/keys/1a2b3c4d");
        await Is(() => s.GetCertificateAsync(), "GET", "/api/security/certificate");
        await Is(() => s.InstallCertificateAsync(new InstallCertificateDto { PfxBase64 = "UEZY" }), "PUT", "/api/security/certificate");
        await Is(() => s.RemoveCertificateAsync(), "DELETE", "/api/security/certificate");
        await Is(() => s.RegenerateCertificateAsync(), "POST", "/api/security/certificate/regenerate");
        await Is(() => s.RequestCertificateAsync(["a.example"]), "POST", "/api/security/certificate/request");
        await Is(() => s.GetCertificateRequestAsync(), "GET", "/api/security/certificate/request");
        await Is(() => s.DeleteCertificateRequestAsync(), "DELETE", "/api/security/certificate/request");
        await Is(() => s.InstallSignedCertificateAsync("LEAF"), "PUT", "/api/security/certificate");
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => s.InstallSignedCertificateAsync(" "));
        await Is(() => s.GetHostAsync(), "GET", "/api/security/host");
        await Is(() => s.SetHostAsync(new HostSettingsDto { XvcPorts = [2542] }), "PUT", "/api/security/host");
        await Is(() => s.GetExtensionsAsync(), "GET", "/api/security/extensions");
        await Is(() => s.AcceptExtensionAsync("", "sha256:00"), "POST", "/api/security/extensions/accept");
        await Is(() => s.WithdrawExtensionAsync("Vendor Psu"), "DELETE", "/api/security/extensions/accept?folder=Vendor%20Psu");
        await Is(() => s.WithdrawExtensionAsync(""), "DELETE", "/api/security/extensions/accept?folder=");
        await Is(() => s.SetExtensionUploadsAsync(30), "PUT", "/api/security/extension-uploads");
        await Is(() => s.SetModuleImageRoleAsync(StationRole.Operator), "PUT", "/api/security/module-images");
        await Is(() => s.GetFactoryResetPreviewAsync(), "GET", "/api/security/factory-reset/preview");
        await Is(() => s.StartFactoryResetAsync("A004166", true), "POST", "/api/security/factory-reset");
        await Is(() => s.GetLastFactoryResetAsync(), "GET", "/api/security/factory-reset");
        await Is(() => s.GetRecoveryAsync(), "GET", "/api/security/recovery");
        await Is(() => s.DownloadRecoveryDriveFileAsync(), "GET", "/api/security/recovery/drive-file");
        await Is(() => s.GetEventsAsync(), "GET", "/api/security/events?tail=200");
        await Is(() => s.GetEventsAsync(50, new DateTimeOffset(2026, 11, 19, 10, 0, 0, TimeSpan.FromHours(1)), ["LoginFailed", "Throttled"]),
            "GET", "/api/security/events?tail=50&since=2026-11-19T10%3A00%3A00.0000000%2B01%3A00&kind=LoginFailed&kind=Throttled");
    }

    [TestMethod]
    public async Task Enums_go_as_the_stations_names()
    {
        var (client, handler) = Create(Key, _ => Ok("{}"));
        using var _ = client;
        await client.Security.SetModeAsync(SecurityMode.On);
        Assert.AreEqual("On", (string?)Body(handler.Last)["mode"]);
        await client.Security.CreateUserAsync("bob", StationRole.Operator, "pw");
        Assert.AreEqual("Operator", (string?)Body(handler.Last)["role"]);
    }

    [TestMethod]
    public async Task The_recovery_drive_reset_goes_without_credentials_and_reads_the_window()
    {
        // Firmware 7.1.0 RecoveryController: anonymous; the reset never rides on a key or token, and doesn't log in
        var (client, handler) = Create(Key, _ => Ok(
            "{\"user\":{\"id\":\"u1\",\"name\":\"admin\",\"role\":\"Admin\",\"created\":\"2026-11-19T10:00:00+00:00\",\"createdBy\":\"claim\"},\"created\":false,\"message\":\"Sign in\"}"));
        using var _ = client;
        client.Security.Token = Token;
        var reset = await client.Security.RecoveryResetAdminAsync(null, "pw-123456789012");
        Assert.AreEqual(HttpMethod.Post, handler.Last.Method);
        Assert.AreEqual("/api/security/recovery/reset-admin", handler.Last.Uri!.AbsolutePath);
        Assert.IsNull(handler.Last.Header("X-Api-Key"));
        Assert.IsNull(handler.Last.Header("Authorization"));
        Assert.AreEqual("pw-123456789012", (string?)Body(handler.Last)["password"]);
        Assert.AreEqual("admin", reset.User.Name);
        Assert.IsFalse(reset.Created);
        Assert.AreEqual(Token, client.Security.Token, "no login: the caller signs in with the new password");

        handler.Respond = _ => Ok("{\"window\":\"open\",\"until\":\"2026-11-19T10:15:00+00:00\",\"usbPortsOff\":true,\"hint\":\"h\",\"steps\":[\"a\",\"b\"]}");
        var window = await client.Security.GetRecoveryAsync();
        Assert.IsTrue(window!.IsOpen);
        Assert.IsTrue(window.UsbPortsOff);
        Assert.AreEqual(new DateTimeOffset(2026, 11, 19, 10, 15, 0, TimeSpan.Zero), window.Until);
        Assert.HasCount(2, window.Steps);

        handler.Respond = _ => RecordingHandler.Json(HttpStatusCode.NotFound, "{}");
        Assert.IsNull(await client.Security.GetRecoveryAsync(), "a station before 7.1.0");

        handler.Respond = _ => RecordingHandler.Json(HttpStatusCode.Conflict,
            "{\"error\":\"No recovery drive is in the station: insert the recovery drive\",\"requires\":\"Anonymous\"}");
        var ex = await Assert.ThrowsExactlyAsync<AccordionQ2ApiException>(() => client.Security.RecoveryResetAdminAsync("admin", "pw-123456789012"));
        Assert.AreEqual(409, ex.StatusCode);
        Assert.Contains("insert the recovery drive", ex.Message);
    }

    [TestMethod]
    public async Task No_reset_record_is_null_not_an_error()
    {
        var (client, _) = Create(Key, _ => RecordingHandler.Json(HttpStatusCode.NotFound,
            "{\"error\":\"This station has no record of a factory reset\",\"requires\":\"Admin\"}"));
        using var _ = client;
        Assert.IsNull(await client.Security.GetLastFactoryResetAsync());
    }

    [TestMethod]
    public async Task Answers_read_whole()
    {
        var (client, _) = Create(Key, _ => Ok(
            "{\"mode\":\"On\",\"keys\":[{\"id\":\"1a\",\"name\":\"assert\",\"role\":\"Operator\",\"created\":\"2026-11-19T10:00:00+00:00\",\"createdBy\":\"x\",\"issuedOver\":\"Https\"}]," +
            "\"claimed\":true,\"throttled\":[{\"client\":\"10.0.0.9\",\"purpose\":\"Password\",\"failures\":6,\"until\":\"2026-11-19T10:05:00+00:00\"}]," +
            "\"moduleImageRole\":\"Operator\",\"extensionCheck\":{\"release\":\"7.1.0\",\"failed\":[\"Bad\"],\"unsigned\":[]},\"futureField\":1}"));
        using var _ = client;
        var state = await client.Security.GetStateAsync();
        Assert.AreEqual("On", state.Mode);
        Assert.AreEqual("Https", state.Keys[0].IssuedOver);
        Assert.AreEqual(6, state.Throttled![0].Failures);
        Assert.AreEqual("Bad", state.ExtensionCheck!.Failed[0]);
    }
}
