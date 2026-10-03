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
    public FirmwareUpdateStatusDto Update { get; set; } = new();
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
}
