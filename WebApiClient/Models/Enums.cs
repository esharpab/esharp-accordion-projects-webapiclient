namespace AccordionQ2.WebApiClient.Models;

/// <summary>Mirrors <c>EsharpDefinitions.CommonTypes.ModuleStatus</c>.</summary>
public enum ModuleStatus { Unknown, OK, Warning, Error, Disabled }

/// <summary>Mirrors <c>EsharpDefinitions.Types.BusTransactionTypes.BusActions</c>.</summary>
public enum BusActions
{
    Undefined    = -1,
    Send         =  0,
    Receive      =  1,
    SendReceive  =  2,
    Scan         =  3,
    Break        =  4,
    ClearBuffers =  5,
    Reconfigure  =  6
}

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

/// <summary>Mirrors <c>EsharpDefinitions.Types.BusTransactionTypes.UartBusTypes</c>.</summary>
public enum UartBusTypes { Undefined = -1, RS232 = 0, RS422 = 1, RS485 = 2 }

/// <summary>Mirrors <c>EsharpDefinitions.Types.BusTransactionTypes.FlowControlTypes</c>.</summary>
public enum FlowControlTypes
{
    Undefined              = -1,
    None                   =  0,
    XON_XOFF              =  1,
    RTS_CTS               =  2,
    RTS_CTS_AND_XON_XOFF  =  3,
    DTR_DSR               =  4,
    DTR_DSR_AND_XON_XOFF  =  5
}

/// <summary>Mirrors <c>EsharpDefinitions.Types.InstrumentTypes.ParityTypes</c>.</summary>
public enum ParityTypes { Undefined = -1, Even = 0, Mark = 1, None = 2, Odd = 3, Space = 4 }
