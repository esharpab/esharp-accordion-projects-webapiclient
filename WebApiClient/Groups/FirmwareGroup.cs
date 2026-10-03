using AccordionQ2.WebApiClient.Internal;
using AccordionQ2.WebApiClient.Models;

namespace AccordionQ2.WebApiClient.Groups;

/// <summary>
/// Firmware updates through the WebApi (contract section 12), replacing Pilot's over SSH. Changes are writes:
/// while another client holds the lease they are refused with 423.
/// </summary>
public sealed class FirmwareGroup : ApiGroupBase
{
    internal FirmwareGroup(HttpClient http) : base(http) { }

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
    /// hardware app and the WebApi restart, so this client loses the station for a minute or two.
    /// </summary>
    public Task<FirmwareUpdateStatusDto> StartUpdateAsync(string version, bool includeBeta = false, CancellationToken ct = default)
        => PostAsync<FirmwareUpdateStatusDto>("api/system/firmware/update", new { version, includeBeta }, ct);

    /// <summary>How the current or last update stands.</summary>
    public Task<FirmwareUpdateStatusDto> GetUpdateAsync(CancellationToken ct = default)
        => GetAsync<FirmwareUpdateStatusDto>("api/system/firmware/update", ct);

    /// <summary>The last update's log.</summary>
    public async Task<string> GetUpdateLogAsync(CancellationToken ct = default)
        => System.Text.Encoding.UTF8.GetString(await GetBytesAsync("api/system/firmware/update/log", ct));

    /// <summary>Uploads a release package (a rel-&lt;version&gt;.zip), for a station that reaches no source.</summary>
    public Task<FirmwareReleaseDto> UploadPackageAsync(byte[] zip, CancellationToken ct = default)
        => PostBytesAsync<FirmwareReleaseDto>("api/system/firmware/packages", zip, ct);

    /// <summary>Removes a package from the station's cache.</summary>
    public Task DeletePackageAsync(string fileName, CancellationToken ct = default)
        => DeleteAsync($"api/system/firmware/packages/{Uri.EscapeDataString(fileName)}", ct);
}
