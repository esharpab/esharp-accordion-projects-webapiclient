namespace AccordionQ2.WebApiClient.Models;

/// <summary>
/// The type-specific fields of a channel (accordionq2 contract section 7): only the ones that belong
/// to the channel's type are set, the rest are null.
/// </summary>
/// <remarks>
/// Enums are kept as strings, so a value added in the hardware app doesn't break older clients.
/// Default values are strings in the resource-value format ("True", "1.25"), whatever the type.
/// </remarks>
public class ChannelDetailsDto
{
    // Analog, Current, Ratiometric, Temperature
    public double? Gain { get; set; }
    public double? Offset { get; set; }

    // Analog
    /// <summary>RSE, NRSE or Differential.</summary>
    public string? InputConfiguration { get; set; }
    public string[]? SupportedConfigurations { get; set; }
    public string? MeasurementGroup { get; set; }

    // Analog, Current, Digital, PseudoDigital, Actuator, Register
    public string? DefaultValue { get; set; }

    // Analog, Current, Resistance, Frequency
    public int? Resolution { get; set; }

    // Digital
    /// <summary>OpenDrain or PushPull.</summary>
    public string? PushType { get; set; }
    /// <summary>None, Down or Up.</summary>
    public string? PullType { get; set; }

    // VirtualDigital
    /// <summary>CUSTOM, AND, OR or TOGGLE.</summary>
    public string? ExpressionType { get; set; }
    public Dictionary<string, bool>? UnderlyingGpios { get; set; }

    // PseudoDigital
    public double? Vl { get; set; }
    public double? Vh { get; set; }
    public double? MinIoVoltage { get; set; }
    public double? MaxIoVoltage { get; set; }

    // Actuator
    public bool? ToggleEnabled { get; set; }
    public int? ToggleTimeMs { get; set; }
    public double? CurrentLimit { get; set; }

    // Temperature
    public string? SensorType { get; set; }
    public string? ColdJunctionDestination { get; set; }
    public string? ChannelSelection { get; set; }
    public double? Parameter { get; set; }

    // Resistance
    public double? ClampVoltage { get; set; }
    public double? MaxClampVoltage { get; set; }
    public double? ReferenceResistance { get; set; }
    public string? SourceNetName { get; set; }
    public string? DestinationNetName { get; set; }

    // Counter
    public DateTime? Started { get; set; }
    public DateTime? Ended { get; set; }

    // Current
    public bool? OvervoltageProtection { get; set; }
    public double? OvervoltageProtectionLimit { get; set; }
    public bool? OvercurrentProtection { get; set; }
    public double? OvercurrentProtectionLimit { get; set; }

    // Register
    public string? RegisterType { get; set; }
    /// <summary>RO, RW or WO.</summary>
    public string? HostRights { get; set; }
    public uint? Address { get; set; }
    public uint? Length { get; set; }

    // Multiplexer
    public string? SourceNet { get; set; }
    /// <summary>The values the multiplexer accepts; set one through Resources.</summary>
    public string[]? DestinationNets { get; set; }

    // Instrument (also from Instruments.GetAllAsync)
    public string? InstrumentName { get; set; }
    public string? InstrumentType { get; set; }
    public Dictionary<string, string>? FunctionMap { get; set; }

    // NumericResult
    public string? TargetNetName { get; set; }
    public string[]? PossibleTargetNames { get; set; }
    public int? NumberOfSamples { get; set; }
    public int? SampleRate { get; set; }
    public bool? ReducedSet { get; set; }
    public bool? MultiChannel { get; set; }

    // UART
    public int? Baudrate { get; set; }
    public string? BusType { get; set; }
    public int? TerminationByte { get; set; }

    // SPI, I2C
    public int? ClockSpeed { get; set; }
    public string? Mode { get; set; }
    public bool? ChipEnableHigh { get; set; }

    // Audio (SampleRate and Length, in seconds, above too): the device, and what a recording or playback uses
    /// <summary>The audio device's name.</summary>
    public string? Name { get; set; }
    /// <summary>Volume in dB, within <see cref="MinVolumeDb"/> to <see cref="MaxVolumeDb"/>.</summary>
    public double? Volume { get; set; }
    public int[]? AllowedSampleRates { get; set; }
    public int[]? AllowedChannels { get; set; }
    public string[]? AllowedFormats { get; set; }
    public double? MinVolumeDb { get; set; }
    public double? MaxVolumeDb { get; set; }
}
