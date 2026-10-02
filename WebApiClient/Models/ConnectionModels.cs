namespace AccordionQ2.WebApiClient.Models;

/// <summary>Current connection status of the API to the hardware manager.</summary>
public class ConnectionStatusDto
{
    /// <summary>Whether the API is connected to the hardware manager.</summary>
    public bool IsConnected { get; set; }

    /// <summary>Last connection error message, if any.</summary>
    public string? LastError { get; set; }

    /// <summary>
    /// Goes up each time the WebApi connects or reconnects to the hardware app; 0 from a WebApi that
    /// predates it. Reload channels when it changes.
    /// </summary>
    public long Generation { get; set; }
}
