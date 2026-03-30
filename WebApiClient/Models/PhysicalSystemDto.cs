namespace AccordionQ2.WebApiClient.Models;

/// <summary>Physical hardware system description (topology).</summary>
public class PhysicalSystemDto
{
    public string Host { get; set; } = string.Empty;
    public string EthIpV4 { get; set; } = string.Empty;
    public string EthIpV6 { get; set; } = string.Empty;
    public string Firmware { get; set; } = string.Empty;
    public string MAC { get; set; } = string.Empty;
    public List<PhysicalModuleDto> Modules { get; set; } = new List<PhysicalModuleDto>();
    public Dictionary<string, string> NetworkInterfaces { get; set; } = new Dictionary<string, string>();
}

/// <summary>Describes one physical hardware module slot.</summary>
public class PhysicalModuleDto
{
    public int Index { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ProductID { get; set; } = string.Empty;
    public byte Revision { get; set; }
    public string SerialNumber { get; set; } = string.Empty;
}
