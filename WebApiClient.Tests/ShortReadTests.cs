using AccordionQ2.WebApiClient.Groups;
using AccordionQ2.WebApiClient.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AccordionQ2.WebApiClient.Tests;

/// <summary>
/// Unit tests for the client-side short-read guard (<see cref="CommGroup.VerifyReadLength"/>).
/// These run without a live agent so they execute in CI (not TestCategory=Integration).
/// </summary>
[TestClass]
public class ShortReadTests
{
    private static I2cTransactionRequest Req(BusActions action, int requested) => new()
    {
        DeviceName             = "dev",
        Address                = "50",
        Action                 = action,
        NumberOfBytesToReceive = requested,
    };

    private static BusTransactionResponse Resp(int received) => new()
    {
        NumberOfBytesReceived = received,
    };

    [TestMethod]
    public void Receive_ShortRead_Throws()
    {
        var ex = Assert.ThrowsExactly<AccordionQ2ShortReadException>(() =>
            CommGroup.VerifyReadLength(Req(BusActions.Receive, 1), Resp(0)));
        Assert.AreEqual(1, ex.RequestedBytes);
        Assert.AreEqual(0, ex.ReceivedBytes);
    }

    [TestMethod]
    public void SendReceive_ShortRead_Throws()
    {
        Assert.ThrowsExactly<AccordionQ2ShortReadException>(() =>
            CommGroup.VerifyReadLength(Req(BusActions.SendReceive, 4), Resp(2)));
    }

    [TestMethod]
    public void Receive_FullRead_DoesNotThrow()
        => CommGroup.VerifyReadLength(Req(BusActions.Receive, 4), Resp(4));

    [TestMethod]
    public void Receive_OverRead_DoesNotThrow()
        => CommGroup.VerifyReadLength(Req(BusActions.Receive, 1), Resp(2));

    [TestMethod]
    public void Scan_ShortByRequestedZero_Ignored()
    {
        // Scan carries no requested length; its returned bytes are the address list, never a short read.
        CommGroup.VerifyReadLength(Req(BusActions.Scan, 0), Resp(0));
    }

    [TestMethod]
    public void Send_Ignored()
        => CommGroup.VerifyReadLength(Req(BusActions.Send, 0), Resp(0));
}
