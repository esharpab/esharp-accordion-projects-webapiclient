namespace AccordionQ2.WebApiClient.Models;

// The station's documentation (firmware 7.1.0, api/docs). Field names and shapes are the WebApi's DocsController and
// DocsEntry; WebApi.Tests DocsContractTests reads the server's answers into these.

/// <summary>
/// The station's documentation pages (<c>GET /api/docs</c>): <c>BaseUrl</c> is <c>/docs/</c> on the station's Web UI
/// port, where each page's <c>Html</c> and <c>Md</c> paths are (null when the Web UI is off); <c>LlmsTxt</c> the pages
/// in the llms.txt convention; <c>Note</c> why the list is empty (a station without the pages, or firmware before 7.1.0).
/// </summary>
public sealed class DocsListDto
{
    public string? BaseUrl { get; set; }
    public string? LlmsTxt { get; set; }
    public List<DocsPageDto> Pages { get; set; } = [];
    public string? Note { get; set; }
}

/// <summary>
/// A page of the station's documentation: <c>Path</c> is what <see cref="Groups.DocsGroup.GetAsync"/> takes; its title,
/// the section it is listed in, its first paragraph shortened, and its HTML page and Markdown copy under <c>/docs/</c>.
/// </summary>
public sealed class DocsPageDto
{
    public string Path { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Section { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Html { get; set; } = string.Empty;
    public string Md { get; set; } = string.Empty;
}
