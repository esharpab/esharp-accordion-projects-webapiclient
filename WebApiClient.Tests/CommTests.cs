using AccordionQ2.WebApiClient.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AccordionQ2.WebApiClient.Tests;

[TestClass]
[TestCategory("Integration")]
public class CommTests
{
    // -------------------------------------------------------------------------
    // I2C
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task I2c_Scan_KnownBus_ReturnsResponse()
    {
        var response = await TestSetup.Client.Comm.I2cAsync(new I2cTransactionRequest
        {
            DeviceName = TestConfig.I2cDeviceName,
            Address    = "00",
            Action     = BusActions.Scan,
        });

        Assert.IsNotNull(response);
        Assert.AreEqual("Scan",                  response.Action);
        Assert.AreEqual(TestConfig.I2cDeviceName, response.DeviceName);
        Assert.AreEqual(response.Received.Length / 2, response.NumberOfBytesReceived,
            "NumberOfBytesReceived must match the actual received-bytes length");

        Console.WriteLine($"I2C scan on '{response.DeviceName}': {response.NumberOfBytesReceived} device(s) found");
        foreach (var addr in Convert.FromHexString(response.Received))
            Console.WriteLine($"  0x{addr:X2}");
    }

    [TestMethod]
    public async Task I2c_Scan_ResponseDeviceNameMatchesRequest()
    {
        var response = await TestSetup.Client.Comm.I2cAsync(new I2cTransactionRequest
        {
            DeviceName = TestConfig.I2cDeviceName,
            Address    = "00",
            Action     = BusActions.Scan,
        });

        Assert.AreEqual(TestConfig.I2cDeviceName, response.DeviceName,
            "Response DeviceName must echo the requested device");
    }

    [TestMethod]
    public async Task I2c_Send_NullDataToSend_ThrowsApiException()
    {
        // null DataToSend collapses to [] in the controller → ArgumentException → 400
        await Assert.ThrowsExactlyAsync<AccordionQ2ApiException>(() =>
            TestSetup.Client.Comm.I2cAsync(new I2cTransactionRequest
            {
                DeviceName = TestConfig.I2cDeviceName,
                Address    = "50",
                Action     = BusActions.Send,
                DataToSend = null,
            }));
    }

    [TestMethod]
    public async Task I2c_Send_EmptyDataToSend_ThrowsApiException()
    {
        // Empty byte array is also rejected by the underlying BusTransaction constructor
        await Assert.ThrowsExactlyAsync<AccordionQ2ApiException>(() =>
            TestSetup.Client.Comm.I2cAsync(new I2cTransactionRequest
            {
                DeviceName = TestConfig.I2cDeviceName,
                Address    = "50",
                Action     = BusActions.Send,
                DataToSend = string.Empty,
            }));
    }

    [TestMethod]
    public async Task I2c_SendReceive_EmptyDataToSend_ThrowsApiException()
    {
        await Assert.ThrowsExactlyAsync<AccordionQ2ApiException>(() =>
            TestSetup.Client.Comm.I2cAsync(new I2cTransactionRequest
            {
                DeviceName             = TestConfig.I2cDeviceName,
                Address                = "50",
                Action                 = BusActions.SendReceive,
                DataToSend             = string.Empty,
                NumberOfBytesToReceive = 4,
            }));
    }

    // -------------------------------------------------------------------------
    // UART
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task Uart_Send_NullDataToSend_ThrowsApiException()
    {
        // Validation fires before any hardware call, so any device name is fine here
        await Assert.ThrowsExactlyAsync<AccordionQ2ApiException>(() =>
            TestSetup.Client.Comm.UartAsync(new UartTransactionRequest
            {
                DeviceName = "validation-only",
                Action     = BusActions.Send,
                DataToSend = null,
            }));
    }

    [TestMethod]
    public async Task Uart_Send_EmptyDataToSend_ThrowsApiException()
    {
        await Assert.ThrowsExactlyAsync<AccordionQ2ApiException>(() =>
            TestSetup.Client.Comm.UartAsync(new UartTransactionRequest
            {
                DeviceName = "validation-only",
                Action     = BusActions.Send,
                DataToSend = string.Empty,
            }));
    }

    [TestMethod]
    public async Task Uart_SendReceive_EmptyDataToSend_ThrowsApiException()
    {
        await Assert.ThrowsExactlyAsync<AccordionQ2ApiException>(() =>
            TestSetup.Client.Comm.UartAsync(new UartTransactionRequest
            {
                DeviceName             = "validation-only",
                Action                 = BusActions.SendReceive,
                DataToSend             = string.Empty,
                NumberOfBytesToReceive = 32,
                TimeoutMs              = 500,
            }));
    }

    // -------------------------------------------------------------------------
    // SPI
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task Spi_Send_NullDataToSend_ThrowsApiException()
    {
        await Assert.ThrowsExactlyAsync<AccordionQ2ApiException>(() =>
            TestSetup.Client.Comm.SpiAsync(new SpiTransactionRequest
            {
                DeviceName = "validation-only",
                Action     = BusActions.Send,
                DataToSend = null,
            }));
    }

    [TestMethod]
    public async Task Spi_Send_EmptyDataToSend_ThrowsApiException()
    {
        await Assert.ThrowsExactlyAsync<AccordionQ2ApiException>(() =>
            TestSetup.Client.Comm.SpiAsync(new SpiTransactionRequest
            {
                DeviceName = "validation-only",
                Action     = BusActions.Send,
                DataToSend = string.Empty,
            }));
    }

    [TestMethod]
    public async Task Spi_SendReceive_EmptyDataToSend_ThrowsApiException()
    {
        await Assert.ThrowsExactlyAsync<AccordionQ2ApiException>(() =>
            TestSetup.Client.Comm.SpiAsync(new SpiTransactionRequest
            {
                DeviceName             = "validation-only",
                Action                 = BusActions.SendReceive,
                DataToSend             = string.Empty,
                NumberOfBytesToReceive = 4,
            }));
    }

    // -------------------------------------------------------------------------
    // Socket
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task Socket_Send_NullDataToSend_ThrowsApiException()
    {
        await Assert.ThrowsExactlyAsync<AccordionQ2ApiException>(() =>
            TestSetup.Client.Comm.SocketAsync(new SocketTransactionRequest
            {
                DeviceName = "validation-only",
                Action     = BusActions.Send,
                HostName   = "127.0.0.1",
                Port       = 9999,
                DataToSend = null,
            }));
    }

    [TestMethod]
    public async Task Socket_Send_EmptyDataToSend_ThrowsApiException()
    {
        await Assert.ThrowsExactlyAsync<AccordionQ2ApiException>(() =>
            TestSetup.Client.Comm.SocketAsync(new SocketTransactionRequest
            {
                DeviceName = "validation-only",
                Action     = BusActions.Send,
                HostName   = "127.0.0.1",
                Port       = 9999,
                DataToSend = string.Empty,
            }));
    }

    [TestMethod]
    public async Task Socket_SendReceive_EmptyDataToSend_ThrowsApiException()
    {
        await Assert.ThrowsExactlyAsync<AccordionQ2ApiException>(() =>
            TestSetup.Client.Comm.SocketAsync(new SocketTransactionRequest
            {
                DeviceName             = "validation-only",
                Action                 = BusActions.SendReceive,
                HostName               = "127.0.0.1",
                Port                   = 9999,
                DataToSend             = string.Empty,
                NumberOfBytesToReceive = 64,
            }));
    }
}
