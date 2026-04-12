using AccordionQ2.WebApiClient.Internal;
using AccordionQ2.WebApiClient.Models;

namespace AccordionQ2.WebApiClient.Groups;

/// <summary>
/// Operations for performing raw bus transactions (I2C, UART, SPI, Socket).
/// </summary>
public sealed class CommGroup : ApiGroupBase
{
    internal CommGroup(HttpClient http) : base(http) { }

    /// <summary>
    /// Performs an I2C bus transaction (Send, Receive, SendReceive, or Scan).
    /// </summary>
    public Task<BusTransactionResponse> I2cAsync(I2cTransactionRequest request, CancellationToken ct = default)
        => PostAsync<BusTransactionResponse>("api/comm/i2c", request, ct);

    /// <summary>
    /// Performs a UART bus transaction (Send, Receive, SendReceive, or ClearBuffers).
    /// </summary>
    public Task<BusTransactionResponse> UartAsync(UartTransactionRequest request, CancellationToken ct = default)
        => PostAsync<BusTransactionResponse>("api/comm/uart", request, ct);

    /// <summary>
    /// Performs a SPI bus transaction (Send, Receive, or SendReceive).
    /// </summary>
    public Task<BusTransactionResponse> SpiAsync(SpiTransactionRequest request, CancellationToken ct = default)
        => PostAsync<BusTransactionResponse>("api/comm/spi", request, ct);

    /// <summary>
    /// Performs a Socket (TCP/IP) bus transaction (Send, Receive, or SendReceive).
    /// </summary>
    public Task<BusTransactionResponse> SocketAsync(SocketTransactionRequest request, CancellationToken ct = default)
        => PostAsync<BusTransactionResponse>("api/comm/socket", request, ct);
}
