using AccordionQ2.WebApiClient.Internal;
using AccordionQ2.WebApiClient.Models;

namespace AccordionQ2.WebApiClient.Groups;

/// <summary>
/// Instrument channels: power supplies, meters and other instruments a module exposes.
/// </summary>
/// <remarks>
/// <c>Channels.GetAllAsync</c> returns the base fields of every channel. An instrument's type and
/// function map come from here; read and write its controls through <c>Resources</c>, by the net
/// names in the map.
/// <code>
/// var supply = (await client.Instruments.GetAllAsync()).First(i => i.Type == "PowerSupply");
/// await client.Resources.SetValueAsync(supply.FunctionMap["OUTPUT_VOLTAGE"], "12");
/// await client.Resources.SetValueAsync(supply.FunctionMap["OUTPUT_ENABLE"], "True");
/// </code>
/// Needs firmware with <c>GET /api/instruments</c>; older firmware answers 404.
/// </remarks>
public sealed class InstrumentsGroup : ApiGroupBase
{
    internal InstrumentsGroup(HttpClient http) : base(http) { }

    /// <summary>Returns every Instrument channel with its type and function map.</summary>
    public Task<List<InstrumentDto>> GetAllAsync(CancellationToken ct = default)
        => GetAsync<List<InstrumentDto>>("api/instruments", ct);
}
