using System.Net;
using System.Text;

namespace AccordionQ2.WebApiClient.Tests.Fakes;

/// <summary>A handler that records each request and answers it as <see cref="Respond"/> says; no network.</summary>
internal sealed class RecordingHandler : HttpMessageHandler
{
    internal sealed record Recorded(HttpMethod Method, Uri? Uri, Dictionary<string, string[]> Headers, byte[] Body)
    {
        public string? Header(string name) =>
            Headers.TryGetValue(name, out var values) ? string.Join(",", values) : null;
    }

    public List<Recorded> Requests { get; } = new();

    /// <summary>The answer; by default 200 with <c>{}</c>.</summary>
    public Func<HttpRequestMessage, HttpResponseMessage> Respond { get; set; } = _ => Json(HttpStatusCode.OK, "{}");

    public Recorded Last => Requests[^1];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var headers = request.Headers.ToDictionary(h => h.Key, h => h.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
        var body = request.Content is null ? Array.Empty<byte>() : await request.Content.ReadAsByteArrayAsync(ct);
        Requests.Add(new Recorded(request.Method, request.RequestUri, headers, body));
        return Respond(request);
    }

    public static HttpResponseMessage Json(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
}
