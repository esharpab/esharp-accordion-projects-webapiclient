namespace AccordionQ2.WebApiClient;

/// <summary>
/// Thrown when a bus read transaction reports HTTP success but delivers fewer bytes than were
/// requested. This most often indicates an out-of-date webapi/agent whose bus layer silently
/// accepts a receive of the wrong length and returns an empty (or short) payload instead of
/// failing — a case that would otherwise be indistinguishable from a genuine read.
/// </summary>
public sealed class AccordionQ2ShortReadException : Exception
{
    /// <summary>Number of bytes the caller asked to receive.</summary>
    public int RequestedBytes { get; }

    /// <summary>Number of bytes the transaction actually returned.</summary>
    public int ReceivedBytes { get; }

    /// <inheritdoc/>
    public AccordionQ2ShortReadException(int requestedBytes, int receivedBytes)
        : base($"Bus read returned {receivedBytes} of {requestedBytes} requested byte(s). " +
               "The transaction reported success but delivered a short read. This commonly means the " +
               "target webapi/agent is out of date and silently accepts a receive of the wrong length — " +
               "confirm the agent has the bus Receive-length fix deployed before trusting bus reads.")
    {
        RequestedBytes = requestedBytes;
        ReceivedBytes = receivedBytes;
    }
}
