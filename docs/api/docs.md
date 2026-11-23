# Documentation

The station's own documentation, for scripts and AI agents (firmware 7.1.0): the pages the station serves under `/docs/` on its Web UI port, listed and read as Markdown or HTML. The calls need no API key in any security mode; a key the client has is sent as on every call. In security mode `On` requests without a key are rate limited per client, as for every call that needs no key.

`GetAsync` of [Capabilities](capabilities.md) lists the feature `docs` (`CapabilitiesDto.Docs`) when the station has the pages.

## Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `ListAsync(ct?)` | `Task<DocsListDto>` | The pages, in the documentation's order, and `/docs/` on the station's Web UI. Empty with a `Note` when the station has no pages, or its firmware has no `api/docs` (before 7.1.0). |
| `GetAsync(path, html?, ct?)` | `Task<string>` | A page by its `Path` from the list (or its `.md` or `.html` file): its Markdown, or with `html: true` its HTML page. A path that isn't one of the pages throws `AccordionQ2ApiException` with `StatusCode` 404. |

## Example

```csharp
var docs = await client.Docs.ListAsync();
foreach (var page in docs.Pages.Where(p => p.Section == "Connectivity and security"))
    Console.WriteLine($"{page.Title}: {page.Summary}");

string markdown = await client.Docs.GetAsync("03-connectivity/security");
```

## Models

`DocsListDto`

| Property | Type | Description |
|----------|------|-------------|
| `BaseUrl` | `string?` | `/docs/` on the station's Web UI port, where each page's `Html` and `Md` are; null when the Web UI is off |
| `LlmsTxt` | `string?` | The pages in the llms.txt convention (`/docs/llms.txt`); null when the Web UI is off or there are no pages |
| `Pages` | `List<DocsPageDto>` | The pages |
| `Note` | `string?` | Why the list is empty; null otherwise |

`DocsPageDto`

| Property | Type | Description |
|----------|------|-------------|
| `Path` | `string` | What `GetAsync` takes, e.g. `03-connectivity/security` |
| `Title` | `string` | The page's title |
| `Section` | `string` | The section it is listed in, e.g. `Connectivity and security` |
| `Summary` | `string` | Its first paragraph, shortened |
| `Html` | `string` | The HTML page, relative to `BaseUrl` |
| `Md` | `string` | Its Markdown copy, relative to `BaseUrl` |
