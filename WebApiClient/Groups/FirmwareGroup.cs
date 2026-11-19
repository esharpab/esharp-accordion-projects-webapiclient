using AccordionQ2.WebApiClient.Internal;
using AccordionQ2.WebApiClient.Models;

namespace AccordionQ2.WebApiClient.Groups;

/// <summary>
/// Firmware updates through the WebApi (contract section 12), replacing Pilot's over SSH. Changes are writes:
/// while another client holds the lease they are refused with 423.
/// </summary>
public sealed class FirmwareGroup : ApiGroupBase
{
    internal FirmwareGroup(ApiTransport transport) : base(transport) { }

    /// <summary>The installed version, the release source and how the last update went.</summary>
    public Task<FirmwareStateDto> GetStateAsync(CancellationToken ct = default)
        => GetAsync<FirmwareStateDto>("api/system/firmware", ct);

    /// <summary>The source's releases, newest first, with the cached (uploaded) packages; revoked ones left out.</summary>
    public Task<FirmwareReleasesDto> GetReleasesAsync(bool includeBeta = false, CancellationToken ct = default)
        => GetAsync<FirmwareReleasesDto>($"api/system/firmware/releases?includeBeta={(includeBeta ? "true" : "false")}", ct);

    /// <summary>Sets the release source: an http(s) address or an absolute folder on the Pi; null for the default.</summary>
    public Task<FirmwareSourceDto> SetSourceAsync(string? location, CancellationToken ct = default)
        => PutAsync<FirmwareSourceDto>("api/system/firmware/source", new { location }, ct);

    /// <summary>
    /// Starts installing <paramref name="version"/> and returns at once; follow <see cref="GetUpdateAsync"/>. The
    /// hardware app and the WebApi restart, so this client loses the station for a minute or two. Refused at once
    /// (400) below the minimum version, (409) while an update runs and (501) off a station; anything found after the
    /// answer (an unknown version, a damaged download, a package not signed by E-Sharp) ends the update as
    /// <c>failed</c>, with the reason in <see cref="FirmwareUpdateStatusDto.Message"/>.
    /// </summary>
    public Task<FirmwareUpdateStatusDto> StartUpdateAsync(string version, bool includeBeta = false, CancellationToken ct = default)
        => PostAsync<FirmwareUpdateStatusDto>("api/system/firmware/update", new { version, includeBeta }, ct);

    /// <summary>How the current or last update stands.</summary>
    public Task<FirmwareUpdateStatusDto> GetUpdateAsync(CancellationToken ct = default)
        => GetAsync<FirmwareUpdateStatusDto>("api/system/firmware/update", ct);

    /// <summary>The last update's log.</summary>
    public async Task<string> GetUpdateLogAsync(CancellationToken ct = default)
        => System.Text.Encoding.UTF8.GetString(await GetBytesAsync("api/system/firmware/update/log", ct));

    /// <summary>Uploads a release package (a rel-&lt;version&gt;.zip), e.g. from a folder on the user's computer; 400 below the minimum version.</summary>
    public Task<FirmwareReleaseDto> UploadPackageAsync(byte[] zip, CancellationToken ct = default)
        => PostBytesAsync<FirmwareReleaseDto>("api/system/firmware/packages", zip, ct);

    /// <summary>
    /// Sets the automatic update (Admin; firmware 7.1.0): <paramref name="mode"/> <c>off</c>, <c>check</c> or <c>security</c>,
    /// <paramref name="window"/> <c>HH:mm-HH:mm</c> in station time; null puts either back to the station's default. 400 for
    /// another value; 404 before 7.1.0.
    /// </summary>
    public Task<AutoUpdateDto> SetAutoUpdateAsync(string? mode, string? window, CancellationToken ct = default)
        => PutAsync<AutoUpdateDto>("api/system/firmware/auto-update", new { mode, window }, ct);

    /// <summary>Reads the release list now, as the automatic check does; installs nothing (Admin; firmware 7.1.0).</summary>
    public Task<AutoUpdateDto> CheckAutoUpdateAsync(CancellationToken ct = default)
        => PostAsync<AutoUpdateDto>("api/system/firmware/auto-update/check", null, ct);

    /// <summary>
    /// Applies the Debian security updates the firmware carries now, and removes unused .NET runtimes (Admin; firmware 7.1.0).
    /// Answers with the record; follow <see cref="FirmwareStateDto.OsUpdates"/>. 409 when there is nothing to do (unless
    /// <paramref name="force"/>) or while an OS update, runtime install, firmware update or factory reset runs; 501 where
    /// it doesn't apply; 507 without the disk space.
    /// </summary>
    public Task<OsUpdateRecordDto> InstallOsUpdatesAsync(bool force = false, CancellationToken ct = default)
        => PostAsync<OsUpdateRecordDto>("api/system/firmware/os-updates/install", new { force }, ct);

    /// <summary>The OS update's log (Admin; firmware 7.1.0).</summary>
    public async Task<string> GetOsUpdatesLogAsync(CancellationToken ct = default)
        => System.Text.Encoding.UTF8.GetString(await GetBytesAsync("api/system/firmware/os-updates/log", ct));

    /// <summary>The installed firmware's SBOM, CycloneDX 1.6 JSON (Viewer; firmware 7.1.0, FW7 CRA-11). 404 before, or without one.</summary>
    public async Task<string> GetSbomAsync(CancellationToken ct = default)
        => System.Text.Encoding.UTF8.GetString(await GetBytesAsync("api/system/firmware/sbom", ct));

    /// <summary>Removes a package from the station's cache.</summary>
    public Task DeletePackageAsync(string fileName, CancellationToken ct = default)
        => DeleteAsync($"api/system/firmware/packages/{Uri.EscapeDataString(fileName)}", ct);
}
