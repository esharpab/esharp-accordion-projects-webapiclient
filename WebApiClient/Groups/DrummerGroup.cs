using AccordionQ2.WebApiClient.Internal;
using AccordionQ2.WebApiClient.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AccordionQ2.WebApiClient.Groups;

/// <summary>
/// The Drummer modules on a station: IEEE 1687 instruments in an FPGA over JTAG (Extensions/Drummer in the firmware).
/// </summary>
/// <remarks>
/// Every instrument operation the station's Web UI has (arm the logic analyser, an I2C transfer, set an MMCM output,
/// start a memory test) is a transaction on the instrument's Call channel (<c>Drummer.MEM.Call</c>) whose answer is
/// the operation's result. <see cref="DrummerTarget"/> runs them and follows a long one to its end.
/// <code>
/// var refs = await client.Drummer.FindAsync();                      // [Drummer]
/// var t = client.Drummer.Target(refs[0].Prefix);
/// var r = await t.CallAsync("MEM", "mem_start", new { pattern = "PRBS31", count = 65536 });
/// var freq = await t.ReadAsync("CLK.Freq0");                         // "233250000"
/// </code>
/// Needs firmware with the capability <see cref="CapabilitiesDto.ResourcesTransact"/> (the Drummer phase 3 firmware).
/// </remarks>
public sealed class DrummerGroup : ApiGroupBase
{
    private readonly ResourcesGroup _resources;
    private readonly InstrumentsGroup _instruments;
    private readonly ChannelsGroup _channels;
    private readonly TransactCheck _check;

    internal DrummerGroup(ApiTransport transport) : base(transport)
    {
        _resources = new ResourcesGroup(transport);
        _instruments = new InstrumentsGroup(transport);
        _channels = new ChannelsGroup(transport);
        _check = new TransactCheck(new CapabilitiesGroup(transport));
    }

    /// <summary>The instrument type a Drummer module reports (an Instrument channel named "{prefix}.Drummer").</summary>
    public const string InstrumentType = "Drummer";

    /// <summary>
    /// The Drummer modules: one per Instrument of type Drummer, else one per <c>Target.Status</c> with a
    /// <c>Target.Package</c> beside it (firmware from before the instrument type), as the Web UI finds them.
    /// </summary>
    public async Task<List<DrummerModuleRef>> FindAsync(CancellationToken ct = default)
    {
        var found = new List<DrummerModuleRef>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        List<InstrumentDto> instruments;
        try
        {
            instruments = await _instruments.GetAllAsync(ct).ConfigureAwait(false);
        }
        catch (AccordionQ2ApiException ex) when (ex.StatusCode == 404)
        {
            instruments = new List<InstrumentDto>();
        }
        foreach (var i in instruments.Where(i => i.Type == InstrumentType))
        {
            int dot = i.NetName.LastIndexOf('.');
            if (dot <= 0) continue;
            string prefix = i.NetName.Substring(0, dot);
            if (seen.Add(prefix))
                found.Add(new DrummerModuleRef(prefix, string.IsNullOrEmpty(i.InstrumentName) ? prefix : i.InstrumentName));
        }
        if (found.Count > 0) return found;
        const string status = ".Target.Status";
        var names = new HashSet<string>(await _resources.GetNamesAsync(ct).ConfigureAwait(false), StringComparer.Ordinal);
        foreach (var name in names.Where(n => n.EndsWith(status, StringComparison.Ordinal)).OrderBy(n => n, StringComparer.Ordinal))
        {
            string prefix = name.Substring(0, name.Length - status.Length);
            if (names.Contains(prefix + ".Target.Package") && seen.Add(prefix)) found.Add(new DrummerModuleRef(prefix, prefix));
        }
        return found;
    }

    /// <summary>The module at <paramref name="prefix"/> ("Drummer"). Makes no request.</summary>
    public DrummerTarget Target(string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix)) throw new ArgumentException("give the module's prefix, e.g. \"Drummer\"", nameof(prefix));
        return new DrummerTarget(prefix.Trim().TrimEnd('.'), _resources, _check, _channels);
    }
}

/// <summary>A Drummer module found on the station.</summary>
public sealed class DrummerModuleRef
{
    public DrummerModuleRef(string prefix, string name)
    {
        Prefix = prefix;
        Name = name;
    }

    /// <summary>The net name prefix of its channels: the instance's configured name, "Drummer" or "DUT_A".</summary>
    public string Prefix { get; }

    /// <summary>Its instrument's name, or the prefix.</summary>
    public string Name { get; }

    public override string ToString() => Prefix;
}

/// <summary>
/// One Drummer module: its Target channels, its instruments' operations, field and semantic channels.
/// </summary>
/// <remarks>
/// <para>An instrument is named by its segment or its SIB: <c>"MEM"</c> or <c>"SIB_MEM"</c> (the leading <c>SIB_</c> is
/// dropped). An operation's name and arguments are the Python console's (Drummer/CONTROL.md section 6); the answer is
/// its result with <c>ms</c> (time in the module) and <c>seq</c> (the module's TAP hold number) added.</para>
/// <para>Errors: a refused request is an <see cref="AccordionQ2ApiException"/> with <c>StatusCode</c> 400; <b>409 is
/// busy, try again</b>: something else holds the JTAG (another operation, a job, a bitfile load or the self-test), named
/// in the message; 500 is an instrument or JTAG fault. The station answers an operation within 15 s; one that runs
/// longer becomes a job, which <see cref="CallAsync"/> follows (<c>{"call": "job"}</c>) up to its timeout.</para>
/// <para>Roles: reads need Viewer; operations and writes Operator. Every operation is one line in the station's audit
/// log, so poll with <see cref="SnapshotAsync"/> or a field read, not with operations.</para>
/// </remarks>
public sealed class DrummerTarget
{
    /// <summary>How long one request waits in the module before it answers with a job (the module's default).</summary>
    public const int WaitMs = 10_000;

    /// <summary>The overall timeout of <see cref="CallAsync"/> when none is given.</summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(60);

    private readonly ResourcesGroup _resources;
    private readonly TransactCheck _check;
    private readonly ChannelsGroup? _channels;

    internal DrummerTarget(string prefix, ResourcesGroup resources, TransactCheck check, ChannelsGroup? channels = null)
    {
        Prefix = prefix;
        _resources = resources;
        _check = check;
        _channels = channels;
    }

    /// <summary>How long <see cref="UploadPackageAsync"/> waits for the package to be kept and selected when no timeout is given.</summary>
    public static readonly TimeSpan UploadTimeout = TimeSpan.FromSeconds(60);

    /// <summary>The instance's name, its channels' prefix: "Drummer".</summary>
    public string Prefix { get; }

    /// <summary>The full net name of a channel below the module: "Drummer." + <paramref name="name"/>.</summary>
    public string Net(string name) => Prefix + "." + name;

    /// <summary>"MEM" for "MEM" and "SIB_MEM".</summary>
    public static string Segment(string instrument)
    {
        if (instrument is null) throw new ArgumentNullException(nameof(instrument));
        string s = instrument.Trim();
        if (s.StartsWith("SIB_", StringComparison.Ordinal)) s = s.Substring(4);
        if (s.Length == 0) throw new ArgumentException("give an instrument: a segment (\"MEM\") or a SIB (\"SIB_MEM\")", nameof(instrument));
        return s;
    }

    /// <summary>Target.Status: connected, the design, the error.</summary>
    public async Task<JObject> StatusAsync(CancellationToken ct = default) => ParseObject(await ReadAsync("Target.Status", ct).ConfigureAwait(false), "Target.Status");

    /// <summary>Target.Info: the design, the scan chain, every instrument with its register map and tool facts (no scan).</summary>
    public async Task<JObject> InfoAsync(CancellationToken ct = default) => ParseObject(await ReadAsync("Target.Info", ct).ConfigureAwait(false), "Target.Info");

    /// <summary>Target.Attention: the ATTN word, the instruments asking for attention and why. A read keeps the module's ATTN poll going for 2 s.</summary>
    public async Task<JObject> AttentionAsync(CancellationToken ct = default) => ParseObject(await ReadAsync("Target.Attention", ct).ConfigureAwait(false), "Target.Attention");

    /// <summary>Target.Jobs: the running jobs and the ended ones not fetched yet.</summary>
    public async Task<JArray> JobsAsync(CancellationToken ct = default)
    {
        string text = await ReadAsync("Target.Jobs", ct).ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(text) ? new JArray() : JArray.Parse(text);
    }

    /// <summary>
    /// The instrument's Snapshot channel: its fields, status and why it asks for attention. While something else holds
    /// the JTAG it answers the last capture with <c>stale</c> and <c>age_ms</c>; <c>fields</c> is null before a first read.
    /// </summary>
    public async Task<JObject> SnapshotAsync(string instrument, CancellationToken ct = default)
    {
        string name = Segment(instrument) + ".Snapshot";
        return ParseObject(await ReadAsync(name, ct).ConfigureAwait(false), name);
    }

    /// <summary>
    /// Runs an operation on the instrument's Call channel and returns its result; a job is followed to its end.
    /// </summary>
    /// <param name="instrument">"MEM" or "SIB_MEM".</param>
    /// <param name="name">The operation: "mem_start", "clock_measure", "mmcm_set", ...</param>
    /// <param name="args">Its arguments: an anonymous object (<c>new { gate = 1200000, channel = 2 }</c>), a dictionary or
    /// a JObject, with the console's keys (an argument named <c>op</c> or <c>name</c> passes as any other).</param>
    /// <param name="timeout">How long to follow a job (default 60 s). After it a <see cref="TimeoutException"/> names the
    /// job's id; the job goes on in the module, and its result can be fetched for 5 minutes after it ends.</param>
    /// <param name="ct">Cancels the wait; a job already started goes on in the module.</param>
    /// <exception cref="NotSupportedException">The station's firmware runs transactions for Admin only, under the engine
    /// lock (no <see cref="CapabilitiesDto.ResourcesTransact"/>); nothing was sent.</exception>
    /// <exception cref="AccordionQ2ApiException">400: the request is wrong; 409: busy, try again; 500: a fault.</exception>
    public Task<JObject> CallAsync(string instrument, string name, object? args = null, TimeSpan? timeout = null, CancellationToken ct = default) =>
        RunAsync(Segment(instrument) + ".Call", name, args, timeout, ct);

    /// <summary>
    /// A module operation on Target.Call (attention, attention_detail, la_group_*), or an instrument's operation with
    /// <c>"sib"</c> in <paramref name="args"/>. Otherwise as <see cref="CallAsync"/>.
    /// </summary>
    public Task<JObject> ModuleCallAsync(string name, object? args = null, TimeSpan? timeout = null, CancellationToken ct = default) =>
        RunAsync("Target.Call", name, args, timeout, ct);

    /// <summary>
    /// The JTAG port the instance is bound to now (<c>Target.Port</c>, firmware with stable module names):
    /// <c>"MicroHAPS.JTAG.A"</c>, or <c>""</c> when it is unbound.
    /// </summary>
    public async Task<string> GetPortAsync(CancellationToken ct = default) =>
        (await ReadAsync("Target.Port", ct).ConfigureAwait(false)).Trim();

    /// <summary>
    /// Binds the instance to a JTAG port now (<c>Target.Port</c>), giving back the one it held; <c>""</c> or null
    /// unbinds. 409 when another instance claims the port (the message names it), or while a bitfile load, the self-test
    /// or a job holds the TAP. Not saved: the module's <c>Port</c> setting stays the binding at its next load. The
    /// station's ports are <see cref="JtagPortsGroup.GetAllAsync"/>.
    /// </summary>
    public Task BindPortAsync(string? port, CancellationToken ct = default) =>
        WriteAsync("Target.Port", port?.Trim() ?? string.Empty, ct);

    // -- the package library and build identity (firmware's Drummer/PACKAGES.md) -------------------

    /// <summary>
    /// <c>Target.Library</c>: the station's package library and this instance's selection, from the module's memory (no
    /// scan): <c>dir</c>, <c>bytes</c>, <c>free_bytes</c>, <c>floor_bytes</c>, <c>selection</c> (<c>source</c> "library",
    /// "PackagePath" or "none", with <c>sha256</c>, <c>file</c>, <c>how</c>, <c>at</c>, <c>by</c>) and <c>packages</c>,
    /// newest upload first (each entry's <c>sha256</c>, <c>file</c>, <c>design</c>, <c>design_id</c>, <c>usr_access</c>,
    /// <c>usr_access_time</c>, <c>size</c>, <c>uploaded</c>, <c>uploaded_by</c>, <c>selected_by</c>,
    /// <c>stale_selected_by</c>, <c>fpga_runs</c>). 404 on firmware without the library.
    /// </summary>
    public async Task<JObject> LibraryAsync(CancellationToken ct = default) =>
        ParseObject(await ReadAsync("Target.Library", ct).ConfigureAwait(false), "Target.Library");

    /// <summary>
    /// Selects a library package for this instance (<c>Target.Select</c>, Operator): its full SHA-256 or 8 or more of
    /// its first hex digits. The selection is kept across restarts; the design's channels are built again, and under
    /// the module's <c>LoadOnSelect</c> (default <c>mismatch</c>) its bitfile is loaded when the FPGA does not run that
    /// build or the station can't tell. The write returns before the connect; follow <see cref="StatusAsync"/> (its
    /// <c>build</c>) and <c>Target.LoadStatus</c>. 400 for an unknown or ambiguous one or a package that does not load
    /// (the old selection stays); 409 while a bitfile load or the self-test runs, or the JTAG is held.
    /// </summary>
    /// <exception cref="ArgumentException">Not 8 to 64 hex digits; nothing was sent.</exception>
    public Task SelectPackageAsync(string sha256, CancellationToken ct = default) =>
        WriteAsync("Target.Select", ShaText(sha256, nameof(sha256)), ct);

    /// <summary>
    /// Unselects (<c>Target.Select</c> = <c>""</c>): the instance falls back to its <c>PackagePath</c> setting (under
    /// <c>LoadOnSelect</c>), or to no package (the design's channels go, nothing is loaded). 409 as a select.
    /// </summary>
    public Task UnselectPackageAsync(CancellationToken ct = default) => WriteAsync("Target.Select", string.Empty, ct);

    /// <summary>
    /// Deletes a package from the station's library (<c>Target.Delete</c>, Operator): a full SHA-256 or 8 or more of its
    /// first hex digits. 409 while a configured instance selects it (the message names the instances: select another
    /// package there first); 400 for an unknown or ambiguous one. It can't be undone: upload the file again.
    /// </summary>
    /// <exception cref="ArgumentException">Not 8 to 64 hex digits; nothing was sent.</exception>
    public Task DeletePackageAsync(string sha256, CancellationToken ct = default) =>
        WriteAsync("Target.Delete", ShaText(sha256, nameof(sha256)), ct);

    /// <summary>
    /// Uploads a <c>.drummer</c> package to this instance (<c>Target.Package</c>, the developer flow): the station checks
    /// it, keeps it in its library, selects it and, under <c>LoadOnSelect</c> <c>mismatch</c>, loads its bitfile when the
    /// FPGA does not run that build, then discovers and connects. Returns when the package is kept and selected (the
    /// status <c>done</c>, its <c>Message</c> naming the library entry) or refused (<c>failed</c>, returned, not thrown,
    /// with the reason); the connect and any load follow: <see cref="StatusAsync"/>, <see cref="BuildAsync"/>,
    /// <c>Target.LoadStatus</c>. Throws <see cref="AccordionQ2ApiException"/> as
    /// <see cref="ChannelsGroup.UploadByteStreamAsync(string, string, string, bool, CancellationToken)"/> does, and
    /// <see cref="TimeoutException"/> after <paramref name="timeout"/> (default 60 s).
    /// </summary>
    public async Task<ByteStreamStatusDto> UploadPackageAsync(string path, TimeSpan? timeout = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("give the .drummer package's path", nameof(path));
        if (_channels is null) throw new InvalidOperationException("this DrummerTarget was not made by client.Drummer.Target(prefix)");
        string channel = Net("Target.Package");
        await _channels.UploadByteStreamAsync(channel, path, ct: ct).ConfigureAwait(false);
        return await _channels.WaitByteStreamAsync(channel, timeout ?? UploadTimeout, ct: ct).ConfigureAwait(false);
    }

    /// <summary>
    /// <c>Target.Status</c>'s <c>build</c>: which build the FPGA runs and which the selected package has, and which rule
    /// decided (<c>fpga</c>, <c>package</c>, <c>marker</c>, <c>held_back</c>, <c>runs_package</c> true, false or null,
    /// <c>rule</c>, <c>text</c>, <c>checked</c>, <c>stale</c>). Null on firmware without build identity.
    /// </summary>
    public async Task<JObject?> BuildAsync(CancellationToken ct = default) =>
        (await StatusAsync(ct).ConfigureAwait(false))["build"] as JObject;

    /// <summary>A SHA-256 or 8+ of its first hex digits, as Target.Select and Target.Delete take it.</summary>
    internal static string ShaText(string? sha256, string param)
    {
        string s = sha256?.Trim() ?? string.Empty;
        if (s.Length < 8 || s.Length > 64 || !s.All(Uri.IsHexDigit))
            throw new ArgumentException($"\"{s}\" is not a SHA-256: give the package's SHA-256 or 8 or more of its first hex digits", param);
        return s;
    }

    /// <summary>A field or semantic channel below the module ("GPIO.GPIO8", "CLK.Freq0"). A semantic read may scan: see the
    /// firmware's drummer.md for what each costs.</summary>
    public Task<string> ReadAsync(string name, CancellationToken ct = default) => _resources.GetValueAsync(Net(name), ct);

    /// <summary>Writes a field or semantic channel below the module ("GPIO.GPIO0" = "True"). 409 when the JTAG stays
    /// busy for 250 ms.</summary>
    public Task WriteAsync(string name, string value, CancellationToken ct = default) => _resources.SetValueAsync(Net(name), value, ct);

    // -- the transport of an operation ---------------------------------------------------------

    /// <summary>Milliseconds on a monotonic clock; the tests move it.</summary>
    internal Func<long> Clock { get; set; } =
        () => (long)(System.Diagnostics.Stopwatch.GetTimestamp() * 1000.0 / System.Diagnostics.Stopwatch.Frequency);

    private async Task<JObject> RunAsync(string channel, string name, object? args, TimeSpan? timeout, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("give the operation's name", nameof(name));
        var request = Request(args);
        if (request.ContainsKey("call")) throw new ArgumentException("'call' is the operation's name, not an argument: pass it as name", nameof(args));
        if (request.ContainsKey("job")) throw new ArgumentException("'job' is no operation argument", nameof(args));
        var body = new JObject { ["call"] = name };
        foreach (var p in request.Properties()) body[p.Name] = p.Value;
        if (body["wait_ms"] is null) body["wait_ms"] = WaitMs;

        await _check.EnsureAsync(ct).ConfigureAwait(false);
        string net = Net(channel);
        long limit = (long)(timeout ?? DefaultTimeout).TotalMilliseconds;
        long start = Clock();
        var result = await TransactAsync(net, body, ct).ConfigureAwait(false);
        while (IsRunningJob(result, out string id))
        {
            long left = limit - (Clock() - start);
            if (left <= 0)
                throw new TimeoutException($"{name} on {channel.Replace(".Call", "")} still runs as job {id} after "
                                           + $"{limit / 1000.0:0.#} s; it goes on in the module. Its result: a transaction "
                                           + $"{{\"call\": \"job\", \"job\": \"{id}\"}} on {net}, within 5 minutes of its end");
            var follow = new JObject { ["call"] = "job", ["job"] = id, ["wait_ms"] = (int)Math.Min(WaitMs, left) };
            result = await TransactAsync(net, follow, ct).ConfigureAwait(false);
        }
        return result;
    }

    private async Task<JObject> TransactAsync(string net, JObject body, CancellationToken ct)
    {
        string answer;
        try
        {
            answer = await _resources.TransactAsync(net, body.ToString(Formatting.None), ct).ConfigureAwait(false);
        }
        catch
        {
            _check.Forget();            // the station may be restarting, perhaps into older firmware
            throw;
        }
        return ParseObject(answer, net);
    }

    // A running job's answer: {"job": id, "state": "running", "call", "sib", "progress", "ms"}
    private static bool IsRunningJob(JObject r, out string id)
    {
        id = r["job"] is JValue { Type: JTokenType.String } j ? (string)j! : "";
        return id.Length > 0 && (string?)r["state"] == "running";
    }

    private static JObject Request(object? args)
    {
        if (args is null) return new JObject();
        if (args is string) throw new ArgumentException("give the arguments as an object, a dictionary or a JObject, not a string", nameof(args));
        var token = args as JToken ?? JToken.FromObject(args);
        return token as JObject ?? throw new ArgumentException("the arguments must make a JSON object", nameof(args));
    }

    private static JObject ParseObject(string text, string what)
    {
        try
        {
            return JObject.Parse(text);
        }
        catch (JsonReaderException e)
        {
            throw new AccordionQ2ApiException(200, $"{what} did not answer a JSON object: {e.Message}");
        }
    }
}

/// <summary>Whether the station has <see cref="CapabilitiesDto.ResourcesTransact"/>, asked once a minute and again after a failed call.</summary>
internal sealed class TransactCheck
{
    private const long NotChecked = -1;
    private readonly CapabilitiesGroup _capabilities;
    private long _checkedAt = NotChecked;

    internal TransactCheck(CapabilitiesGroup capabilities) => _capabilities = capabilities;

    /// <summary>How long a check is trusted.</summary>
    internal static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(60);

    internal Func<long> Clock { get; set; } =
        () => (long)(System.Diagnostics.Stopwatch.GetTimestamp() * 1000.0 / System.Diagnostics.Stopwatch.Frequency);

    internal async Task EnsureAsync(CancellationToken ct)
    {
        long at = Interlocked.Read(ref _checkedAt);
        if (at >= 0 && Clock() - at < (long)Lifetime.TotalMilliseconds) return;
        Interlocked.Exchange(ref _checkedAt, NotChecked);
        var capabilities = await _capabilities.GetAsync(ct).ConfigureAwait(false);
        if (!capabilities.Has(CapabilitiesDto.ResourcesTransact))
            throw new NotSupportedException(
                "this station's firmware runs transactions for Admin only, under the engine lock "
                + $"(no feature '{CapabilitiesDto.ResourcesTransact}'): update it to run Drummer operations; nothing was sent");
        Interlocked.Exchange(ref _checkedAt, Clock());
    }

    internal void Forget() => Interlocked.Exchange(ref _checkedAt, NotChecked);
}
