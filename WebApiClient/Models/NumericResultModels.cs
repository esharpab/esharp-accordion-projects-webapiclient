namespace AccordionQ2.WebApiClient.Models;

/// <summary>
/// Describes one NumericResult channel and its sampling capabilities.
/// </summary>
public class NumericResultChannelDto
{
    /// <summary>Channel net name (use as the <c>channelNetName</c> parameter).</summary>
    public string NetName { get; set; } = string.Empty;

    /// <summary>Human-readable alias.</summary>
    public string Alias { get; set; } = string.Empty;

    /// <summary>Physical channels this NumericResult channel can sample.</summary>
    public string[] PossibleTargetNames { get; set; } = Array.Empty<string>();

    /// <summary>Hardware sample rate in Hz.</summary>
    public int SampleRate { get; set; }

    /// <summary>Default number of samples configured on the channel.</summary>
    public int DefaultSamples { get; set; }
}

/// <summary>
/// Request for triggering a numeric sampling acquisition.
/// </summary>
public class NumericMeasureRequest
{
    /// <summary>Net name of the NumericResult channel to use.</summary>
    public string ChannelNetName { get; set; } = string.Empty;

    /// <summary>Net name of the physical channel to sample.</summary>
    public string TargetNetName { get; set; } = string.Empty;

    /// <summary>Number of samples to acquire. Default is 1000.</summary>
    public int Samples { get; set; } = 1000;

    /// <summary>
    /// When true (default), the firmware discards raw samples after computing summary statistics,
    /// minimising transfer size.  Set false to retain raw samples.
    /// </summary>
    public bool ReducedSet { get; set; } = true;
}

/// <summary>
/// Acquisition metadata returned by <see cref="Groups.NumericResultsGroup.MeasureAsync"/>.
/// </summary>
public class NumericMeasureResultDto
{
    /// <summary>Net name of the NumericResult channel used.</summary>
    public string ChannelNetName { get; set; } = string.Empty;

    /// <summary>Net name of the physical channel that was sampled.</summary>
    public string TargetNetName { get; set; } = string.Empty;

    /// <summary>Number of raw samples retained (0 when <see cref="ReducedSet"/> is true).</summary>
    public int SampleCount { get; set; }

    /// <summary>Sample rate in Hz.</summary>
    public int SampleRate { get; set; }

    /// <summary>Whether raw samples were discarded after computing statistics.</summary>
    public bool ReducedSet { get; set; }

    /// <summary>Timestamp when acquisition started.</summary>
    public DateTime Started { get; set; }

    /// <summary>Timestamp when acquisition stopped.</summary>
    public DateTime Stopped { get; set; }

    /// <summary>Total acquisition duration.</summary>
    public TimeSpan Duration { get; set; }
}
