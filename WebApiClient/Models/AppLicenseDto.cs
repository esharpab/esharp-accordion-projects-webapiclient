namespace AccordionQ2.WebApiClient.Models;

/// <summary>Application license information.</summary>
public class AppLicenseDto
{
    public string Name { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public DateTime Expires { get; set; }
    public AppTypes Type { get; set; }
}
