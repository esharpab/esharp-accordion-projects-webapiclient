using AccordionQ2.WebApiClient.Internal;
using AccordionQ2.WebApiClient.Models;

namespace AccordionQ2.WebApiClient.Groups;

/// <summary>Operations for querying and configuring hardware channels.</summary>
public sealed class ChannelsGroup : ApiGroupBase
{
    internal ChannelsGroup(HttpClient http) : base(http) { }

    /// <summary>
    /// Returns every channel, or the one with <paramref name="netName"/> (net names only, no aliases),
    /// in the byte protocol's encoding (feature <see cref="CapabilitiesDto.ChannelsEncoded"/>).
    /// </summary>
    public Task<EncodedChannelsDto> GetEncodedAsync(string? netName = null, CancellationToken ct = default)
        => GetAsync<EncodedChannelsDto>(netName is null ? "api/channels/encoded" : "api/channels/encoded?netName=" + Uri.EscapeDataString(netName), ct);

    /// <summary>
    /// Configures channels sent whole in the byte protocol's encoding, so type changes and type-specific
    /// settings work. Throws <see cref="AccordionQ2ApiException"/> with 409 when <paramref name="codecVersion"/>
    /// isn't the server's. Returns how many channels were configured; reload on the <c>configuration</c>
    /// event rather than from this response.
    /// </summary>
    public async Task<int> ConfigureEncodedAsync(int codecVersion, string data, CancellationToken ct = default)
        => (await PostAsync<ConfiguredResponse>("api/channels/configure/encoded", new { CodecVersion = codecVersion, Data = data }, ct).ConfigureAwait(false)).Configured;

    private sealed class ConfiguredResponse { public int Configured { get; set; } }

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
