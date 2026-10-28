using AccordionQ2.WebApiClient.Internal;
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

    /// <summary>The header that carries the station API key.</summary>
    public const string ApiKeyHeader = "X-Api-Key";

    /// <summary>
    /// Creates a client that manages its own <see cref="HttpClient"/> lifetime.
    /// </summary>
    /// <param name="baseUrl">Base URL of the WebApi, e.g. <c>http://raspberrypi:5000</c>, <c>https://[::1]:5001</c> or <c>http://[fe80::1%eth0]:5000</c></param>
    public AccordionQ2Client(string baseUrl) : this(baseUrl, new AccordionQ2ClientOptions()) { }

    /// <summary>
    /// Creates a client using an externally managed <see cref="HttpClient"/>.
    /// The caller is responsible for the HttpClient lifetime.
    /// </summary>
    /// <param name="baseUrl">Base URL of the WebApi, e.g. <c>http://raspberrypi:5000</c></param>
    /// <param name="httpClient">Pre-configured HTTP client (e.g. from IHttpClientFactory). Pass <c>null</c> to create one internally.</param>
    public AccordionQ2Client(string baseUrl, HttpClient? httpClient)
        : this(baseUrl, new AccordionQ2ClientOptions { HttpClient = httpClient }) { }

    /// <summary>
    /// Creates a client with an API key, HTTPS certificate trust and, optionally, the caller's
    /// <see cref="HttpClient"/> or handler.
    /// </summary>
    /// <param name="baseUrl">Base URL of the WebApi, e.g. <c>https://station-12:5001</c>. IPv6 literals work in
    /// brackets, with a zone id as <c>%eth0</c> or <c>%25eth0</c>.</param>
    /// <param name="options">The API key and certificate trust; see <see cref="AccordionQ2ClientOptions"/>.</param>
    /// <exception cref="ArgumentException">The base URL, key or fingerprint is malformed, or certificate options
    /// were given with the caller's own HttpClient or handler. The message never contains the key.</exception>
    /// <example>
    /// <code>
    /// using var client = new AccordionQ2Client("https://station-12:5001", new AccordionQ2ClientOptions
    /// {
    ///     ApiKey = Environment.GetEnvironmentVariable("ACCORDION_API_KEY"),
    ///     CertificateSha256 = "3F:A2:...:9C",
    /// });
    /// </code>
    /// </example>
    public AccordionQ2Client(string baseUrl, AccordionQ2ClientOptions options)
    {
        if (options is null) throw new ArgumentNullException(nameof(options));
        var baseAddress = ParseBaseUrl(baseUrl);
        var trustOptions = options.CertificateSha256 is not null || options.TrustedCaCertificate is not null;
        if (options.HttpClient is not null && options.Handler is not null)
            throw new ArgumentException("Give either an HttpClient or a Handler, not both", nameof(options));
        if (trustOptions && (options.HttpClient is not null || options.Handler is not null))
            throw new ArgumentException(
                "Certificate trust can't be added to the caller's own HttpClient or handler; build the handler with AccordionQ2Client.CreateHandler",
                nameof(options));

        HttpClient http;
        if (options.HttpClient is not null)
        {
            _ownsClient = false;
            http = options.HttpClient;
        }
        else
        {
            _ownsClient = true;
            http = options.Handler is not null
                ? new HttpClient(options.Handler, disposeHandler: false)
                : new HttpClient(CreateHandler(options), disposeHandler: true);
        }

        // Validates the key before anything uses it, so a bad one fails here and not on the first request.
        ApiTransport transport;
        try { transport = new ApiTransport(http, baseAddress, options.ApiKey); }
        catch { if (_ownsClient) http.Dispose(); throw; }

        // The base address stays in the transport, which makes each request URI absolute, so a caller's
        // HttpClient isn't changed (its BaseAddress included) and may serve several stations.
        _http = http;

        Resources      = new ResourcesGroup(transport);
        Channels       = new ChannelsGroup(transport);
        Modules        = new ModulesGroup(transport);
        Application    = new ApplicationGroup(transport);
        Media          = new MediaGroup(transport);
        Connection     = new ConnectionGroup(transport);
        Comm           = new CommGroup(transport);
        NumericResults = new NumericResultsGroup(transport);
        Calibration    = new CalibrationGroup(transport);
        Audit          = new AuditGroup(transport);
        Capabilities   = new CapabilitiesGroup(transport);
        Events         = new EventsGroup(transport);
        Subscriptions  = new SubscriptionsGroup(transport);
        Lease          = new LeaseGroup(transport);
        System         = new SystemGroup(transport);
        Firmware       = new FirmwareGroup(transport);
        Files          = new FilesGroup(transport);
        Instruments    = new InstrumentsGroup(transport);
    }

    /// <summary>
    /// A handler that applies <paramref name="options"/>' certificate trust (<see cref="AccordionQ2ClientOptions.CertificateSha256"/>,
    /// <see cref="AccordionQ2ClientOptions.TrustedCaCertificate"/>), for a caller that builds its own HttpClient, e.g.
    /// <c>AddHttpClient("accordion").ConfigurePrimaryHttpMessageHandler(() =&gt; AccordionQ2Client.CreateHandler(options))</c>.
    /// The API key isn't part of the handler: pass it in the options given to the constructor.
    /// The handler doesn't follow redirects, so the key never goes to a host a <c>Location</c> header names;
    /// a 3xx answer throws <see cref="AccordionQ2ApiException"/>.
    /// </summary>
    /// <remarks>Certificate trust needs .NET Framework 4.7.1 or later on .NET Framework.</remarks>
    public static HttpClientHandler CreateHandler(AccordionQ2ClientOptions options)
    {
        if (options is null) throw new ArgumentNullException(nameof(options));
        // .NET drops only Authorization on a redirect; X-Api-Key and X-Lease-Id would go to the new host.
        // The station's API never redirects.
        var handler = new HttpClientHandler { AllowAutoRedirect = false };
        if (options.CertificateSha256 is not null || options.TrustedCaCertificate is not null)
        {
            var callback = AccordionQ2Certificates.CreateValidationCallback(options.CertificateSha256, options.TrustedCaCertificate);
            handler.ServerCertificateCustomValidationCallback = callback;
        }
        return handler;
    }

    /// <summary>
    /// <paramref name="baseUrl"/> as an absolute http(s) URI ending in '/'. An IPv6 zone id may be written as
    /// RFC 6874's <c>%25eth0</c> or as <c>%eth0</c>; <see cref="Uri"/> takes only the latter.
    /// </summary>
    internal static Uri ParseBaseUrl(string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new ArgumentException("The base URL is empty", nameof(baseUrl));
        var text = ZoneId.Replace(baseUrl.Trim().TrimEnd('/'), "%") + "/";
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new ArgumentException(
                "The base URL must be an absolute http:// or https:// URL, e.g. http://station:5000 or http://[fe80::1%eth0]:5000",
                nameof(baseUrl));
        if (!string.IsNullOrEmpty(uri.UserInfo))
            throw new ArgumentException("The base URL must not carry credentials; use AccordionQ2ClientOptions.ApiKey", nameof(baseUrl));
        return uri;
    }

    // "%25" inside the brackets of an IPv6 literal, followed by a zone id.
    private static readonly global::System.Text.RegularExpressions.Regex ZoneId =
        new global::System.Text.RegularExpressions.Regex(@"(?<=\[[0-9A-Fa-f:.]+)%25(?=[^\]]+\])");

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_ownsClient) _http.Dispose();
    }
}
