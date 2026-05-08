namespace AccordionQ2.WebApiClient.Models;

/// <summary>
/// Base properties shared by all bus transaction requests.
/// </summary>
public abstract class BusTransactionRequestBase
{
    /// <summary>
    /// Name of the hardware device to transact with (must match the device name registered in the hardware manager).
    /// </summary>
    public string DeviceName { get; set; } = string.Empty;

    /// <summary>
    /// Bus action to perform.
    /// </summary>
    public BusActions Action { get; set; } = BusActions.Send;

    /// <summary>
    /// Bytes to send, hex-encoded (uppercase, no separator, e.g. "AABB"). Required for <see cref="BusActions.Send"/> and <see cref="BusActions.SendReceive"/> actions.
    /// </summary>
    public string? DataToSend { get; set; }

    /// <summary>
    /// Number of bytes to receive. Used for <see cref="BusActions.Receive"/> and <see cref="BusActions.SendReceive"/> actions.
    /// </summary>
    public int NumberOfBytesToReceive { get; set; }
}

/// <summary>
/// I2C bus transaction request.
/// </summary>
public class I2cTransactionRequest : BusTransactionRequestBase
{
    /// <summary>I2C 7-bit device address as a two-digit hex string (e.g. "50" for 0x50).</summary>
    public string Address { get; set; } = "00";

    /// <summary>Maximum number of retries on NAK or error. -1 uses the device default.</summary>
    public int MaxRetries { get; set; } = -1;
}

/// <summary>
/// UART bus transaction request.
/// </summary>
public class UartTransactionRequest : BusTransactionRequestBase
{
    /// <summary>Serial port name (e.g. "/dev/ttyS0" or "COM3").</summary>
    public string PortName { get; set; } = string.Empty;

    /// <summary>Baud rate (e.g. 9600, 115200).</summary>
    public int BaudRate { get; set; } = 9600;

    /// <summary>UART bus type (RS232, RS422, RS485).</summary>
    public UartBusTypes BusType { get; set; } = UartBusTypes.RS232;

    /// <summary>Flow control mode.</summary>
    public FlowControlTypes FlowControl { get; set; } = FlowControlTypes.None;

    /// <summary>Parity setting.</summary>
    public ParityTypes Parity { get; set; } = ParityTypes.None;

    /// <summary>Whether to use a termination byte as a receive boundary.</summary>
    public bool UseTerminationByte { get; set; } = false;

    /// <summary>Termination byte as a two-digit hex string (e.g. "0A" for LF). Only used when <see cref="UseTerminationByte"/> is true.</summary>
    public string TerminationByte { get; set; } = "0A";

    /// <summary>Transaction timeout in milliseconds.</summary>
    public int TimeoutMs { get; set; } = 1000;
}

/// <summary>
/// SPI bus transaction request.
/// </summary>
public class SpiTransactionRequest : BusTransactionRequestBase { }

/// <summary>
/// Socket (TCP/IP) bus transaction request.
/// </summary>
public class SocketTransactionRequest : BusTransactionRequestBase
{
    /// <summary>Remote host name or IP address.</summary>
    public string HostName { get; set; } = string.Empty;

    /// <summary>Remote port number.</summary>
    public int Port { get; set; }

    /// <summary>Byte value used as a message terminator when <see cref="UseTerminationByte"/> is true, as a two-digit hex string (e.g. "0A").</summary>
    public string TerminationByte { get; set; } = "00";

    /// <summary>Whether to use <see cref="TerminationByte"/> as a receive boundary.</summary>
    public bool UseTerminationByte { get; set; }

    /// <summary>Transaction timeout in milliseconds.</summary>
    public int TimeoutMs { get; set; } = 1000;
}

/// <summary>
/// Result of a bus transaction.
/// </summary>
public class BusTransactionResponse
{
    /// <summary>Name of the device the transaction was performed with.</summary>
    public string DeviceName { get; set; } = string.Empty;

    /// <summary>Bus action that was performed.</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Bytes received from the device, hex-encoded (uppercase, no separator).</summary>
    public string Received { get; set; } = string.Empty;

    /// <summary>Number of bytes received.</summary>
    public int NumberOfBytesReceived { get; set; }
}
