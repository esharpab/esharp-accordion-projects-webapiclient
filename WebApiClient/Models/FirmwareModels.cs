namespace AccordionQ2.WebApiClient.Models;

/// <summary>Firmware on a station (contract section 12).</summary>
public sealed class FirmwareStateDto
{
    /// <summary>The installed version, or null; "pending" after an interrupted update.</summary>
    public string? Current { get; set; }
    public FirmwareSourceDto Source { get; set; } = new();
    public string DefaultSource { get; set; } = string.Empty;
    /// <summary>False off a station (a WebApi on a development PC): updates answer 501.</summary>
    public bool Supported { get; set; }
    /// <summary>The oldest version the WebApi installs (6.0.0): older releases have no browser GUI.</summary>
    public string MinimumVersion { get; set; } = string.Empty;
    public FirmwareUpdateStatusDto Update { get; set; } = new();
    /// <summary>The .NET the WebApi runs and the firmware's bundled runtime (firmware 7.0.5); null before.</summary>
    public RuntimeDto? Runtime { get; set; }
    /// <summary>The automatic update (firmware 7.1.0, FW7 CRA-05); null before.</summary>
    public AutoUpdateDto? AutoUpdate { get; set; }
    /// <summary>The Debian security updates and the removal of unused .NET runtimes (firmware 7.1.0, FW7 CRA-05); null before.</summary>
    public OsUpdatesDto? OsUpdates { get; set; }
    /// <summary>Whether the installed firmware carries its SBOM (<see cref="Groups.FirmwareGroup.GetSbomAsync"/>; firmware 7.1.0, FW7 CRA-11).</summary>
    public bool Sbom { get; set; }
}

/// <summary>The .NET runtime of a station (firmware 7.0.5, FW7 CRA-05).</summary>
public sealed class RuntimeDto
{
    public string NetCore { get; set; } = string.Empty;
    public string? AspNetCore { get; set; }
    /// <summary>The dotnet root the WebApi runs from, e.g. /opt/accordion/runtimes/10.0.14.</summary>
    public string? Root { get; set; }
    /// <summary>The runtime the firmware carries.</summary>
    public string? Bundled { get; set; }
    /// <summary>The patch /opt/accordion/dotnet points at.</summary>
    public string? Active { get; set; }
    public List<string> Installed { get; set; } = [];
    /// <summary>The station's own .NET, e.g. "10.0.12 (/usr/share/dotnet)".</summary>
    public string? System { get; set; }
    public bool Enabled { get; set; }
    /// <summary>Why the bundled runtime isn't installed here (null: it applies).</summary>
    public string? Applicable { get; set; }
    public string? Waiting { get; set; }
    public bool TrialLinkPresent { get; set; }
    public RuntimeInstallDto? Install { get; set; }
    /// <summary>How to put the WebApi back on the station's own .NET.</summary>
    public string? Fallback { get; set; }
}

public sealed class RuntimeInstallDto
{
    /// <summary>requested, installing, installed, notNeeded, deferred, interrupted, failed or unknown.</summary>
    public string State { get; set; } = string.Empty;
    public string? Version { get; set; }
    public int? Attempt { get; set; }
    public string? Trigger { get; set; }
    public string? Message { get; set; }
    public DateTimeOffset? Time { get; set; }
}

/// <summary>The automatic firmware update (firmware 7.1.0, FW7 CRA-05).</summary>
public sealed class AutoUpdateDto
{
    /// <summary><c>off</c>, <c>check</c> (look and show a notice; the default) or <c>security</c> (install security releases in <see cref="Window"/>).</summary>
    public string Mode { get; set; } = "check";
    /// <summary>True when the mode is the station's configured default (no admin setting).</summary>
    public bool ModeIsDefault { get; set; }
    /// <summary>HH:mm-HH:mm in station time, or null for any time.</summary>
    public string? Window { get; set; }
    /// <summary>True when the window is the station's configured one (no admin setting): send null to keep it so.</summary>
    public bool WindowIsDefault { get; set; }
    public DateTimeOffset? LastCheck { get; set; }
    public string? LastCheckError { get; set; }
    /// <summary>The security update, or else the newest newer release, at the last check.</summary>
    public string? Available { get; set; }
    public bool AvailableIsSecurity { get; set; }
    /// <summary>Why an available security update isn't installed now (mode security).</summary>
    public string? Waiting { get; set; }
    /// <summary>The version last installed automatically, and when.</summary>
    public string? LastAttempt { get; set; }
    /// <summary>How that attempt ended: started, or refused, succeeded, failed or rolledBack with the message.</summary>
    public string? LastResult { get; set; }
    public DateTimeOffset? NextCheck { get; set; }
}

/// <summary>The Debian security updates the firmware carries and the removal of unused .NET runtimes (firmware 7.1.0, FW7 CRA-05).</summary>
public sealed class OsUpdatesDto
{
    /// <summary>The bundle's id (e.g. bookworm-20261119T1200), or null when the firmware carries none.</summary>
    public string? Bundle { get; set; }
    public DateTimeOffset? BundleCreated { get; set; }
    public int Packages { get; set; }
    /// <summary>never, requested, applying, applied, partial, failed, notApplicable, interrupted, pruning, pruned or unknown.</summary>
    public string State { get; set; } = "never";
    public string? Message { get; set; }
    public DateTimeOffset? Time { get; set; }
    /// <summary>"name old -> new" per upgraded package.</summary>
    public List<string> Upgraded { get; set; } = [];
    /// <summary>Packages not upgraded, with the reason.</summary>
    public List<string> KeptBack { get; set; } = [];
    /// <summary>An upgraded package (libc6, systemd, ...) takes effect at the next restart of the station; it never reboots by itself.</summary>
    public bool RebootRecommended { get; set; }
    public List<string> RuntimesRemoved { get; set; } = [];
    /// <summary>Why the bundle isn't applied here (null: it applies).</summary>
    public string? Applicable { get; set; }
    public string? Waiting { get; set; }
    /// <summary>OsUpdates:Install.</summary>
    public bool Enabled { get; set; }
    /// <summary>Runtime:RemoveUnused.</summary>
    public bool RemoveUnusedRuntimes { get; set; }
    public long? BytesFreed { get; set; }
    /// <summary>Unused runtime folders the next run removes, as found at the WebApi's start.</summary>
    public List<string>? UnusedRuntimes { get; set; }
}

/// <summary>The OS update's record, as <see cref="Groups.FirmwareGroup.InstallOsUpdatesAsync"/> answers it.</summary>
public sealed class OsUpdateRecordDto
{
    public string? BundleId { get; set; }
    public string? State { get; set; }
    public int Attempt { get; set; }
    public string? Trigger { get; set; }
    public string? Message { get; set; }
    public DateTimeOffset? Time { get; set; }
    /// <summary><c>apply</c> or <c>prune</c> (only unused runtimes).</summary>
    public string? Mode { get; set; }
}

public sealed class FirmwareSourceDto
{
    /// <summary><c>url</c> or <c>folder</c>.</summary>
    public string Kind { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
}

public sealed class FirmwareReleasesDto
{
    public FirmwareSourceDto Source { get; set; } = new();
    public List<FirmwareReleaseDto> Releases { get; set; } = [];
    /// <summary>Why the source couldn't be read; the list then holds the cached packages only.</summary>
    public string? Error { get; set; }
    /// <summary>
    /// The release to install for security fixes (firmware 7.1.0, FW7 CRA-05): the newest installable release newer than the
    /// installed one, when it or a release in between is a security release; null otherwise and before 7.1.0.
    /// </summary>
    public string? SecurityUpdate { get; set; }
}

public sealed class FirmwareReleaseDto
{
    public string Version { get; set; } = string.Empty;
    public string? Author { get; set; }
    public string? ReleaseNotes { get; set; }
    public string FileName { get; set; } = string.Empty;
    public bool Beta { get; set; }
    public bool Revoked { get; set; }
    public bool Installed { get; set; }
    /// <summary>On the station already (in its cache or the source folder), so installing needs no download.</summary>
    public bool Downloaded { get; set; }
    /// <summary><c>catalogue</c>, or <c>uploaded</c> for a package only the cache has.</summary>
    public string Origin { get; set; } = string.Empty;
    /// <summary>False below <see cref="FirmwareStateDto.MinimumVersion"/>; installing it is refused.</summary>
    public bool Installable { get; set; } = true;
    /// <summary>A security release (releases.json <c>Security</c>; firmware 7.1.0 reports it, older firmware leaves it false).</summary>
    public bool Security { get; set; }
}

public sealed class FirmwareUpdateStatusDto
{
    /// <summary>idle, downloading, staging, installing, succeeded, failed, rolledBack.</summary>
    public string State { get; set; } = "idle";
    public string? Version { get; set; }
    public string? Message { get; set; }
    public long? Bytes { get; set; }
    public long? TotalBytes { get; set; }
    public DateTimeOffset? Time { get; set; }
    /// <summary>
    /// What an admin should know after a succeeded update in On (firmware 7.1.0, FW7 HOST-02), e.g. that the firewall
    /// couldn't be loaded; null when there is nothing, and before 7.1.0.
    /// </summary>
    public List<string>? Warnings { get; set; }
}
