namespace AccordionQ2.WebApiClient.Models;

/// <summary>Current connection status of the API to the hardware manager.</summary>
public class ConnectionStatusDto
{
    /// <summary>Whether the API is connected to the hardware manager.</summary>
    public bool IsConnected { get; set; }

    /// <summary>Last connection error message, if any.</summary>
    public string? LastError { get; set; }
}
