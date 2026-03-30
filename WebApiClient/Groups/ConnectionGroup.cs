using AccordionQ2.WebApiClient.Internal;
using AccordionQ2.WebApiClient.Models;

namespace AccordionQ2.WebApiClient.Groups;

/// <summary>Operations for querying the API's connection status to the hardware manager.</summary>
public sealed class ConnectionGroup : ApiGroupBase
{
    internal ConnectionGroup(HttpClient http) : base(http) { }

    /// <summary>Returns the current connection status.</summary>
    public Task<ConnectionStatusDto> GetStatusAsync(CancellationToken ct = default)
        => GetAsync<ConnectionStatusDto>("api/connection/status", ct);
}
