using AccordionQ2.WebApiClient.Internal;
using AccordionQ2.WebApiClient.Models;

namespace AccordionQ2.WebApiClient.Groups;

/// <summary>
/// The control lease (accordionq2 contract section 5.5): one client at a time may change the station.
/// </summary>
/// <remarks>
/// After <see cref="AcquireAsync"/> succeeds, this client sends the lease id (<c>X-Lease-Id</c>) with every
/// request until <see cref="ReleaseAsync"/>, so its own writes and forced reads go through. Renew it with
/// <see cref="RenewAsync"/> before <see cref="LeaseDto.ExpiresInMs"/> runs out.
/// </remarks>
public sealed class LeaseGroup : ApiGroupBase
{
    public const string HeaderName = "X-Lease-Id";

    internal LeaseGroup(HttpClient http) : base(http) { }

    /// <summary>The lease this client holds, if any.</summary>
    public string? HeldLeaseId { get; private set; }

    /// <summary>Who holds the lease, if anyone.</summary>
    public Task<LeaseStateDto> GetAsync(CancellationToken ct = default) => GetAsync<LeaseStateDto>("api/lease", ct);

    /// <summary>
    /// Takes the lease for <paramref name="owner"/> (shown to everyone it blocks). Throws
    /// <see cref="AccordionQ2ApiException"/> with 409 when someone else holds it.
    /// </summary>
    public async Task<LeaseDto> AcquireAsync(string owner, int ttlMs = 30000, CancellationToken ct = default)
    {
        var lease = await PostAsync<LeaseDto>("api/lease", new { Owner = owner, TtlMs = ttlMs }, ct).ConfigureAwait(false);
        Use(lease.LeaseId);
        return lease;
    }

    /// <summary>Renews the lease this client holds, optionally with a new time to live. 404 once it has ended.</summary>
    public Task<LeaseDto> RenewAsync(int? ttlMs = null, CancellationToken ct = default)
    {
        if (HeldLeaseId is not { } id)
            throw new InvalidOperationException("This client holds no lease");
        return PutAsync<LeaseDto>("api/lease/" + Uri.EscapeDataString(id), new { TtlMs = ttlMs }, ct);
    }

    /// <summary>Releases the lease this client holds; does nothing when it holds none.</summary>
    public async Task ReleaseAsync(CancellationToken ct = default)
    {
        if (HeldLeaseId is not { } id)
            return;
        Use(null);
        try { await DeleteAsync("api/lease/" + Uri.EscapeDataString(id), ct).ConfigureAwait(false); }
        catch (AccordionQ2ApiException ex) when (ex.StatusCode == 404) { } // already ended
    }

    private void Use(string? leaseId)
    {
        HeldLeaseId = leaseId;
        _http.DefaultRequestHeaders.Remove(HeaderName);
        if (leaseId is not null)
            _http.DefaultRequestHeaders.Add(HeaderName, leaseId);
    }
}
