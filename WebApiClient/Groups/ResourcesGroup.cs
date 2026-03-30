using AccordionQ2.WebApiClient.Internal;

namespace AccordionQ2.WebApiClient.Groups;

/// <summary>
/// Operations for reading and writing hardware resource values.
/// Resources are identified by name (e.g. "Voltage.VDD", "Temperature.Ambient").
/// </summary>
public sealed class ResourcesGroup : ApiGroupBase
{
    internal ResourcesGroup(HttpClient http) : base(http) { }

    /// <summary>Returns the names of all available resources.</summary>
    public Task<string[]> GetNamesAsync(CancellationToken ct = default)
        => GetAsync<string[]>("api/resources/names", ct);

    /// <summary>Reads the current value of a single resource.</summary>
    public async Task<string> GetValueAsync(string name, CancellationToken ct = default)
    {
        var r = await PostAsync<ValueDto>("api/resources/value/get", new { Name = name }, ct).ConfigureAwait(false);
        return r.Value;
    }

    /// <summary>Sets the value of a single resource.</summary>
    public Task SetValueAsync(string name, string value, CancellationToken ct = default)
        => PostAsync("api/resources/value/set", new { Name = name, Value = value }, ct);

    /// <summary>
    /// Reads values for multiple resources in one round-trip.
    /// </summary>
    /// <returns>Dictionary mapping each resource name to its current value string.</returns>
    public async Task<Dictionary<string, string>> GetValuesAsync(string[] names, CancellationToken ct = default)
    {
        var r = await PostAsync<ValuesDto>("api/resources/values/get", new { Names = names }, ct).ConfigureAwait(false);
        return r.Resources;
    }

    /// <summary>Sets values for multiple resources in one round-trip.</summary>
    public Task SetValuesAsync(Dictionary<string, string> resources, CancellationToken ct = default)
        => PostAsync("api/resources/values/set", new { Resources = resources }, ct);

    /// <summary>
    /// Performs a write-then-read transaction on a resource (command/response pattern).
    /// Useful for EEPROM commands, register access, or other stateful resources.
    /// </summary>
    /// <returns>The response value returned by the resource.</returns>
    public async Task<string> TransactAsync(string name, string value, CancellationToken ct = default)
    {
        var r = await PostAsync<ValueDto>("api/resources/transact", new { Name = name, Value = value }, ct).ConfigureAwait(false);
        return r.Value;
    }

    private sealed class ValueDto  { public string Value     { get; set; } = string.Empty; }
    private sealed class ValuesDto { public Dictionary<string, string> Resources { get; set; } = new Dictionary<string, string>(); }
}
