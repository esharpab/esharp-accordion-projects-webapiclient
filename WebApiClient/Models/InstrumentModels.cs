namespace AccordionQ2.WebApiClient.Models;

/// <summary>
/// An Instrument channel with its type and function map, from <c>GET /api/instruments</c>.
/// </summary>
/// <remarks>
/// The function map names the channel behind each of the instrument's capabilities, for example
/// <c>OUTPUT_VOLTAGE</c> → <c>0.4.ESH10000662.VSET1</c>. Every key is optional.
/// </remarks>
public class InstrumentDto
{
    /// <summary>Net name of the Instrument channel; unique.</summary>
    public string NetName { get; set; } = string.Empty;

    /// <summary>Alias of the Instrument channel.</summary>
    public string Alias { get; set; } = string.Empty;

    /// <summary>Group, e.g. "CH1" for one output of a two-output supply.</summary>
    public string GroupName { get; set; } = string.Empty;

    /// <summary>Description of the instrument.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Instrument name; several outputs of one module can share it.</summary>
    public string InstrumentName { get; set; } = string.Empty;

    /// <summary>The instrument type (EsharpDefinitions <c>InstrumentTypes</c>), e.g. "PowerSupply".</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Capability name to the net name of the channel that implements it.</summary>
    public Dictionary<string, string> FunctionMap { get; set; } = new Dictionary<string, string>();
}
