using AccordionQ2.WebApiClient.Internal;
using AccordionQ2.WebApiClient.Models;

namespace AccordionQ2.WebApiClient.Groups;

/// <summary>
/// Application lifecycle operations including module status, reset,
/// and configuration file management.
/// </summary>
public sealed class ApplicationGroup : ApiGroupBase
{
    internal ApplicationGroup(HttpClient http) : base(http) { }

    /// <summary>Returns the application module name.</summary>
    public Task<string> GetNameAsync(CancellationToken ct = default)
        => GetAsync<string>("api/application/name", ct);

    /// <summary>Returns the application identification string.</summary>
    public Task<string> GetIdentificationAsync(CancellationToken ct = default)
        => GetAsync<string>("api/application/identification", ct);

    /// <summary>Returns the current application module status.</summary>
    public Task<ModuleStatus> GetStatusAsync(CancellationToken ct = default)
        => GetAsync<ModuleStatus>("api/application/status", ct);

    /// <summary>Sends a reset command to the application engine.</summary>
    public Task ResetAsync(CancellationToken ct = default)
        => PostAsync("api/application/reset", ct: ct);

    // --- Configuration file operations ---

    /// <summary>Lists all configuration files available on the device.</summary>
    public Task<string[]> ListConfigFilesAsync(CancellationToken ct = default)
        => GetAsync<string[]>("api/application/config/list", ct);

    /// <summary>Returns the names of currently loaded configuration files.</summary>
    public Task<List<string>> GetLoadedConfigFilesAsync(CancellationToken ct = default)
        => GetAsync<List<string>>("api/application/config/loaded", ct);

    /// <summary>Loads a configuration file by name.</summary>
    public Task LoadConfigFileAsync(string fileName, CancellationToken ct = default)
        => PostAsync("api/application/config/load", new { FileName = fileName }, ct);

    /// <summary>Saves the current configuration to a named file on the device.</summary>
    public Task SaveConfigFileAsync(string fileName, CancellationToken ct = default)
        => PostAsync("api/application/config/save", new { FileName = fileName }, ct);

    /// <summary>Downloads a configuration file as raw bytes.</summary>
    public Task<byte[]> DownloadConfigFileAsync(string fileName, CancellationToken ct = default)
        => GetBytesAsync($"api/application/config/download/{Uri.EscapeDataString(fileName)}", ct);

    /// <summary>Uploads a configuration file to the device.</summary>
    public Task UploadConfigFileAsync(string fileName, byte[] data, CancellationToken ct = default)
        => PostMultipartAsync("api/application/config/upload", fileName, data, ct);

    /// <summary>Deletes a configuration file from the device.</summary>
    public Task DeleteConfigFileAsync(string fileName, CancellationToken ct = default)
        => DeleteAsync($"api/application/config/{Uri.EscapeDataString(fileName)}", ct);

    // --- System log ---

    /// <summary>
    /// Returns the last <paramref name="tail"/> lines of the system log (hw.log).
    /// Pass 0 to retrieve the entire log.
    /// </summary>
    public Task<string[]> GetSystemLogAsync(int tail = 100, CancellationToken ct = default)
        => GetAsync<string[]>($"api/application/log?tail={tail}", ct);
}
