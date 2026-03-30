namespace AccordionQ2.WebApiClient.Models;

/// <summary>Represents a multi-purpose hardware channel.</summary>
public class ChannelDto
{
    public int ChannelIndex { get; set; }
    public int Index { get; set; }
    public bool Enabled { get; set; }
    public MpioUsageTypes Usage { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public ChannelTypes ChannelType { get; set; }
    public ChannelTypes ChannelTypeCapability { get; set; }
    public string Alias { get; set; } = string.Empty;
    public string NetName { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;
    public DirectionTypes Capability { get; set; }
    public string Description { get; set; } = string.Empty;
    public DirectionTypes Direction { get; set; }
    public bool DirectionChanged { get; set; }
    public DirectionTypes DefaultDirection { get; set; }
    public string Unit { get; set; } = string.Empty;
    public bool IsVirtual { get; set; }
}
