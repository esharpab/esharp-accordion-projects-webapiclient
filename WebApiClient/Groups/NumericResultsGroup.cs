using AccordionQ2.WebApiClient.Internal;
using AccordionQ2.WebApiClient.Models;

namespace AccordionQ2.WebApiClient.Groups;

/// <summary>
/// Operations for fast numeric sampling via dedicated NumericResult channels.
/// </summary>
/// <remarks>
/// Typical workflow:
/// <code>
/// // 1. Discover which NumericResult channels exist
/// var channels = await client.NumericResults.GetChannelsAsync();
///
/// // 2. Pick a channel and see what it can sample
/// var targets = await client.NumericResults.GetTargetsAsync(channels[0].NetName);
///
/// // 3. Trigger acquisition (result cached server-side)
/// var meta = await client.NumericResults.MeasureAsync(new NumericMeasureRequest
/// {
///     ChannelNetName = channels[0].NetName,
///     TargetNetName  = targets[0],
///     Samples        = 1000,
///     ReducedSet     = true
/// });
///
/// // 4. Fetch statistics
/// double mean  = await client.NumericResults.GetMeanAsync(channels[0].NetName);
/// double stdev = await client.NumericResults.GetStdDevAsync(channels[0].NetName);
/// </code>
/// </remarks>
public sealed class NumericResultsGroup : ApiGroupBase
{
    internal NumericResultsGroup(HttpClient http) : base(http) { }

    /// <summary>Returns all NumericResult channels with their sampling capabilities.</summary>
    /// <summary>
    /// Acquires <paramref name="samples"/> samples on <paramref name="target"/> through the NumericResult channel
    /// <paramref name="channel"/> and returns them with their statistics in one call (accordionq2 contract
    /// section 8). With both limits, <see cref="NumericStatsDto.Cp"/> and <see cref="NumericStatsDto.Cpk"/> are set.
    /// It reconfigures the channel, so it needs the lease while someone else holds it.
    /// </summary>
    public async Task<NumericAcquisitionDto> AcquireAsync(string channel, string target, int samples = 1000,
        double? lsl = null, double? usl = null, CancellationToken ct = default)
    {
        var raw = await PostAsync<RawAcquisition>("api/numeric-results/acquire",
            new { Channel = channel, Target = target, Samples = samples, Lsl = lsl, Usl = usl }, ct).ConfigureAwait(false);
        var bytes = Convert.FromBase64String(raw.Samples ?? string.Empty);
        var values = new double[bytes.Length / 8];
        for (var i = 0; i < values.Length; i++)
        {
            // Little-endian float64, whatever this machine is.
            var word = BitConverter.ToInt64(bytes, i * 8);
            if (!BitConverter.IsLittleEndian)
                word = System.Net.IPAddress.HostToNetworkOrder(word);
            values[i] = BitConverter.Int64BitsToDouble(word);
        }
        return new NumericAcquisitionDto
        {
            Channel = raw.Channel, Target = raw.Target, Unit = raw.Unit, SampleRate = raw.SampleRate,
            Started = raw.Started, DurationMs = raw.DurationMs, Samples = values, Stats = raw.Stats ?? new NumericStatsDto(),
        };
    }

    private sealed class RawAcquisition
    {
        public string Channel { get; set; } = string.Empty;
        public string Target { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public int SampleRate { get; set; }
        public DateTime Started { get; set; }
        public double? DurationMs { get; set; }
        public string? Samples { get; set; }
        public NumericStatsDto? Stats { get; set; }
    }

    public Task<List<NumericResultChannelDto>> GetChannelsAsync(CancellationToken ct = default)
        => GetAsync<List<NumericResultChannelDto>>("api/numeric-results/channels", ct);

    /// <summary>Returns the physical channel net names that a NumericResult channel can sample.</summary>
    /// <param name="channelNetName">Net name of the NumericResult channel.</param>
    /// <param name="ct">Cancellation token.</param>
    public Task<string[]> GetTargetsAsync(string channelNetName, CancellationToken ct = default)
        => GetAsync<string[]>(
            $"api/numeric-results/targets?channel={Uri.EscapeDataString(channelNetName)}", ct);

    /// <summary>
    /// Configures and triggers a numeric sampling acquisition.
    /// The result is cached server-side; use the <c>Get*Async</c> methods to retrieve values.
    /// </summary>
    public Task<NumericMeasureResultDto> MeasureAsync(NumericMeasureRequest request, CancellationToken ct = default)
        => PostAsync<NumericMeasureResultDto>("api/numeric-results/measure", request, ct);

    /// <summary>Returns the mean value from the last measurement on a channel.</summary>
    public Task<double> GetMeanAsync(string channelNetName, CancellationToken ct = default)
        => GetAsync<double>(
            $"api/numeric-results/result/mean?channel={Uri.EscapeDataString(channelNetName)}", ct);

    /// <summary>Returns the minimum value from the last measurement on a channel.</summary>
    public Task<double> GetMinAsync(string channelNetName, CancellationToken ct = default)
        => GetAsync<double>(
            $"api/numeric-results/result/min?channel={Uri.EscapeDataString(channelNetName)}", ct);

    /// <summary>Returns the maximum value from the last measurement on a channel.</summary>
    public Task<double> GetMaxAsync(string channelNetName, CancellationToken ct = default)
        => GetAsync<double>(
            $"api/numeric-results/result/max?channel={Uri.EscapeDataString(channelNetName)}", ct);

    /// <summary>Returns the standard deviation from the last measurement on a channel.</summary>
    public Task<double> GetStdDevAsync(string channelNetName, CancellationToken ct = default)
        => GetAsync<double>(
            $"api/numeric-results/result/stdev?channel={Uri.EscapeDataString(channelNetName)}", ct);

    /// <summary>
    /// Returns the raw sample array from the last measurement on a channel.
    /// </summary>
    /// <remarks>
    /// Throws <see cref="AccordionQ2ApiException"/> (HTTP 400) if the measurement was taken with
    /// <c>ReducedSet=true</c>.  Re-measure with <c>ReducedSet=false</c> to retain raw samples.
    /// </remarks>
    public Task<double[]> GetSamplesAsync(string channelNetName, CancellationToken ct = default)
        => GetAsync<double[]>(
            $"api/numeric-results/result/samples?channel={Uri.EscapeDataString(channelNetName)}", ct);
}
