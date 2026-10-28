using System.Net;
using AccordionQ2.WebApiClient.Tests.Fakes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AccordionQ2.WebApiClient.Tests;

/// <summary>
/// A redirect must not carry the API key or the lease id to another host: .NET drops only Authorization on a
/// redirect. Two local Kestrel servers on 127.0.0.1 (different ports, so different origins); no other host.
/// </summary>
[TestClass]
public class RedirectTests
{
    private const string Key = "acc_redirect_test_key";

    private sealed class LocalServer : IAsyncDisposable
    {
        private readonly WebApplication _app;
        public int Requests;
        public string? ReceivedKey;

        private LocalServer(WebApplication app) => _app = app;

        public string BaseUrl { get; private set; } = "";

        /// <summary>Answers every request: 302 to <paramref name="redirectTo"/> when given, else 200 with capabilities.</summary>
        public static async Task<LocalServer> StartAsync(string? redirectTo)
        {
            var builder = WebApplication.CreateSlimBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.ConfigureKestrel(k => k.Listen(IPAddress.Loopback, 0));
            var app = builder.Build();
            var server = new LocalServer(app);
            app.Run(context =>
            {
                Interlocked.Increment(ref server.Requests);
                server.ReceivedKey ??= context.Request.Headers["X-Api-Key"].FirstOrDefault();
                if (redirectTo is not null)
                {
                    context.Response.StatusCode = StatusCodes.Status302Found;
                    context.Response.Headers.Location = redirectTo + context.Request.Path;
                    return Task.CompletedTask;
                }
                context.Response.ContentType = "application/json";
                return context.Response.WriteAsync("{\"apiVersion\":7,\"features\":[],\"securityMode\":\"On\"}");
            });
            await app.StartAsync();
            server.BaseUrl = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            return server;
        }

        public async ValueTask DisposeAsync()
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }

    [TestMethod]
    public async Task RedirectToAnotherHost_IsNotFollowed_AndTheKeyStaysHere()
    {
        await using var elsewhere = await LocalServer.StartAsync(redirectTo: null);
        await using var station = await LocalServer.StartAsync(redirectTo: elsewhere.BaseUrl);
        using var client = new AccordionQ2Client(station.BaseUrl, new AccordionQ2ClientOptions { ApiKey = Key });

        var ex = await Assert.ThrowsAsync<AccordionQ2ApiException>(() => client.Capabilities.GetAsync());

        Assert.AreEqual(302, ex.StatusCode);
        Assert.DoesNotContain(Key, ex.ToString());
        Assert.AreEqual(1, station.Requests);
        Assert.AreEqual(0, elsewhere.Requests, "the redirect was followed");
        Assert.IsNull(elsewhere.ReceivedKey);
    }

    [TestMethod]
    public async Task CreateHandler_DoesNotFollowRedirects_ForTheCallersOwnHttpClient()
    {
        await using var elsewhere = await LocalServer.StartAsync(redirectTo: null);
        await using var station = await LocalServer.StartAsync(redirectTo: elsewhere.BaseUrl);
        using var http = new HttpClient(AccordionQ2Client.CreateHandler(new AccordionQ2ClientOptions()));
        using var client = new AccordionQ2Client(station.BaseUrl, new AccordionQ2ClientOptions { ApiKey = Key, HttpClient = http });

        var ex = await Assert.ThrowsAsync<AccordionQ2ApiException>(() => client.Capabilities.GetAsync());

        Assert.AreEqual(302, ex.StatusCode);
        Assert.AreEqual(0, elsewhere.Requests);
    }

    [TestMethod]
    public async Task SharedHttpClient_IsLeftUnchanged_AndServesTwoStations()
    {
        // No network: two clients on one HttpClient, each with its own base URL.
        var handler = new RecordingHandler();
        using var http = new HttpClient(handler);
        await http.GetAsync("http://already-used.invalid/"); // a used HttpClient can't take a BaseAddress any more

        using var a = new AccordionQ2Client("http://station-a:5000", new AccordionQ2ClientOptions { ApiKey = Key, HttpClient = http });
        using var b = new AccordionQ2Client("http://station-b:5000", new AccordionQ2ClientOptions { HttpClient = http });
        await a.Capabilities.GetAsync();
        await b.Capabilities.GetAsync();

        Assert.IsNull(http.BaseAddress);
        Assert.IsEmpty(http.DefaultRequestHeaders);
        Assert.AreEqual("http://station-a:5000/api/capabilities", handler.Requests[1].Uri!.AbsoluteUri);
        Assert.AreEqual(Key, handler.Requests[1].Header("X-Api-Key"));
        Assert.AreEqual("http://station-b:5000/api/capabilities", handler.Requests[2].Uri!.AbsoluteUri);
        Assert.IsNull(handler.Requests[2].Header("X-Api-Key"));
    }
}
