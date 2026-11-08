using Newtonsoft.Json;

namespace AccordionQ2.WebApiClient.Models;

/// <summary>
/// One JTAG port on the station (firmware's Drummer.Jtag): a JTAG TAP that a module owns the way to (MicroHAPS's
/// FT4232H interface A, an FTDI cable of the JtagPorts module). Its two channels are <c>&lt;port&gt;.State</c>
/// (Register, Unit <c>jtag-port</c>, the state as JSON) and <c>&lt;port&gt;.Xvc</c> (the XVC server's switch, Admin).
/// </summary>
public class JtagPortDto
{
    /// <summary>The Unit of a port's State channel: how the ports are found in the channel list.</summary>
    public const string StateUnit = "jtag-port";

    /// <summary>The Unit of a port's XVC switch.</summary>
    public const string XvcUnit = "jtag-port:xvc";

    /// <summary>The port's name, the prefix of its two channels: <c>"MicroHAPS.JTAG.A"</c>.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary><c>"MicroHAPS.JTAG.A.State"</c>.</summary>
    public string StateChannel => Name + ".State";

    /// <summary><c>"MicroHAPS.JTAG.A.Xvc"</c>.</summary>
    public string XvcChannel => Name + ".Xvc";

    /// <summary>Its state when it was read; null when the State channel's value was not the JSON expected.</summary>
    public JtagPortStateDto? State { get; set; }

    /// <summary>The State channel's value as read.</summary>
    public string Raw { get; set; } = string.Empty;

    public override string ToString() => Name;
}

/// <summary>A JTAG port's state: the value of its <c>&lt;port&gt;.State</c> channel.</summary>
public class JtagPortStateDto
{
    [JsonProperty("port")] public string Port { get; set; } = string.Empty;

    /// <summary>The module that owns the port: <c>"MicroHAPS"</c>.</summary>
    [JsonProperty("owner")] public string Owner { get; set; } = string.Empty;

    /// <summary>What the port opens: <c>"FT4232H J020324 interface A"</c>.</summary>
    [JsonProperty("label")] public string Label { get; set; } = string.Empty;

    /// <summary>Grows by one at every change; ignore a value older than one you have.</summary>
    [JsonProperty("seq")] public long Seq { get; set; }

    /// <summary><c>"free"</c>, <c>"held"</c> or <c>"down"</c>.</summary>
    [JsonProperty("state")] public string State { get; set; } = string.Empty;

    /// <summary>Who holds it; null when it is free or down.</summary>
    [JsonProperty("holder")] public JtagHolderDto? Holder { get; set; }

    /// <summary>Why it is down (the owner's reason), or null.</summary>
    [JsonProperty("why")] public string? Why { get; set; }

    /// <summary>The Drummer instance bound to it, or null.</summary>
    [JsonProperty("bound")] public string? Bound { get; set; }

    /// <summary>The fastest TCK a holder gets, in Hz.</summary>
    [JsonProperty("max_tck_hz")] public double MaxTckHz { get; set; }

    [JsonProperty("xvc")] public JtagXvcDto? Xvc { get; set; }

    [JsonProperty("since")] public DateTimeOffset? Since { get; set; }
}

/// <summary>Who holds a JTAG port.</summary>
public class JtagHolderDto
{
    /// <summary><c>"borrower"</c> (a Drummer instance), <c>"xvc"</c> (an XVC client) or <c>"owner"</c> (the owner module's own use).</summary>
    [JsonProperty("kind")] public string Kind { get; set; } = string.Empty;

    /// <summary>The borrower's name, the XVC client's address, or the owner.</summary>
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;

    /// <summary>What it does: <c>"an FPGA image load"</c>; null when nothing is said.</summary>
    [JsonProperty("what")] public string? What { get; set; }

    [JsonProperty("since")] public DateTimeOffset? Since { get; set; }
}

/// <summary>A JTAG port's XVC server.</summary>
public class JtagXvcDto
{
    /// <summary>The switch: the server listens.</summary>
    [JsonProperty("enabled")] public bool Enabled { get; set; }

    /// <summary><c>"drummer"</c> (the station's own server) or <c>"openfpgaloader"</c>.</summary>
    [JsonProperty("engine")] public string? Engine { get; set; }

    /// <summary>Where it listens: <c>"0.0.0.0:2542"</c>.</summary>
    [JsonProperty("listen")] public string? Listen { get; set; }

    /// <summary>The client addresses or CIDR ranges served; empty: every client.</summary>
    [JsonProperty("allow")] public List<string> Allow { get; set; } = new List<string>();

    /// <summary>The connected client's address, or null.</summary>
    [JsonProperty("client")] public string? Client { get; set; }

    /// <summary>A client waits for the port (its first shift is held).</summary>
    [JsonProperty("waiting")] public bool Waiting { get; set; }

    [JsonProperty("sessions")] public int Sessions { get; set; }

    /// <summary>Why the last session ended.</summary>
    [JsonProperty("last_end")] public string? LastEnd { get; set; }
}
