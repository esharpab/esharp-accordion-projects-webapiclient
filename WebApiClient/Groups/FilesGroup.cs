using AccordionQ2.WebApiClient.Internal;
using AccordionQ2.WebApiClient.Models;

namespace AccordionQ2.WebApiClient.Groups;

/// <summary>
/// Files in the station's own folders (contract section 10): configuration, alias files, state machines,
/// media, extensions and logs. A path is relative to its folder (<c>root</c>), with forward slashes.
/// </summary>
public sealed class FilesGroup : ApiGroupBase
{
    internal FilesGroup(HttpClient http) : base(http) { }

    /// <summary>The folders the file API reaches.</summary>
    public Task<List<FileRootDto>> GetRootsAsync(CancellationToken ct = default)
        => GetAsync<List<FileRootDto>>("api/files", ct);

    /// <summary>What a folder holds, folders first.</summary>
    public Task<FileListingDto> ListAsync(string root, string path = "", CancellationToken ct = default)
        => GetAsync<FileListingDto>($"api/files/{Uri.EscapeDataString(root)}?path={Uri.EscapeDataString(path)}", ct);

    /// <summary>A file's bytes.</summary>
    public Task<byte[]> DownloadAsync(string root, string path, CancellationToken ct = default)
        => GetBytesAsync($"api/files/{Uri.EscapeDataString(root)}/content?path={Uri.EscapeDataString(path)}", ct);

    /// <summary>Uploads a file; an existing one is replaced only with <paramref name="overwrite"/> (409 otherwise).</summary>
    public Task<FileEntryDto> UploadAsync(string root, string path, byte[] data, bool overwrite = false, CancellationToken ct = default)
        => PutBytesAsync<FileEntryDto>(
            $"api/files/{Uri.EscapeDataString(root)}/content?path={Uri.EscapeDataString(path)}&overwrite={(overwrite ? "true" : "false")}", data, ct);

    /// <summary>Creates a folder.</summary>
    public Task CreateFolderAsync(string root, string path, CancellationToken ct = default)
        => PostAsync($"api/files/{Uri.EscapeDataString(root)}/folder?path={Uri.EscapeDataString(path)}", null, ct);

    /// <summary>Renames or moves a file or folder within its folder.</summary>
    public Task MoveAsync(string root, string from, string to, bool overwrite = false, CancellationToken ct = default)
        => PostAsync($"api/files/{Uri.EscapeDataString(root)}/move", new { from, to, overwrite }, ct);

    /// <summary>Deletes a file, or a folder: a folder with contents only with <paramref name="recursive"/>.</summary>
    public Task DeleteAsync(string root, string path, bool recursive = false, CancellationToken ct = default)
        => base.DeleteAsync($"api/files/{Uri.EscapeDataString(root)}?path={Uri.EscapeDataString(path)}&recursive={(recursive ? "true" : "false")}", ct);
}
