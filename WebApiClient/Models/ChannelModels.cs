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
}
