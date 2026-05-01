# Media

Upload, download, and manage media files stored on the device.

## Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `ListFilesAsync(ct?)` | `Task<string[]>` | List all media files on the device. |
| `DownloadFileAsync(fileName, ct?)` | `Task<byte[]>` | Download a media file as raw bytes. |
| `UploadFileAsync(fileName, data, ct?)` | `Task` | Upload a media file (raw bytes). |
| `DeleteFileAsync(fileName, ct?)` | `Task` | Delete a media file from the device. |

## Examples

```csharp
// List all media files
foreach (var f in await client.Media.ListFilesAsync())
    Console.WriteLine(f);

// Download and save locally
byte[] content = await client.Media.DownloadFileAsync("waveform.bin");
await File.WriteAllBytesAsync("waveform.bin", content);

// Upload a file
byte[] data = await File.ReadAllBytesAsync("new_waveform.bin");
await client.Media.UploadFileAsync("new_waveform.bin", data);

// Delete a file
await client.Media.DeleteFileAsync("old_waveform.bin");
```
