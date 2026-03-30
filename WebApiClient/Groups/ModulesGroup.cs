using AccordionQ2.WebApiClient.Internal;
using AccordionQ2.WebApiClient.Models;

namespace AccordionQ2.WebApiClient.Groups;

/// <summary>Operations for managing hardware and software modules.</summary>
public sealed class ModulesGroup : ApiGroupBase
{
    internal ModulesGroup(HttpClient http) : base(http) { }

    /// <summary>Returns all available modules (loaded and unloaded).</summary>
    public Task<List<ModuleSettingsDto>> GetAllAsync(CancellationToken ct = default)
        => GetAsync<List<ModuleSettingsDto>>("api/modules", ct);

    /// <summary>Returns currently loaded modules only.</summary>
    public Task<List<ModuleSettingsDto>> GetLoadedAsync(CancellationToken ct = default)
        => GetAsync<List<ModuleSettingsDto>>("api/modules/loaded", ct);

    /// <summary>Loads a module.</summary>
    public Task LoadAsync(ModuleSettingsDto module, CancellationToken ct = default)
        => PostAsync("api/modules/load", module, ct);

    /// <summary>Unloads a module.</summary>
    public Task UnloadAsync(ModuleSettingsDto module, CancellationToken ct = default)
        => PostAsync("api/modules/unload", module, ct);

    /// <summary>Configures a module.</summary>
    public Task ConfigureAsync(ModuleSettingsDto module, CancellationToken ct = default)
        => PostAsync("api/modules/configure", module, ct);

    /// <summary>Returns the physical system description (hardware topology).</summary>
    public Task<PhysicalSystemDto> GetPhysicalSystemAsync(CancellationToken ct = default)
        => GetAsync<PhysicalSystemDto>("api/modules/physical-system", ct);

    /// <summary>Returns licensed applications only.</summary>
    public Task<List<AppLicenseDto>> GetLicensedAppsAsync(CancellationToken ct = default)
        => GetAsync<List<AppLicenseDto>>("api/modules/apps/licensed", ct);

    /// <summary>Returns all applications (licensed and unlicensed).</summary>
    public Task<List<AppLicenseDto>> GetAllAppsAsync(CancellationToken ct = default)
        => GetAsync<List<AppLicenseDto>>("api/modules/apps", ct);
}
