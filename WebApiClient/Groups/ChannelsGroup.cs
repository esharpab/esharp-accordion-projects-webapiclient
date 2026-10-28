using AccordionQ2.WebApiClient.Internal;
using AccordionQ2.WebApiClient.Models;

namespace AccordionQ2.WebApiClient.Groups;

/// <summary>Operations for querying and configuring hardware channels.</summary>
public sealed class ChannelsGroup : ApiGroupBase
{
    internal ChannelsGroup(ApiTransport transport) : base(transport) { }

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

    /// <summary>
    /// Uploads a file to a ByteStream file channel (firmware 7.0.5): an FPGA image to MicroHAPS's
    /// <c>JTAG.FPGA_IMAGE</c>, loaded into the KU5P's SRAM by openFPGALoader without Vivado. The stream is sent as it
    /// is read (<c>application/octet-stream</c>, at most 64 MB), not buffered whole. Returns once the station has
    /// taken the file and started the load (202); follow <see cref="GetByteStreamStatusAsync"/> or use
    /// <see cref="WaitByteStreamAsync"/>. Throws <see cref="AccordionQ2ApiException"/> with 400 (not a file channel,
    /// or the file refused, e.g. built for another part), 409 (a load, a POWER cycle or a PROGRAM pulse is running),
    /// 413 (too big), 423 (leased by someone else), 501 (<c>target</c> <c>flash</c>: not in this firmware) or 504 (the
    /// station didn't take the file in time; read the status, the load may still start).
    /// </summary>
    /// <param name="name">The channel's net name or alias.</param>
    /// <param name="content">The file's bytes.</param>
    /// <param name="fileName">The file's own name, shown in the status and the log.</param>
    /// <param name="target"><c>sram</c>: until the next power cycle or PROGRAM_B pulse.</param>
    /// <param name="force">Load even while Vivado is connected to the station's XVC server (its session is cut).</param>
    /// <param name="ct">Cancellation token.</param>
    public Task<ByteStreamStatusDto> UploadByteStreamAsync(string name, Stream content, string fileName, string target = "sram",
                                                           bool force = false, CancellationToken ct = default)
        => PostStreamAsync<ByteStreamStatusDto>(ByteStreamPath(name, fileName, target, force), content, ct);

    /// <summary>Uploads the file at <paramref name="path"/>; see the stream overload.</summary>
    public async Task<ByteStreamStatusDto> UploadByteStreamAsync(string name, string path, string target = "sram",
                                                                 bool force = false, CancellationToken ct = default)
    {
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        return await UploadByteStreamAsync(name, file, Path.GetFileName(path), target, force, ct).ConfigureAwait(false);
    }

    internal static string ByteStreamPath(string name, string fileName, string target, bool force) =>
        $"api/channels/bytestream?name={Uri.EscapeDataString(name)}&fileName={Uri.EscapeDataString(fileName)}"
        + $"&target={Uri.EscapeDataString(target)}" + (force ? "&force=true" : "");

    /// <summary>
    /// The status of a ByteStream file channel: the load's state, progress and message, and the last image's name, size,
    /// SHA-256 and result. Answered from the module's memory, also while someone else holds the lease.
    /// </summary>
    public Task<ByteStreamStatusDto> GetByteStreamStatusAsync(string name, CancellationToken ct = default)
        => GetAsync<ByteStreamStatusDto>($"api/channels/bytestream?name={Uri.EscapeDataString(name)}", ct);

    /// <summary>
    /// Reads the status every <paramref name="pollInterval"/> (default 500 ms) until the load has ended (<c>done</c>
    /// or <c>failed</c>) and returns it; a <c>failed</c> load is returned, not thrown. Throws
    /// <see cref="TimeoutException"/> after <paramref name="timeout"/>.
    /// </summary>
    public async Task<ByteStreamStatusDto> WaitByteStreamAsync(string name, TimeSpan timeout, TimeSpan? pollInterval = null,
                                                               CancellationToken ct = default)
    {
        var interval = pollInterval ?? TimeSpan.FromMilliseconds(500);
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (true)
        {
            var status = await GetByteStreamStatusAsync(name, ct).ConfigureAwait(false);
            if (status.IsEnded)
                return status;
            if (DateTimeOffset.UtcNow >= deadline)
                throw new TimeoutException($"{name}: still {status.State} ({status.Progress:0} %) after {timeout.TotalSeconds:0} s");
            await Task.Delay(interval, ct).ConfigureAwait(false);
        }
    }
}
