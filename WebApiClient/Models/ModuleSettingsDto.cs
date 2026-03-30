namespace AccordionQ2.WebApiClient.Models;

/// <summary>Configuration settings for a hardware or software module.</summary>
public class ModuleSettingsDto
{
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public string AssemblyPath { get; set; } = string.Empty;
    public string Namespace { get; set; } = string.Empty;
    public string ImageName { get; set; } = string.Empty;

    /// <summary>
    /// Arbitrary key/value initialisation data. Values are deserialized from JSON
    /// and may be strings, numbers, booleans, or Newtonsoft.Json JObject/JArray for
    /// complex types.
    /// </summary>
    public Dictionary<string, object?> InitialData { get; set; } = new Dictionary<string, object?>();
}
