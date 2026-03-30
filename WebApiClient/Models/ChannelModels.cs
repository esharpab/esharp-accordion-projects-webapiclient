namespace AccordionQ2.WebApiClient.Models;

/// <summary>Identifies a channel by alias or net name. At least one must be provided.</summary>
public class ChannelLookupRequest
{
    /// <summary>The alias name of the channel (preferred).</summary>
    public string? Alias { get; set; }

    /// <summary>The net name of the channel (alternative to alias).</summary>
    public string? NetName { get; set; }
}

/// <summary>
/// Partial-update configuration for a single channel.
/// Only non-null properties are applied; the rest are left unchanged.
/// At least one of <see cref="Alias"/> or <see cref="NetName"/> is required.
/// </summary>
public class ChannelConfigRequest
{
    /// <summary>Alias name of the channel to update (preferred).</summary>
    public string? Alias { get; set; }

    /// <summary>Net name of the channel to update.</summary>
    public string? NetName { get; set; }

    /// <summary>Enable or disable the channel.</summary>
    public bool? Enabled { get; set; }

    /// <summary>Set the I/O direction (IN, OUT, or both).</summary>
    public DirectionTypes? Direction { get; set; }

    /// <summary>Human-readable description.</summary>
    public string? Description { get; set; }

    /// <summary>Unit of measurement (e.g. "°C", "V", "A").</summary>
    public string? Unit { get; set; }

    /// <summary>Logical group name for organising channels.</summary>
    public string? GroupName { get; set; }

    /// <summary>Name of the device that provides this channel.</summary>
    public string? DeviceName { get; set; }
}
