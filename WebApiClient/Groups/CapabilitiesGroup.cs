using AccordionQ2.WebApiClient.Internal;
using AccordionQ2.WebApiClient.Models;

namespace AccordionQ2.WebApiClient.Groups;

/// <summary>What the WebApi supports (contract section 2).</summary>
public sealed class CapabilitiesGroup : ApiGroupBase
{
    internal CapabilitiesGroup(HttpClient http) : base(http) { }

    /// <summary>
    /// Returns the WebApi's capabilities, or <see cref="CapabilitiesDto.None"/> for a WebApi that
    /// predates them (404). Answers while the hardware app is disconnected too.
    /// </summary>
    public async Task<CapabilitiesDto> GetAsync(CancellationToken ct = default)
    {
        try
        {
            return await GetAsync<CapabilitiesDto>("api/capabilities", ct).ConfigureAwait(false);
        }
        catch (AccordionQ2ApiException ex) when (ex.StatusCode == 404)
        {
            return CapabilitiesDto.None;
        }
    }
}
