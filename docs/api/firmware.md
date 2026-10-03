# Firmware

Update a station's firmware through the WebApi: list the releases, choose where they come from, install one, and follow how it goes.

- **Signed releases only.** A station installs only releases signed by E-Sharp. The station checks the signature itself, before anything is unpacked, whichever way the package arrives: downloaded, from a folder on the Pi, or uploaded. No client can skip the check. An unsigned or altered package is refused with 400 on upload; one that comes from the release source ends the update as `failed` before anything is unpacked.
- **Minimum version 6.0.0.** Older releases have no browser GUI or update API. They are still listed, with `Installable == false`, but installing or uploading one is refused with 400. Any version from the minimum up may be installed, older or the same as the current one included.
- **Installing restarts the station's software.** `StartUpdateAsync` returns at once. While installing, the hardware app and the WebApi restart, so for a minute or two every call fails with `HttpRequestException` (connection refused or reset) or a timeout. Poll `GetUpdateAsync` and tolerate those errors; see the [example](#example-updating-a-station).
- Changes (`SetSourceAsync`, `StartUpdateAsync`, `UploadPackageAsync`, `DeletePackageAsync`) are writes: while another client holds the [lease](lease.md) they are refused with 423.
- Off a station (a WebApi on a development PC) `Supported` is false and updates answer 501.

## Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `GetStateAsync(ct?)` | `Task<FirmwareStateDto>` | The installed version, the release source, the minimum version and how the last update went. |
| `GetReleasesAsync(includeBeta = false, ct?)` | `Task<FirmwareReleasesDto>` | The source's releases, newest first, with the packages cached on the station. Revoked releases are left out; beta ones unless `includeBeta`. |
| `SetSourceAsync(location, ct?)` | `Task<FirmwareSourceDto>` | Set the release source: an http(s) address or an absolute folder on the Pi; null for the default. |
| `StartUpdateAsync(version, includeBeta = false, ct?)` | `Task<FirmwareUpdateStatusDto>` | Start installing `version` and return at once. Pass `includeBeta: true` for a beta release. |
| `GetUpdateAsync(ct?)` | `Task<FirmwareUpdateStatusDto>` | How the current or last update stands. |
| `GetUpdateLogAsync(ct?)` | `Task<string>` | The last update's log, as text. |
| `UploadPackageAsync(zip, ct?)` | `Task<FirmwareReleaseDto>` | Upload a release package (`rel-<version>.zip`, at most 256 MB) into the station's cache. |
| `DeletePackageAsync(fileName, ct?)` | `Task` | Remove a package from the station's cache. |

## Release Sources

By default releases come from E-Sharp's deployment on the internet (`DefaultSource`). A source can also be an absolute folder on the Pi holding the same files (`releases.json` and the packages beside it), such as a USB stick or a mounted share. The source setting survives updates.

```csharp
// Use a USB stick on the Pi
await client.Firmware.SetSourceAsync("/media/usb/accfirmware");

// Back to the default
await client.Firmware.SetSourceAsync(null);
```

If the source can't be read, `GetReleasesAsync` still succeeds: `Error` says why and `Releases` holds only the packages already cached on the station.

## Update States

`FirmwareUpdateStatusDto.State` goes through:

| State | Means |
|-------|-------|
| `idle` | No update has run |
| `downloading` | Getting the package from the cache, the source folder or the deployment; `Bytes` and `TotalBytes` give progress |
| `staging` | Checking the package and its signature, then unpacking it |
| `installing` | The installer has stopped the hardware app and the WebApi and is copying the release; the WebApi is unreachable |
| `succeeded` | Both services stayed up after starting |
| `failed` | The update ended before anything was copied (a bad download, a missing signature, no space, an unknown version); nothing changed |
| `rolledBack` | The services didn't stay up, so the old files were put back |

The restarted WebApi reports how it went, so polling after the restart gives the final state. `GetStateAsync().Current` is `"pending"` after an interrupted update.

## Example: Updating a Station

```csharp
using AccordionQ2.WebApiClient;
using AccordionQ2.WebApiClient.Models;

using var client = new AccordionQ2Client("http://raspberrypi:5000");

var state = await client.Firmware.GetStateAsync();
if (!state.Supported)
    throw new InvalidOperationException("This WebApi can't update firmware (not a station)");
Console.WriteLine($"Installed: {state.Current}, source: {state.Source.Location}, minimum: {state.MinimumVersion}");

var list = await client.Firmware.GetReleasesAsync();
if (list.Error is not null)
    Console.WriteLine($"Source unavailable ({list.Error}); showing cached packages only");

foreach (var r in list.Releases)
    Console.WriteLine($"  {r.Version,-10} installed={r.Installed} downloaded={r.Downloaded} installable={r.Installable}");

var newest = list.Releases.FirstOrDefault(r => r.Installable);
if (newest is null || newest.Installed)
{
    Console.WriteLine("Nothing to install");
    return;
}

try
{
    await client.Firmware.StartUpdateAsync(newest.Version);
}
catch (AccordionQ2ApiException ex) when (ex.StatusCode == 409)
{
    Console.WriteLine($"An update is already running: {ex.Message}");
    return;
}

var result = await WaitForUpdateAsync(client, TimeSpan.FromMinutes(10));
Console.WriteLine($"Update to {result.Version}: {result.State} {result.Message}");
if (result.State != "succeeded")
    Console.WriteLine(await client.Firmware.GetUpdateLogAsync());

// Polls GetUpdateAsync until the update has finished, riding out the minute or two
// in which the hardware app and the WebApi restart and nothing answers.
static async Task<FirmwareUpdateStatusDto> WaitForUpdateAsync(AccordionQ2Client client, TimeSpan limit)
{
    var deadline = DateTime.UtcNow + limit;
    while (DateTime.UtcNow < deadline)
    {
        await Task.Delay(TimeSpan.FromSeconds(2));
        try
        {
            var status = await client.Firmware.GetUpdateAsync();
            if (status.State is "succeeded" or "failed" or "rolledBack")
                return status;

            var progress = status.TotalBytes > 0 ? $" {100 * status.Bytes / status.TotalBytes}%" : "";
            Console.WriteLine($"{status.State}{progress} {status.Message}");
        }
        catch (HttpRequestException)
        {
            Console.WriteLine("Station restarting...");   // connection refused or reset
        }
        catch (TaskCanceledException)
        {
            Console.WriteLine("Station restarting...");   // the request timed out
        }
        catch (AccordionQ2ApiException ex) when (ex.StatusCode >= 500)
        {
            Console.WriteLine("Station restarting...");   // e.g. answering before it is fully up
        }
    }
    throw new TimeoutException("The update didn't finish in time");
}
```

`StartUpdateAsync` itself refuses at once with 400 for a version below the minimum, 409 while another update runs, and 501 off a station. Anything found after it has answered (an unknown version, a damaged download, a package not signed by E-Sharp) ends the update as `failed`, with the reason in `Message`.

## Example: A Station Without Internet

Releases are published at the station's `DefaultSource`, the E-Sharp deployment
`https://esharp.blob.core.windows.net/accfirmware`. A computer with internet can download them for a station
without: `releases.json` lists the releases, and each package is beside it under its `FileName`, for example
`https://esharp.blob.core.windows.net/accfirmware/rel-6.0.0.zip`. Read the address from the station rather than
writing it into a program:

```csharp
var url = (await client.Firmware.GetStateAsync()).DefaultSource;
Console.WriteLine($"{url}/releases.json");
```

Then upload the package from this computer, for example from the folder Pilot downloaded releases into, and install it by the version the upload reports:

```csharp
using AccordionQ2.WebApiClient;
using AccordionQ2.WebApiClient.Models;

FirmwareReleaseDto release;
try
{
    release = await client.Firmware.UploadPackageAsync(File.ReadAllBytes(@"C:\Releases\rel-6.1.0.zip"));
}
catch (AccordionQ2ApiException ex) when (ex.StatusCode == 400)
{
    // Not signed by E-Sharp, altered, not a release package, or below the minimum version
    Console.WriteLine($"Refused: {ex.Message}");
    return;
}

await client.Firmware.StartUpdateAsync(release.Version, includeBeta: release.Beta);
var result = await WaitForUpdateAsync(client, TimeSpan.FromMinutes(10));   // as above
```

The uploaded package stays in the station's cache (listed with `Origin == "uploaded"` and `Downloaded == true`) until you remove it with `DeletePackageAsync(release.FileName)`.

## Models

### `FirmwareStateDto`

| Property | Type | Description |
|----------|------|-------------|
| `Current` | `string?` | The installed version, or null; `"pending"` after an interrupted update |
| `Source` | `FirmwareSourceDto` | The release source in use |
| `DefaultSource` | `string` | The default source's address |
| `Supported` | `bool` | False off a station: updates answer 501 |
| `MinimumVersion` | `string` | The oldest version the WebApi installs (`"6.0.0"`) |
| `Update` | `FirmwareUpdateStatusDto` | How the current or last update stands |

### `FirmwareSourceDto`

| Property | Type | Description |
|----------|------|-------------|
| `Kind` | `string` | `url` or `folder` |
| `Location` | `string` | The address or folder |
| `IsDefault` | `bool` | Whether this is the default source |

### `FirmwareReleasesDto`

| Property | Type | Description |
|----------|------|-------------|
| `Source` | `FirmwareSourceDto` | The source read |
| `Releases` | `List<FirmwareReleaseDto>` | Newest first |
| `Error` | `string?` | Why the source couldn't be read; the list then holds the cached packages only |

### `FirmwareReleaseDto`

| Property | Type | Description |
|----------|------|-------------|
| `Version` | `string` | e.g. `"6.1.0"` |
| `Author` | `string?` | Who released it |
| `ReleaseNotes` | `string?` | Release notes |
| `FileName` | `string` | The package's file name, e.g. `rel-6.1.0.zip` |
| `Beta` | `bool` | A beta release: install with `includeBeta: true` |
| `Revoked` | `bool` | Withdrawn (revoked releases aren't listed) |
| `Installed` | `bool` | The version installed now |
| `Downloaded` | `bool` | Already on the station (its cache or the source folder), so installing needs no download |
| `Origin` | `string` | `catalogue`, or `uploaded` for a package only the cache has |
| `Installable` | `bool` | False below `MinimumVersion`; installing it is refused |

### `FirmwareUpdateStatusDto`

| Property | Type | Description |
|----------|------|-------------|
| `State` | `string` | See [Update States](#update-states) |
| `Version` | `string?` | The version being or last installed |
| `Message` | `string?` | What is happening, or why it failed |
| `Bytes` | `long?` | Downloaded so far |
| `TotalBytes` | `long?` | Package size, when known |
| `Time` | `DateTimeOffset?` | When the status last changed |
