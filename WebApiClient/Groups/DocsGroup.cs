using AccordionQ2.WebApiClient.Internal;
using AccordionQ2.WebApiClient.Models;

namespace AccordionQ2.WebApiClient.Groups;

/// <summary>
/// The station's documentation (firmware 7.1.0, <c>api/docs</c>), for scripts and AI agents: the pages the station
/// serves under <c>/docs/</c> on its Web UI port, listed and read as Markdown or HTML. Anonymous in every security mode
/// (no key needed; one the client has is sent as for every call).
/// </summary>
public sealed class DocsGroup : ApiGroupBase
{
    /// <summary>The note of the list from a station whose firmware has no <c>api/docs</c> (before 7.1.0).</summary>
    public const string NoEndpointNote = "This station's firmware has no api/docs (it came with 7.1.0).";

    internal DocsGroup(ApiTransport transport) : base(transport) { }

    /// <summary>
    /// The documentation's pages, in the documentation's order, with <c>/docs/</c>'s URL on the station's Web UI. Empty
    /// with a <see cref="DocsListDto.Note"/> when the station has no pages, or its firmware has no <c>api/docs</c>.
    /// </summary>
    public async Task<DocsListDto> ListAsync(CancellationToken ct = default)
        => await GetOrNullAsync<DocsListDto>("api/docs", ct).ConfigureAwait(false) ?? new DocsListDto { Note = NoEndpointNote };

    /// <summary>
    /// A page by its <see cref="DocsPageDto.Path"/> (or its .md or .html file): its cleaned Markdown, or with
    /// <paramref name="html"/> its HTML page. A path that isn't one of the pages throws <see cref="AccordionQ2ApiException"/>
    /// with 404.
    /// </summary>
    public Task<string> GetAsync(string path, bool html = false, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("A page's path from ListAsync", nameof(path));
        var escaped = string.Join("/", path.Trim('/').Split('/').Select(Uri.EscapeDataString));
        return GetTextAsync($"api/docs/{escaped}{(html ? "?format=html" : "")}", ct);
    }
}
