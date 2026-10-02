namespace AccordionQ2.WebApiClient.Models;

/// <summary>What the WebApi supports, from <c>GET /api/capabilities</c> (accordionq2 contract section 2).</summary>
public class CapabilitiesDto
{
    /// <summary>Feature name: <c>GET /api/channels/encoded</c> and <c>POST /api/channels/configure/encoded</c>.</summary>
    public const string ChannelsEncoded = "channels.encoded";

    /// <summary>Feature name: <c>GET /api/events</c>.</summary>
    public const string Events = "events";

    /// <summary>The contract version; 0 for a WebApi that predates it.</summary>
    public int ApiVersion { get; set; }

    /// <summary>The WebApi build's informational version.</summary>
    public string WebApiVersion { get; set; } = string.Empty;

    /// <summary>EsharpDefinitions' <c>ChannelFactory.CodecVersion</c> on the server; compare it with your own before using encoded channels.</summary>
    public int CodecVersion { get; set; }

    /// <summary>The supported features. Decide by these, not by version numbers.</summary>
    public string[] Features { get; set; } = Array.Empty<string>();

    /// <summary>Whether the WebApi has <paramref name="feature"/>, e.g. <see cref="Events"/>.</summary>
    public bool Has(string feature) => Array.IndexOf(Features, feature) >= 0;

    /// <summary>What a WebApi without <c>/api/capabilities</c> supports: none of the features.</summary>
    public static CapabilitiesDto None => new CapabilitiesDto();
}

/// <summary>Channels in the byte protocol's binary encoding (contract section 3).</summary>
/// <remarks>
/// <see cref="Data"/> is a TelemetryConfiguration in base64url, as EsharpDefinitions'
/// <c>SerializableHelpers.AsBase64</c> writes it. Decode it with
/// <c>SerializableHelpers.CreateFromBase64&lt;TelemetryConfiguration&gt;(Data).Channels</c>.
/// </remarks>
public class EncodedChannelsDto
{
    public int CodecVersion { get; set; }

    /// <summary>The hardware-app session the channels came from (contract section 4).</summary>
    public long Generation { get; set; }

    public string Data { get; set; } = string.Empty;
}

/// <summary>One event from <c>GET /api/events</c> (contract section 4).</summary>
public class ServerEventDto
{
    /// <summary>Sent once, when the stream opens.</summary>
    public const string Hello = "hello";

    /// <summary>The WebApi's connection to the hardware app went up or down.</summary>
    public const string Connection = "connection";

    /// <summary>Channels were added, removed or changed; reload them.</summary>
    public const string Configuration = "configuration";

    /// <summary>Values for a subscription (contract section 5.3); read the data as <see cref="ValuesEventDto"/>.</summary>
    public const string Values = "values";

    /// <summary>The control lease was taken, released or ran out (contract section 5.5); data as <see cref="LeaseStateDto"/>.</summary>
    public const string Lease = "lease";

    /// <summary>Increases by one per event within a stream; a gap means events were dropped.</summary>
    public long Id { get; set; }

    /// <summary><see cref="Hello"/>, <see cref="Connection"/> or <see cref="Configuration"/>.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The event's data as JSON.</summary>
    public string Data { get; set; } = string.Empty;

    /// <summary>The data as <typeparamref name="T"/>, e.g. <see cref="StreamStateDto"/>.</summary>
    public T DataAs<T>() => Newtonsoft.Json.JsonConvert.DeserializeObject<T>(Data)!;
}

/// <summary>The data of <c>hello</c>, <c>connection</c> and <c>configuration</c> events; fields an event doesn't carry keep their defaults.</summary>
public class StreamStateDto
{
    public bool IsConnected { get; set; }

    public long Generation { get; set; }

    public string? LastError { get; set; }

    /// <summary>In <c>hello</c>.</summary>
    public int ApiVersion { get; set; }

    /// <summary>In <c>hello</c>: the stream's id, for subscribing it to values. Null from a WebApi without subscriptions.</summary>
    public string? StreamId { get; set; }

    /// <summary>In <c>configuration</c>: Added, Removed or Changed.</summary>
    public string? ChangeType { get; set; }
}

/// <summary>Values with their age, from <c>POST /api/resources/values/get</c> with <c>maxAgeMs</c> (contract section 5.2).</summary>
public class ResourceValuesDto
{
    /// <summary>Each requested name and its value.</summary>
    public Dictionary<string, string> Resources { get; set; } = new Dictionary<string, string>();

    /// <summary>How old each value is in milliseconds; 0 when it was just read. Empty from a WebApi without the cache.</summary>
    public Dictionary<string, double> AgeMs { get; set; } = new Dictionary<string, double>();
}

/// <summary>A subscription as created or renewed (contract section 5.3).</summary>
public class SubscriptionDto
{
    public string Id { get; set; } = string.Empty;

    /// <summary>It ends unless renewed within this long.</summary>
    public long ExpiresInMs { get; set; }
}

/// <summary>The data of a <c>values</c> event: one subscription's channels, keyed by the names it was given.</summary>
public class ValuesEventDto
{
    public string Subscription { get; set; } = string.Empty;

    public Dictionary<string, string> Values { get; set; } = new Dictionary<string, string>();

    /// <summary>How old each value is in milliseconds; 0 when it was just read.</summary>
    public Dictionary<string, double> AgeMs { get; set; } = new Dictionary<string, double>();

    /// <summary>Channels that couldn't be read this time, with why; null when all were.</summary>
    public Dictionary<string, string>? Errors { get; set; }
}

/// <summary>Who holds the control lease (contract section 5.5), from <c>GET /api/lease</c>.</summary>
public class LeaseStateDto
{
    public bool Held { get; set; }
    public string? Owner { get; set; }
    public double ExpiresInMs { get; set; }
    public DateTimeOffset? Since { get; set; }
}

/// <summary>A lease this client took or renewed.</summary>
public class LeaseDto
{
    public string LeaseId { get; set; } = string.Empty;
    public string? Owner { get; set; }
    public double ExpiresInMs { get; set; }
}
