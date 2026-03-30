using AccordionQ2.WebApiClient.Internal;

namespace AccordionQ2.WebApiClient.Groups;

/// <summary>Operations for managing media files stored on the device.</summary>
public sealed class MediaGroup : ApiGroupBase
{
    internal MediaGroup(HttpClient http) : base(http) { }

    /// <summary>Lists all available media files.</summary>
    public Task<string[]> ListFilesAsync(CancellationToken ct = default)
        => GetAsync<string[]>("api/media", ct);

    /// <summary>Downloads a media file as raw bytes.</summary>
    public Task<byte[]> DownloadFileAsync(string fileName, CancellationToken ct = default)
        => GetBytesAsync($"api/media/{Uri.EscapeDataString(fileName)}", ct);

    /// <summary>Uploads a media file to the device.</summary>
    public Task UploadFileAsync(string fileName, byte[] data, CancellationToken ct = default)
        => PostMultipartAsync("api/media/upload", fileName, data, ct);

    /// <summary>Deletes a media file from the device.</summary>
    public Task DeleteFileAsync(string fileName, CancellationToken ct = default)
        => DeleteAsync($"api/media/{Uri.EscapeDataString(fileName)}", ct);
}
