# Modules

Manage hardware and software modules, query the hardware topology, and view application licenses.

## Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `GetAllAsync(ct?)` | `Task<List<ModuleSettingsDto>>` | All modules (loaded and unloaded). |
| `GetLoadedAsync(ct?)` | `Task<List<ModuleSettingsDto>>` | Currently loaded modules only. |
| `LoadAsync(module, ct?)` | `Task` | Load a module. |
| `UnloadAsync(module, ct?)` | `Task` | Unload a module. |
| `ConfigureAsync(module, ct?)` | `Task` | Update module configuration. |
| `GetPhysicalSystemAsync(ct?)` | `Task<PhysicalSystemDto>` | Hardware topology (host, MAC, firmware, modules). |
| `GetLicensedAppsAsync(ct?)` | `Task<List<AppLicenseDto>>` | Licensed applications only. |
| `GetAllAppsAsync(ct?)` | `Task<List<AppLicenseDto>>` | All applications (licensed and unlicensed). |

## Examples

### Hardware Topology

```csharp
var system = await client.Modules.GetPhysicalSystemAsync();
Console.WriteLine($"Host: {system.Host}, MAC: {system.MAC}, Firmware: {system.Firmware}");

foreach (var mod in system.Modules)
    Console.WriteLine($"  Slot {mod.Index}: {mod.Name} ({mod.ProductID}) rev={mod.Revision}");

foreach (var (iface, addr) in system.NetworkInterfaces)
    Console.WriteLine($"  {iface}: {addr}");
```

### Listing Loaded Modules

```csharp
var loaded = await client.Modules.GetLoadedAsync();
foreach (var m in loaded)
    Console.WriteLine($"  {m.Name} (enabled={m.Enabled}, class={m.ClassName})");
```

### Loading and Unloading a Module

```csharp
// Get the module descriptor first
var all = await client.Modules.GetAllAsync();
var target = all.First(m => m.Name == "MyHardwareModule");

await client.Modules.LoadAsync(target);
// ... use the module ...
await client.Modules.UnloadAsync(target);
```

### License Information

```csharp
foreach (var app in await client.Modules.GetLicensedAppsAsync())
    Console.WriteLine($"  {app.Name}: key={app.Key}, expires={app.Expires:d}, type={app.Type}");
```

## Models

### `ModuleSettingsDto`

| Property | Type | Description |
|----------|------|-------------|
| `Name` | `string` | Module name |
| `Enabled` | `bool` | Whether the module is enabled |
| `ClassName` | `string` | Module class name |
| `AssemblyPath` | `string` | Path to the module assembly |
| `Namespace` | `string` | Module namespace |
| `ImageName` | `string` | Docker/firmware image name |
| `InitialData` | `Dictionary<string, object?>` | Arbitrary key/value initialisation data |

### `PhysicalSystemDto`

| Property | Type | Description |
|----------|------|-------------|
| `Host` | `string` | Host name |
| `EthIpV4` | `string` | Primary IPv4 address |
| `EthIpV6` | `string` | Primary IPv6 address |
| `Firmware` | `string` | Firmware version string |
| `MAC` | `string` | MAC address |
| `Modules` | `List<PhysicalModuleDto>` | List of physical hardware module slots |
| `NetworkInterfaces` | `Dictionary<string, string>` | All network interface names mapped to addresses |

### `PhysicalModuleDto`

| Property | Type | Description |
|----------|------|-------------|
| `Index` | `int` | Slot index |
| `Name` | `string` | Module name |
| `ProductID` | `string` | Product identifier |
| `Revision` | `byte` | Hardware revision |
| `SerialNumber` | `string` | Serial number |

### `AppLicenseDto`

| Property | Type | Description |
|----------|------|-------------|
| `Name` | `string` | Application name |
| `Key` | `string` | License key |
| `Expires` | `DateTime` | License expiration date |
| `Type` | `AppTypes` | Application type (`SoftwareModule` or `HardwareModule`) |
