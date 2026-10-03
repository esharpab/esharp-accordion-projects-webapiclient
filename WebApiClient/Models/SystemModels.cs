namespace AccordionQ2.WebApiClient.Models;

/// <summary>A service on the station (contract section 9): the hardware app, the WebApi or the dashboard.</summary>
public sealed class ServiceStatusDto
{
    /// <summary><c>hardware</c>, <c>webapi</c> or <c>dashboard</c>.</summary>
    public string Id { get; set; } = string.Empty;
    /// <summary>The systemd unit, e.g. <c>accordion.service</c>.</summary>
    public string Unit { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    /// <summary>False when the Pi doesn't have the unit.</summary>
    public bool Installed { get; set; }
    /// <summary>systemd's state: active, inactive, failed, activating, deactivating.</summary>
    public string ActiveState { get; set; } = string.Empty;
    /// <summary>systemd's detail: running, dead, exited, auto-restart and so on.</summary>
    public string SubState { get; set; } = string.Empty;
    /// <summary>Starts at boot.</summary>
    public bool Enabled { get; set; }
    /// <summary>When it last became active.</summary>
    public DateTimeOffset? Since { get; set; }
    /// <summary>The WebApi answering, which can be restarted but not stopped through itself.</summary>
    public bool Self { get; set; }
    /// <summary>What it can be asked for: start, stop, restart, enable, disable.</summary>
    public List<string> Actions { get; set; } = [];
}

/// <summary>The station's clock (contract section 9).</summary>
public sealed class ClockStatusDto
{
    /// <summary>The Pi's time when it answered.</summary>
    public DateTimeOffset Utc { get; set; }
    public string TimeZone { get; set; } = string.Empty;
    public bool NtpEnabled { get; set; }
    /// <summary>A time server keeps the clock; setting it then needs <c>force</c>.</summary>
    public bool NtpSynchronized { get; set; }
}

/// <summary>A folder the file API reaches (contract section 10).</summary>
public sealed class FileRootDto
{
    /// <summary><c>config</c>, <c>alias</c>, <c>fsms</c>, <c>media</c>, <c>extensions</c>, <c>logs</c>, <c>webapi-logs</c>.</summary>
    public string Id { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Writable { get; set; }
    /// <summary>False when the station doesn't have the folder.</summary>
    public bool Exists { get; set; }
}

/// <summary>A folder's contents.</summary>
public sealed class FileListingDto
{
    public string Root { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public bool Writable { get; set; }
    public List<FileEntryDto> Entries { get; set; } = [];
}

/// <summary>A file or folder in a listing.</summary>
public sealed class FileEntryDto
{
    public string Name { get; set; } = string.Empty;
    public bool Directory { get; set; }
    /// <summary>Bytes; null for a folder.</summary>
    public long? Size { get; set; }
    public DateTimeOffset Modified { get; set; }
}
