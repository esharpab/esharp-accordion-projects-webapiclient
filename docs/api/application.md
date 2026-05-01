# Application

Application lifecycle management and configuration file operations.

## Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `GetNameAsync(ct?)` | `Task<string>` | Application module name. |
| `GetIdentificationAsync(ct?)` | `Task<string>` | Application identification string. |
| `GetStatusAsync(ct?)` | `Task<ModuleStatus>` | Current status enum. |
| `ResetAsync(ct?)` | `Task` | Reset the application engine. |
| `ListConfigFilesAsync(ct?)` | `Task<string[]>` | Available configuration files on the device. |
| `GetLoadedConfigFilesAsync(ct?)` | `Task<List<string>>` | Currently loaded configuration files. |
| `LoadConfigFileAsync(fileName, ct?)` | `Task` | Load a configuration file by name. |
| `SaveConfigFileAsync(fileName, ct?)` | `Task` | Save current configuration to a named file. |
| `DownloadConfigFileAsync(fileName, ct?)` | `Task<byte[]>` | Download a configuration file as raw bytes. |
| `UploadConfigFileAsync(fileName, data, ct?)` | `Task` | Upload a configuration file to the device. |
| `DeleteConfigFileAsync(fileName, ct?)` | `Task` | Delete a configuration file from the device. |

## Examples

### Checking Application Status

```csharp
using AccordionQ2.WebApiClient.Models;

var name   = await client.Application.GetNameAsync();
var ident  = await client.Application.GetIdentificationAsync();
var status = await client.Application.GetStatusAsync();

Console.WriteLine($"{name} ({ident}): {status}");

if (status == ModuleStatus.OK)
    Console.WriteLine("Application is running normally");
else if (status == ModuleStatus.Error)
    Console.WriteLine("Application has an error");
```

### Resetting the Application

```csharp
await client.Application.ResetAsync();
```

### Listing Configuration Files

```csharp
Console.WriteLine("Available configs:");
foreach (var f in await client.Application.ListConfigFilesAsync())
    Console.WriteLine($"  {f}");

Console.WriteLine("Currently loaded:");
foreach (var f in await client.Application.GetLoadedConfigFilesAsync())
    Console.WriteLine($"  {f}");
```

### Configuration File Round-Trip (Backup & Restore)

```csharp
// Download current config and back it up
byte[] data = await client.Application.DownloadConfigFileAsync("factory.cfg");
await client.Application.UploadConfigFileAsync("factory_backup.cfg", data);

// Later, restore from backup
byte[] backup = await client.Application.DownloadConfigFileAsync("factory_backup.cfg");
await client.Application.UploadConfigFileAsync("factory.cfg", backup);
await client.Application.LoadConfigFileAsync("factory.cfg");
```

### Saving Configuration from the Running State

```csharp
// Persist the current in-memory state to a named file
await client.Application.SaveConfigFileAsync("current_state.cfg");
```

### Deleting a Configuration File

```csharp
await client.Application.DeleteConfigFileAsync("old_config.cfg");
```
