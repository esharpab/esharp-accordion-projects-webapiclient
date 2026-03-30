namespace AccordionQ2.WebApiClient.Models;

/// <summary>Mirrors <c>EsharpDefinitions.CommonTypes.ModuleStatus</c>.</summary>
public enum ModuleStatus { Unknown, OK, Warning, Error, Disabled }

/// <summary>Mirrors <c>EsharpDefinitions.Types.ApplicationTypes.AppTypes</c>.</summary>
public enum AppTypes { Unknown, SoftwareModule, HardwareModule }

/// <summary>Mirrors <c>EsharpDefinitions.Types.BusTransactionTypes.DirectionTypes</c>.</summary>
[Flags]
public enum DirectionTypes
{
    Undefined = 0,
    IN        = 1,
    OUT       = 2
}

/// <summary>Mirrors <c>EsharpDefinitions.Types.BusTransactionTypes.MpioUsageTypes</c>.</summary>
public enum MpioUsageTypes
{
    Undefined             = -1,
    HiddenSystemControl   =  0,
    ReadOnlySystemControl =  1,
    UserAllocatable       =  2,
    BusSignal             =  3
}

/// <summary>Mirrors <c>EsharpDefinitions.Types.BusTransactionTypes.ChannelTypes</c>.</summary>
[Flags]
public enum ChannelTypes
{
    Undefined      = 0,
    Analog         = 1 << 0,
    Digital        = 1 << 1,
    VirtualDigital = 1 << 2,
    Temperature    = 1 << 3,
    Multiplexer    = 1 << 4,
    Resistance     = 1 << 5,
    Counter        = 1 << 6,
    Frequency      = 1 << 7,
    Actuator       = 1 << 8,
    Register       = 1 << 10,
    Current        = 1 << 11,
    Ratiometric    = 1 << 12,
    UART           = 1 << 13,
    SPI            = 1 << 14,
    I2C            = 1 << 15,
    ByteStream     = 1 << 16,
    Socket         = 1 << 17,
    Waveform       = 1 << 18,
    NumericResult  = 1 << 19,
    PseudoDigital  = 1 << 20,
    Image          = 1 << 21,
    Audio          = 1 << 22,
    Video          = 1 << 23,
    Instrument     = 1 << 24,
    NumericResults = 1 << 25,
    Calibration    = 1 << 26
}
