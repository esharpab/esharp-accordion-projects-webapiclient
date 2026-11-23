using System.Net;
using System.Text;
using AccordionQ2.WebApiClient.Groups;
using AccordionQ2.WebApiClient.Tests.Fakes;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AccordionQ2.WebApiClient.Tests;

/// <summary>
/// client.Docs (firmware 7.1.0, api/docs): the list and a page as Markdown or HTML, the routes and the query, a station
/// before 7.1.0, and a page that isn't there. The answers against the server's records: WebApi.Tests DocsContractTests.
/// </summary>
[TestClass]
public class DocsGroupTests
{
    private const string List =
        "{\"baseUrl\":\"https://station-12/docs/\",\"llmsTxt\":\"https://station-12/docs/llms.txt\",\"pages\":[{\"path\":\"03-connectivity/security\"," +
        "\"title\":\"Security\",\"section\":\"Connectivity and security\",\"summary\":\"The reference.\",\"html\":\"03-connectivity/security.html\"," +
        "\"md\":\"03-connectivity/security.md\"}],\"note\":null}";

    private static (AccordionQ2Client Client, RecordingHandler Handler) Create(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var handler = new RecordingHandler { Respond = respond };
        return (new AccordionQ2Client("https://station-12:5001", new AccordionQ2ClientOptions { Handler = handler }), handler);
    }

    private static HttpResponseMessage Text(string text, string type) =>
        new(HttpStatusCode.OK) { Content = new StringContent(text, Encoding.UTF8, type) };

    [TestMethod]
    public async Task The_list_reads_the_pages_and_the_docs_URL()
    {
        var (client, handler) = Create(_ => RecordingHandler.Json(HttpStatusCode.OK, List));
        using var _ = client;
        var list = await client.Docs.ListAsync();
        Assert.AreEqual(HttpMethod.Get, handler.Last.Method);
        Assert.AreEqual("/api/docs", handler.Last.Uri!.AbsolutePath);
        Assert.AreEqual("https://station-12/docs/", list.BaseUrl);
        Assert.AreEqual("https://station-12/docs/llms.txt", list.LlmsTxt);
        Assert.IsNull(list.Note);
        var page = list.Pages.Single();
        Assert.AreEqual(("03-connectivity/security", "Security", "Connectivity and security", "The reference.", "03-connectivity/security.html", "03-connectivity/security.md"),
            (page.Path, page.Title, page.Section, page.Summary, page.Html, page.Md));
    }

    [TestMethod]
    public async Task A_station_before_7_1_0_answers_an_empty_list_with_a_note()
    {
        var (client, _) = Create(_ => RecordingHandler.Json(HttpStatusCode.NotFound, "{}"));
        using var __ = client;
        var list = await client.Docs.ListAsync();
        Assert.AreEqual(0, list.Pages.Count);
        Assert.AreEqual(DocsGroup.NoEndpointNote, list.Note);
    }

    [TestMethod]
    public async Task A_page_is_its_Markdown_or_with_html_its_HTML_page()
    {
        var (client, handler) = Create(r => r.RequestUri!.Query.Contains("format=html") ? Text("<!doctype html><p>Kävlinge</p>", "text/html") : Text("# Security\n\nKävlinge\n", "text/markdown"));
        using var _ = client;
        Assert.AreEqual("# Security\n\nKävlinge\n", await client.Docs.GetAsync("03-connectivity/security"));
        Assert.AreEqual("/api/docs/03-connectivity/security", handler.Last.Uri!.AbsolutePath);
        Assert.AreEqual("", handler.Last.Uri.Query);

        Assert.AreEqual("<!doctype html><p>Kävlinge</p>", await client.Docs.GetAsync("03-connectivity/security", html: true));
        Assert.AreEqual("?format=html", handler.Last.Uri!.Query);

        await client.Docs.GetAsync("/a page/x#y/");
        Assert.AreEqual("/api/docs/a%20page/x%23y", handler.Last.Uri!.AbsolutePath, "each segment escaped, the slashes kept");
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => client.Docs.GetAsync(" "));
    }

    [TestMethod]
    public async Task A_page_that_isn_t_there_is_a_404()
    {
        var (client, _) = Create(_ => RecordingHandler.Json(HttpStatusCode.NotFound, "{\"error\":\"No page 'x' in the documentation; GET /api/docs lists the pages\"}"));
        using var __ = client;
        var ex = await Assert.ThrowsExactlyAsync<AccordionQ2ApiException>(() => client.Docs.GetAsync("x"));
        Assert.AreEqual(404, ex.StatusCode);
    }
}
