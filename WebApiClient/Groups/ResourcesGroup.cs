using AccordionQ2.WebApiClient.Models;
using AccordionQ2.WebApiClient.Internal;

namespace AccordionQ2.WebApiClient.Groups;

/// <summary>
/// Operations for reading and writing hardware resource values.
/// Resources are identified by name (e.g. "Voltage.VDD", "Temperature.Ambient").
/// </summary>
public sealed class ResourcesGroup : ApiGroupBase
{
    private readonly CapabilitiesGroup _capabilities;
    private volatile bool _cacheOnlyChecked;

    internal ResourcesGroup(ApiTransport transport) : base(transport) => _capabilities = new CapabilitiesGroup(transport);

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

    /// <summary>
    /// Reads several resources, accepting values the WebApi read at most <paramref name="maxAgeMs"/> ago
    /// (accordionq2 contract section 5.2); the rest are read from the hardware. 0 reads every one, as
    /// <see cref="GetValuesAsync(string[], CancellationToken)"/> does. The result says how old each value is.
    /// A WebApi without the cache reads every value and reports no ages.
    /// </summary>
    public Task<ResourceValuesDto> ReadValuesAsync(string[] names, int maxAgeMs, CancellationToken ct = default)
        => PostAsync<ResourceValuesDto>("api/resources/values/get", new { Names = names, MaxAgeMs = maxAgeMs }, ct);

    /// <summary>
    /// Reads several resources from the WebApi's value cache alone (accordionq2 contract section 5.2, <c>cacheOnly</c>):
    /// the station never reads the hardware for this call and the lease never refuses it, so it can't switch a mux or
    /// reset a peak buffer under a running test. A name with no cached value at most <paramref name="maxAgeMs"/> old is
    /// in <see cref="ResourceValuesDto.NotCached"/> instead of <see cref="ResourceValuesDto.Resources"/>, and so is every
    /// channel whose reads consume data. <paramref name="maxAgeMs"/> 0 takes a cached value of any age;
    /// <see cref="ResourceValuesDto.AgeMs"/> says how old each is.
    /// </summary>
    /// <remarks>
    /// The first call asks <c>GET /api/capabilities</c> for <see cref="CapabilitiesDto.ValuesCacheOnly"/>, since an older
    /// WebApi ignores <c>cacheOnly</c> and reads the hardware; without the feature nothing is asked for.
    /// </remarks>
    /// <exception cref="NotSupportedException">
    /// The station doesn't have <see cref="CapabilitiesDto.ValuesCacheOnly"/> (firmware before 7.0.5), and no values were
    /// asked for; or its answer had no <c>notCached</c>, so it ignored <c>cacheOnly</c>.
    /// </exception>
    /// <exception cref="AccordionQ2ApiException">400 for a name no channel has, as with the other reads.</exception>
    public async Task<ResourceValuesDto> ReadCachedValuesAsync(string[] names, int maxAgeMs = 0, CancellationToken ct = default)
    {
        if (!_cacheOnlyChecked)
        {
            var capabilities = await _capabilities.GetAsync(ct).ConfigureAwait(false);
            if (!capabilities.Has(CapabilitiesDto.ValuesCacheOnly))
                throw new NotSupportedException(
                    $"The station doesn't offer cache-only reads (feature '{CapabilitiesDto.ValuesCacheOnly}', firmware 7.0.5 or later); nothing was read");
            _cacheOnlyChecked = true;
        }
        var r = await PostAsync<ResourceValuesDto>("api/resources/values/get",
            new { Names = names, MaxAgeMs = maxAgeMs, CacheOnly = true }, ct).ConfigureAwait(false);
        if (r.NotCached is null)
            throw new NotSupportedException("The station answered without notCached: it ignored cacheOnly and may have read the hardware");
        return r;
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
