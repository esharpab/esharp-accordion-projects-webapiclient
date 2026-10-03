# Files

Files in the station's own folders: configuration, alias files, state machines, media, extensions and logs. Only these folders are reachable, each by an id (`root`). A `path` is relative to its folder, with forward slashes; `""` is the folder itself.

| Root | Folder on a station | Writable |
|------|---------------------|----------|
| `config` | `hw/config`: boot.config, modules.config, log4net.config | yes |
| `alias` | `hw/alias` | yes |
| `fsms` | `hw/fsms` | yes |
| `media` | `hw/capture` | yes |
| `extensions` | `hw/additional` | yes |
| `logs` | `hw/logs` | no |
| `webapi-logs` | the WebApi's logs, `audit.log` among them | no |

A station can be set up with other folders; `GetRootsAsync` lists the ones it has.

## Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `GetRootsAsync(ct?)` | `Task<List<FileRootDto>>` | The folders the file API reaches. |
| `ListAsync(root, path = "", ct?)` | `Task<FileListingDto>` | What a folder holds, folders first. |
| `DownloadAsync(root, path, ct?)` | `Task<byte[]>` | A file's bytes. A log can be read while it is being written. |
| `UploadAsync(root, path, data, overwrite = false, ct?)` | `Task<FileEntryDto>` | Upload a file (at most 64 MB). An existing file is replaced only with `overwrite: true`. |
| `CreateFolderAsync(root, path, ct?)` | `Task` | Create a folder. |
| `MoveAsync(root, from, to, overwrite = false, ct?)` | `Task` | Rename or move a file or folder within its root. |
| `DeleteAsync(root, path, recursive = false, ct?)` | `Task` | Delete a file, or a folder: a non-empty one only with `recursive: true`. |

## Examples

### Browsing

```csharp
foreach (var r in await client.Files.GetRootsAsync())
    Console.WriteLine($"{r.Id,-12} writable={r.Writable} exists={r.Exists}  {r.Description}");

var listing = await client.Files.ListAsync("alias");
foreach (var e in listing.Entries)
    Console.WriteLine(e.Directory ? $"[{e.Name}]" : $"{e.Name}  {e.Size} bytes  {e.Modified:g}");
```

### Downloading and Uploading

```csharp
// Back up an alias file, then upload a new version over it
byte[] old = await client.Files.DownloadAsync("alias", "station.csv");
File.WriteAllBytes("station.backup.csv", old);

var entry = await client.Files.UploadAsync("alias", "station.csv",
    File.ReadAllBytes("station.csv"), overwrite: true);
Console.WriteLine($"Uploaded {entry.Name}, {entry.Size} bytes");
```

An upload is written beside the old file first, so a failed upload leaves the old file as it was. Uploading a file doesn't load it; to load an alias file now use `Application.LoadConfigFileAsync`, and to load it at every start see [boot.config](system.md#start-up-configuration-bootconfig).

### Folders, Moving and Deleting

```csharp
await client.Files.CreateFolderAsync("fsms", "archive");
await client.Files.MoveAsync("fsms", "old-sequence.json", "archive/old-sequence.json");
await client.Files.DeleteAsync("fsms", "archive", recursive: true);
```

### Reading a Log

```csharp
byte[] log = await client.Files.DownloadAsync("webapi-logs", "audit.log");
string[] lines = System.Text.Encoding.UTF8.GetString(log).Split('\n');
Console.WriteLine(string.Join("\n", lines.Skip(Math.Max(0, lines.Length - 20))));
```

## Errors

All as `AccordionQ2ApiException`:

| Status | When |
|--------|------|
| 400 | A path that climbs out (`..`), names a drive, or holds a character a file name can't |
| 403 | A change in a read-only folder (`logs`, `webapi-logs`), or a path through a symbolic link |
| 404 | An unknown root, or the file or folder doesn't exist |
| 409 | Uploading or moving onto an existing file without `overwrite`, creating a folder that exists, deleting a non-empty folder without `recursive` |
| 423 | Another client holds the [lease](lease.md) (changes only) |

The root folder itself can't be deleted, and a folder can't be moved into itself.

## Models

### `FileRootDto`

| Property | Type | Description |
|----------|------|-------------|
| `Id` | `string` | The root id, e.g. `config` |
| `Description` | `string` | What the folder holds |
| `Writable` | `bool` | Whether changes are allowed |
| `Exists` | `bool` | False when the station doesn't have the folder |

### `FileListingDto`

| Property | Type | Description |
|----------|------|-------------|
| `Root` | `string` | The root listed |
| `Path` | `string` | The folder listed, relative to the root |
| `Writable` | `bool` | Whether changes are allowed |
| `Entries` | `List<FileEntryDto>` | Folders first, then files |

### `FileEntryDto`

| Property | Type | Description |
|----------|------|-------------|
| `Name` | `string` | File or folder name |
| `Directory` | `bool` | True for a folder |
| `Size` | `long?` | Bytes; null for a folder |
| `Modified` | `DateTimeOffset` | Last change |
