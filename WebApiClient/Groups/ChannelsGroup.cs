using AccordionQ2.WebApiClient.Internal;
using AccordionQ2.WebApiClient.Models;

namespace AccordionQ2.WebApiClient.Groups;

/// <summary>Operations for querying and configuring hardware channels.</summary>
public sealed class ChannelsGroup : ApiGroupBase
{
    internal ChannelsGroup(HttpClient http) : base(http) { }

    /// <summary>Returns all configured channels.</summary>
    public Task<List<ChannelDto>> GetAllAsync(CancellationToken ct = default)
        => GetAsync<List<ChannelDto>>("api/channels", ct);

    /// <summary>Looks up a single channel by alias or net name.</summary>
    /// <param name="alias">Alias name (preferred).</param>
    /// <param name="netName">Net name (alternative to alias).</param>
    /// <param name="ct">Cancellation token.</param>
    public Task<ChannelDto> GetChannelAsync(string? alias = null, string? netName = null, CancellationToken ct = default)
        => PostAsync<ChannelDto>("api/channels/channel",
            new ChannelLookupRequest { Alias = alias, NetName = netName }, ct);

    /// <summary>
    /// Applies a partial update to a single channel.
    /// Only non-null properties in <paramref name="config"/> are changed.
    /// </summary>
    public Task ConfigureAsync(ChannelConfigRequest config, CancellationToken ct = default)
        => PostAsync("api/channels/channel/configure", config, ct);

    /// <summary>
    /// Applies partial updates to multiple channels in one round-trip.
    /// Only non-null properties in each request are changed.
    /// </summary>
    public Task ConfigureManyAsync(List<ChannelConfigRequest> configs, CancellationToken ct = default)
        => PostAsync("api/channels/configure", configs, ct);
}
