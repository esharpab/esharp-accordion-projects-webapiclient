using AccordionQ2.WebApiClient.Internal;
using AccordionQ2.WebApiClient.Models;

namespace AccordionQ2.WebApiClient.Groups;

/// <summary>
/// Value subscriptions (accordionq2 contract section 5.3): values of the named channels about every interval,
/// delivered as <see cref="ServerEventDto.Values"/> events on the event stream the subscription belongs to.
/// </summary>
public sealed class SubscriptionsGroup : ApiGroupBase
{
    internal SubscriptionsGroup(HttpClient http) : base(http) { }

    /// <summary>
    /// Subscribes the stream <paramref name="streamId"/> (from its <c>hello</c>) to <paramref name="channels"/>.
    /// The subscription ends with the stream, on <see cref="DeleteAsync"/>, or when not renewed with
    /// <see cref="UpdateAsync"/> within <see cref="SubscriptionDto.ExpiresInMs"/>.
    /// </summary>
    public Task<SubscriptionDto> CreateAsync(string streamId, IEnumerable<string> channels, int intervalMs, CancellationToken ct = default)
        => PostAsync<SubscriptionDto>("api/subscriptions", new { StreamId = streamId, Channels = channels.ToArray(), IntervalMs = intervalMs }, ct);

    /// <summary>Replaces the subscription's channels and interval, and renews it. 404 when it has ended.</summary>
    public Task<SubscriptionDto> UpdateAsync(string id, IEnumerable<string> channels, int intervalMs, CancellationToken ct = default)
        => PutAsync<SubscriptionDto>("api/subscriptions/" + Uri.EscapeDataString(id), new { Channels = channels.ToArray(), IntervalMs = intervalMs }, ct);

    /// <summary>Ends the subscription.</summary>
    public new Task DeleteAsync(string id, CancellationToken ct = default)
        => base.DeleteAsync("api/subscriptions/" + Uri.EscapeDataString(id), ct);
}
