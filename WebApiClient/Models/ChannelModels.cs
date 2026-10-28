namespace AccordionQ2.WebApiClient.Models;

/// <summary>
/// Identifies a channel by net name or alias. At least one must be provided.
/// If <see cref="NetName"/> is supplied it is used exclusively to locate the channel.
/// If only <see cref="Alias"/> is supplied, it must match an existing channel alias.
/// </summary>
public class ChannelLookupRequest
{
    /// <summary>Alias name of the channel. Used for lookup when NetName is not provided.</summary>
    public string? Alias { get; set; }

    /// <summary>Net name of the channel. When provided, takes priority over Alias for lookup.</summary>
    public string? NetName { get; set; }
}

/// <summary>
/// Partial-update configuration for a single channel.
/// Only non-null properties are applied; the rest are left unchanged.
/// If <see cref="NetName"/> is provided it is used to locate the channel; if <see cref="Alias"/> is
/// also supplied the alias is updated to that value.
/// If only <see cref="Alias"/> is provided it must match an existing channel alias.
/// </summary>
public class ChannelConfigRequest
{
    /// <summary>
    /// Alias for lookup (when NetName is absent) or new alias value (when NetName is also provided).
    /// </summary>
    public string? Alias { get; set; }

    /// <summary>Net name used to locate the channel. Takes priority over Alias for lookup.</summary>
    public string? NetName { get; set; }

    /// <summary>Enable or disable the channel.</summary>
    public bool? Enabled { get; set; }

    /// <summary>
    /// Set the I/O direction. Must be a subset of the channel's Capability flags.
    /// </summary>
    public DirectionTypes? Direction { get; set; }

    /// <summary>
    /// Set the active channel type. Must be a subset of the channel's ChannelTypeCapability flags.
    /// </summary>
    public ChannelTypes? ChannelType { get; set; }

    /// <summary>Human-readable description.</summary>
    public string? Description { get; set; }

    /// <summary>Unit of measurement (e.g. "°C", "V", "A").</summary>
    public string? Unit { get; set; }

    /// <summary>Logical group name for organising channels.</summary>
    public string? GroupName { get; set; }

    /// <summary>Name of the device that provides this channel.</summary>
    public string? DeviceName { get; set; }

    /// <summary>
    /// Type-specific fields to change (accordionq2 contract section 7): set only the ones to change,
    /// e.g. <c>new ChannelDetailsDto { Gain = 2 }</c>. Only the configurable fields of the channel's
    /// type are accepted; the server refuses the request with the field's name otherwise.
    /// </summary>
    public ChannelDetailsDto? Details { get; set; }

    /// <summary>
    /// The output's value, applied in the same configure as <see cref="Direction"/>, <see cref="ChannelType"/> and
    /// <see cref="Details"/>, so the pin doesn't drive another level in between. Written as
    /// <c>Resources.SetValueAsync</c> takes it, invariant culture: <c>"1.8"</c> for an Analog channel (in its units,
    /// at the gain and offset the request leaves), <c>"True"</c>/<c>"False"</c> for a PseudoDigital or Digital one;
    /// <see cref="WithValue(double)"/> and <see cref="WithValue(bool)"/> format it. Only for a channel the request
    /// leaves an output; the station refuses it otherwise (400). Without it, a channel that becomes an output (from an
    /// input, or as another type) starts at its default value, never at the last value read while it was an input.
    /// Needs firmware 7.0.5 or later: older firmware ignores the field and the output starts at its last reading.
    /// </summary>
    public string? Value { get; set; }

    /// <summary>Sets <see cref="Value"/> to <paramref name="value"/>, invariant culture; returns this request.</summary>
    public ChannelConfigRequest WithValue(double value)
    {
        Value = value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        return this;
    }

    /// <summary>Sets <see cref="Value"/> to "True" or "False"; returns this request.</summary>
    public ChannelConfigRequest WithValue(bool value)
    {
        Value = value ? "True" : "False";
        return this;
    }
}
