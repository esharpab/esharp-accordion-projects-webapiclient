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

/// <summary>The hardware app's start-up configuration, boot.config (contract section 11).</summary>
public sealed class BootConfigDto
{
    /// <summary>Applied at start-up; when false only <see cref="Services"/> are.</summary>
    public bool Enabled { get; set; }
    public DateTime Modified { get; set; }
    public string? Description { get; set; }
    /// <summary>Loaded after the engine starts, and on every reset, in this order.</summary>
    public List<BootAliasFileDto> AliasFiles { get; set; } = [];
    public List<BootModuleDto> Modules { get; set; } = [];
    /// <summary>Static addresses given beside DHCP.</summary>
    public List<BootAddressDto> IpConfigurations { get; set; } = [];
    public BootWifiDto Wifi { get; set; } = new();
    public bool DisableUsbPorts { get; set; }
    public bool EnableMediaDevices { get; set; }
    public List<BootServiceDto> Services { get; set; } = [];
    /// <summary>Units that can't be turned off here: the hardware app's and the WebApi's.</summary>
    public List<string> ProtectedServices { get; set; } = [];
}

public sealed class BootAliasFileDto
{
    /// <summary>A file name in the alias folder.</summary>
    public string Path { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public int Order { get; set; }
}

public sealed class BootModuleDto
{
    public string Name { get; set; } = string.Empty;
    public string AssemblyPath { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;
    /// <summary>Empty when sent: taken from <see cref="ClassName"/>.</summary>
    public string Namespace { get; set; } = string.Empty;
    public bool Enabled { get; set; }
}

public sealed class BootAddressDto
{
    public string Interface { get; set; } = string.Empty;
    /// <summary>An address, with or without its prefix length, e.g. <c>192.168.0.222/24</c>.</summary>
    public string StaticIp { get; set; } = string.Empty;
    public bool Enabled { get; set; }
}

public sealed class BootWifiDto
{
    public bool Enabled { get; set; }
    public string Ssid { get; set; } = string.Empty;
    /// <summary>The password isn't sent, only whether there is one.</summary>
    public bool PasswordSet { get; set; }
}

public sealed class BootServiceDto
{
    public string Name { get; set; } = string.Empty;
    public string ServiceFile { get; set; } = string.Empty;
    public bool Enabled { get; set; }
}

/// <summary>
/// An edit of boot.config for <c>SetBootAsync</c>: each section set replaces the file's, a null one stays.
/// </summary>
public sealed class BootConfigUpdateDto
{
    /// <summary>The <see cref="BootConfigDto.Modified"/> read; the edit is refused (409) if the file changed since.</summary>
    public DateTime? IfModified { get; set; }
    public bool? Enabled { get; set; }
    public string? Description { get; set; }
    public List<BootAliasFileDto>? AliasFiles { get; set; }
    public List<BootModuleDto>? Modules { get; set; }
    public List<BootAddressDto>? IpConfigurations { get; set; }
    public BootWifiUpdateDto? Wifi { get; set; }
    public bool? DisableUsbPorts { get; set; }
    public bool? EnableMediaDevices { get; set; }
    public List<BootServiceDto>? Services { get; set; }
}

public sealed class BootWifiUpdateDto
{
    public bool Enabled { get; set; }
    public string Ssid { get; set; } = string.Empty;
    /// <summary>A new password; null keeps the one there is, empty clears it.</summary>
    public string? Password { get; set; }
}
