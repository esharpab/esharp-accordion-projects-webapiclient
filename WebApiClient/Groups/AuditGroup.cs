using AccordionQ2.WebApiClient.Internal;

namespace AccordionQ2.WebApiClient.Groups;

/// <summary>
/// Audit log operations.
/// </summary>
public sealed class AuditGroup : ApiGroupBase
{
    internal AuditGroup(HttpClient http) : base(http) { }

    /// <summary>
    /// Returns the last <paramref name="tail"/> lines of the WebApi audit log.
    /// Pass 0 to retrieve the entire log.
    /// </summary>
    public Task<string[]> GetAuditLogAsync(int tail = 100, CancellationToken ct = default)
        => GetAsync<string[]>($"api/audit/log?tail={tail}", ct);
}
