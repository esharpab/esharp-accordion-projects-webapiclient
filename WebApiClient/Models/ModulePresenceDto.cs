namespace AccordionQ2.WebApiClient.Models;

/// <summary>
/// One module present on the station (<c>GET /api/modules/present</c>, firmware with stable module names): a hardware module in
/// the backplane or a software module loaded from modules.config or boot.config. Hardware first by slot, then software
/// by name.
/// </summary>
public class ModulePresenceDto
{
    /// <summary><c>"hardware"</c> or <c>"software"</c>.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>
    /// The module's address, what the Web UI's filter <c>channels?module=</c> takes: the slot path for hardware
    /// (<c>"0.2"</c>), the configured name for software (<c>"MicroHAPS"</c>).
    /// </summary>
    public string Address { get; set; } = string.Empty;

    /// <summary>
    /// What its net names start with, before a <c>.</c>: <c>"0.2.ESH10000560"</c> for hardware, the name for software
    /// (<c>"MicroHAPS"</c>: <c>MicroHAPS.HCI.Scan</c>).
    /// </summary>
    public string Prefix { get; set; } = string.Empty;

    /// <summary>The hardware module's product name (<c>"MPIO-96 SPI Module"</c>), or the software module's name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Hardware only: <c>"ESH10000560"</c>.</summary>
    public string? ProductId { get; set; }

    /// <summary>Hardware only.</summary>
    public int? Revision { get; set; }

    /// <summary>Hardware only.</summary>
    public string? SerialNumber { get; set; }

    /// <summary>Software only: the module's class, <c>"MicroHapsHandler.MicroHaps"</c>.</summary>
    public string? ClassName { get; set; }

    /// <summary>True for a hardware module (<see cref="Kind"/> <c>"hardware"</c>).</summary>
    public bool IsHardware => string.Equals(Kind, "hardware", StringComparison.OrdinalIgnoreCase);
}
