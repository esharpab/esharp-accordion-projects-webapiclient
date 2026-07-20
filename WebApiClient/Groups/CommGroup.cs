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
    /// <exception cref="AccordionQ2ShortReadException">
    /// A Receive/SendReceive returned fewer bytes than requested (e.g. an out-of-date agent that
    /// silently accepts a wrong-length receive). See <see cref="AccordionQ2ShortReadException"/>.
    /// </exception>
    public async Task<BusTransactionResponse> I2cAsync(I2cTransactionRequest request, CancellationToken ct = default)
    {
        var response = await PostAsync<BusTransactionResponse>("api/comm/i2c", request, ct).ConfigureAwait(false);
        VerifyReadLength(request, response);
        return response;
    }

    /// <summary>
    /// Performs a UART bus transaction (Send, Receive, SendReceive, or ClearBuffers).
    /// </summary>
    public Task<BusTransactionResponse> UartAsync(UartTransactionRequest request, CancellationToken ct = default)
        => PostAsync<BusTransactionResponse>("api/comm/uart", request, ct);

    /// <summary>
    /// Performs a SPI bus transaction (Send, Receive, or SendReceive).
    /// </summary>
    /// <exception cref="AccordionQ2ShortReadException">
    /// A Receive/SendReceive returned fewer bytes than requested (e.g. an out-of-date agent that
    /// silently accepts a wrong-length receive). See <see cref="AccordionQ2ShortReadException"/>.
    /// </exception>
    public async Task<BusTransactionResponse> SpiAsync(SpiTransactionRequest request, CancellationToken ct = default)
    {
        var response = await PostAsync<BusTransactionResponse>("api/comm/spi", request, ct).ConfigureAwait(false);
        VerifyReadLength(request, response);
        return response;
    }

    /// <summary>
    /// Performs a Socket (TCP/IP) bus transaction (Send, Receive, or SendReceive).
    /// </summary>
    public Task<BusTransactionResponse> SocketAsync(SocketTransactionRequest request, CancellationToken ct = default)
        => PostAsync<BusTransactionResponse>("api/comm/socket", request, ct);

    /// <summary>
    /// Guards against a bus read that reports success but returns fewer bytes than requested.
    /// Applies only to clocked buses (I2C/SPI) where a short read is never legitimate; UART and
    /// Socket are timeout-bounded, so a short read there is expected and left to the caller.
    /// Only Receive/SendReceive carry a requested length — Send and Scan are ignored.
    /// </summary>
    internal static void VerifyReadLength(BusTransactionRequestBase request, BusTransactionResponse response)
    {
        if (request.Action != BusActions.Receive && request.Action != BusActions.SendReceive)
            return;

        var requested = request.NumberOfBytesToReceive;
        if (requested <= 0)
            return;

        if (response.NumberOfBytesReceived < requested)
            throw new AccordionQ2ShortReadException(requested, response.NumberOfBytesReceived);
    }
}
