using AccordionQ2.WebApiClient.Internal;
using AccordionQ2.WebApiClient.Models;
using Newtonsoft.Json;

namespace AccordionQ2.WebApiClient.Groups;

/// <summary>
/// The station's JTAG ports (firmware with stable module names): each a JTAG TAP that a module owns, held by one of the
/// owner, an XVC client or a Drummer instance at a time. Found by their State channels (Unit <c>jtag-port</c>).
/// </summary>
/// <remarks>
/// <code>
/// var ports = await client.JtagPorts.GetAllAsync();       // [MicroHAPS.JTAG.A]
/// Console.WriteLine(ports[0].State?.State);              // "held"
/// await client.JtagPorts.SetXvcAsync("MicroHAPS.JTAG.A", true);   // Admin
/// </code>
/// </remarks>
public sealed class JtagPortsGroup : ApiGroupBase
{
    private readonly ChannelsGroup _channels;
    private readonly ResourcesGroup _resources;

    internal JtagPortsGroup(ApiTransport transport) : base(transport)
    {
        _channels = new ChannelsGroup(transport);
        _resources = new ResourcesGroup(transport);
    }

    /// <summary>Every JTAG port with its state read now, by name. Empty on firmware without ports.</summary>
    public async Task<List<JtagPortDto>> GetAllAsync(CancellationToken ct = default)
    {
        var states = (await _channels.GetAllAsync(ct).ConfigureAwait(false))
            .Where(c => string.Equals(c.Unit, JtagPortDto.StateUnit, StringComparison.Ordinal)
                        && c.NetName.EndsWith(".State", StringComparison.Ordinal))
            .Select(c => c.NetName)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (states.Length == 0)
            return new List<JtagPortDto>();
        var values = await _resources.GetValuesAsync(states, ct).ConfigureAwait(false);
        return states.Select(n =>
        {
            string raw = values.TryGetValue(n, out var v) ? v : string.Empty;
            return new JtagPortDto { Name = n.Substring(0, n.Length - ".State".Length), Raw = raw, State = Parse(raw) };
        }).ToList();
    }

    /// <summary>One port's state now (<paramref name="port"/> is its name, or one of its channels' net names).</summary>
    public async Task<JtagPortStateDto?> GetStateAsync(string port, CancellationToken ct = default) =>
        Parse(await _resources.GetValueAsync(PortName(port) + ".State", ct).ConfigureAwait(false));

    /// <summary>
    /// Switches the port's XVC server on or off (<c>&lt;port&gt;.Xvc</c>). Needs Admin (403 below it); 409 when the TCP
    /// port can't be bound. Switching it on takes nothing from a holder: a client takes the port at its first shift.
    /// </summary>
    public Task SetXvcAsync(string port, bool on, CancellationToken ct = default) =>
        _resources.SetValueAsync(PortName(port) + ".Xvc", on ? "True" : "False", ct);

    /// <summary>"MicroHAPS.JTAG.A" for itself and for "MicroHAPS.JTAG.A.State" or ".Xvc".</summary>
    public static string PortName(string port)
    {
        if (string.IsNullOrWhiteSpace(port)) throw new ArgumentException("give the port's name, e.g. \"MicroHAPS.JTAG.A\"", nameof(port));
        port = port.Trim();
        if (port.EndsWith(".State", StringComparison.OrdinalIgnoreCase)) return port.Substring(0, port.Length - 6);
        if (port.EndsWith(".Xvc", StringComparison.OrdinalIgnoreCase)) return port.Substring(0, port.Length - 4);
        return port;
    }

    private static JtagPortStateDto? Parse(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        try
        {
            return JsonConvert.DeserializeObject<JtagPortStateDto>(raw);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
