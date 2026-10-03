using AccordionQ2.WebApiClient.Internal;
using AccordionQ2.WebApiClient.Models;

namespace AccordionQ2.WebApiClient.Groups;

/// <summary>
/// The station's services, reboot and clock (contract section 9). Changes are writes: while another client
/// holds the lease they are refused with 423.
/// </summary>
public sealed class SystemGroup : ApiGroupBase
{
    internal SystemGroup(HttpClient http) : base(http) { }

    /// <summary>The hardware app, the WebApi and the dashboard, with their state.</summary>
    public Task<List<ServiceStatusDto>> GetServicesAsync(CancellationToken ct = default)
        => GetAsync<List<ServiceStatusDto>>("api/system/services", ct);

    /// <summary>
    /// Starts, stops or restarts a service, or turns starting it at boot on or off (<c>start</c>, <c>stop</c>,
    /// <c>restart</c>, <c>enable</c>, <c>disable</c>). Doesn't wait for the job; follow <see cref="GetServicesAsync"/>.
    /// Restarting the WebApi itself drops this client's connection for a few seconds.
    /// </summary>
    public Task ServiceActionAsync(string id, string action, CancellationToken ct = default)
        => PostAsync($"api/system/services/{Uri.EscapeDataString(id)}/{Uri.EscapeDataString(action)}", null, ct);

    /// <summary>Reboots the Pi a second after answering; it is back after about a minute.</summary>
    public Task RebootAsync(CancellationToken ct = default)
        => PostAsync("api/system/reboot", null, ct);

    /// <summary>The Pi's clock.</summary>
    public Task<ClockStatusDto> GetClockAsync(CancellationToken ct = default)
        => GetAsync<ClockStatusDto>("api/system/clock", ct);

    /// <summary>
    /// Sets the Pi's clock, typically to this computer's. A clock a time server keeps is refused (409) unless
    /// <paramref name="force"/>.
    /// </summary>
    public Task<ClockStatusDto> SetClockAsync(DateTimeOffset utc, bool force = false, CancellationToken ct = default)
        => PutAsync<ClockStatusDto>("api/system/clock", new { utc = utc.ToUniversalTime(), force }, ct);
}
