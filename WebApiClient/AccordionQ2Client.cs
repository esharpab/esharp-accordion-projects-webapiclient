using AccordionQ2.WebApiClient.Groups;

namespace AccordionQ2.WebApiClient;

/// <summary>
/// Client for the AccordionQ2 Hardware Management REST API.
/// </summary>
/// <example>
/// <code>
/// using var client = new AccordionQ2Client("http://raspberrypi:5000");
/// var names  = await client.Resources.GetNamesAsync();
/// var status = await client.Application.GetStatusAsync();
/// </code>
/// </example>
public sealed class AccordionQ2Client : IDisposable
{
    private readonly HttpClient _http;
    private readonly bool _ownsClient;

    /// <summary>Resource read/write operations.</summary>
    public ResourcesGroup Resources { get; }

    /// <summary>Channel configuration operations.</summary>
    public ChannelsGroup Channels { get; }

    /// <summary>Module management operations.</summary>
    public ModulesGroup Modules { get; }

    /// <summary>Application lifecycle and configuration file operations.</summary>
    public ApplicationGroup Application { get; }

    /// <summary>Media file operations.</summary>
    public MediaGroup Media { get; }

    /// <summary>Connection status operations.</summary>
    public ConnectionGroup Connection { get; }

    /// <summary>Bus communication operations (I2C, UART, SPI, Socket).</summary>
    public CommGroup Comm { get; }

    /// <summary>Fast numeric sampling operations.</summary>
    public NumericResultsGroup NumericResults { get; }

    /// <summary>Calibration channel read/write operations.</summary>
    public CalibrationGroup Calibration { get; }

    /// <summary>WebApi audit log operations.</summary>
    public AuditGroup Audit { get; }

    /// <summary>Instrument channels with their type and function map.</summary>
    public InstrumentsGroup Instruments { get; }

    /// <summary>What the WebApi supports: API and codec versions, features.</summary>
    public CapabilitiesGroup Capabilities { get; }

    /// <summary>The event stream: connection and channel configuration changes.</summary>
    public EventsGroup Events { get; }

    /// <summary>Value subscriptions, delivered on the event stream.</summary>
    public SubscriptionsGroup Subscriptions { get; }

    /// <summary>The control lease: one client at a time may change the station.</summary>
    public LeaseGroup Lease { get; }

    /// <summary>The station's services, reboot and clock (contract section 9).</summary>
    public SystemGroup System { get; }
    /// <summary>Firmware: releases, the release source and updates (contract section 12).</summary>
    public FirmwareGroup Firmware { get; }

    /// <summary>Files in the station's own folders (contract section 10).</summary>
    public FilesGroup Files { get; }

    /// <summary>
    /// Creates a client that manages its own <see cref="HttpClient"/> lifetime.
    /// </summary>
    /// <param name="baseUrl">Base URL of the WebApi, e.g. <c>http://raspberrypi:5000</c></param>
    public AccordionQ2Client(string baseUrl) : this(baseUrl, null) { }

    /// <summary>
    /// Creates a client using an externally managed <see cref="HttpClient"/>.
    /// The caller is responsible for the HttpClient lifetime.
    /// </summary>
    /// <param name="baseUrl">Base URL of the WebApi, e.g. <c>http://raspberrypi:5000</c></param>
    /// <param name="httpClient">Pre-configured HTTP client (e.g. from IHttpClientFactory). Pass <c>null</c> to create one internally.</param>
    public AccordionQ2Client(string baseUrl, HttpClient? httpClient)
    {
        _ownsClient = httpClient is null;
        _http = httpClient ?? new HttpClient();
        _http.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");

        Resources      = new ResourcesGroup(_http);
        Channels       = new ChannelsGroup(_http);
        Modules        = new ModulesGroup(_http);
        Application    = new ApplicationGroup(_http);
        Media          = new MediaGroup(_http);
        Connection     = new ConnectionGroup(_http);
        Comm           = new CommGroup(_http);
        NumericResults = new NumericResultsGroup(_http);
        Calibration    = new CalibrationGroup(_http);
        Audit          = new AuditGroup(_http);
        Capabilities   = new CapabilitiesGroup(_http);
        Events         = new EventsGroup(_http);
        Subscriptions  = new SubscriptionsGroup(_http);
        Lease          = new LeaseGroup(_http);
        System         = new SystemGroup(_http);
        Firmware       = new FirmwareGroup(_http);
        Files          = new FilesGroup(_http);
        Instruments    = new InstrumentsGroup(_http);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_ownsClient) _http.Dispose();
    }
}
