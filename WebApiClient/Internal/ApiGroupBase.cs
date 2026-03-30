using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace AccordionQ2.WebApiClient.Internal;

public abstract class ApiGroupBase
{
    protected readonly HttpClient _http;

    private static readonly JsonSerializerSettings _serializerSettings = new JsonSerializerSettings
    {
        Converters = { new StringEnumConverter() },
        NullValueHandling = NullValueHandling.Ignore,
        MissingMemberHandling = MissingMemberHandling.Ignore
    };

    protected ApiGroupBase(HttpClient http) => _http = http;

    protected async Task<T> GetAsync<T>(string path, CancellationToken ct = default)
    {
        var response = await _http.GetAsync(path, ct).ConfigureAwait(false);
        return await ReadAsync<T>(response).ConfigureAwait(false);
    }

    protected async Task<byte[]> GetBytesAsync(string path, CancellationToken ct = default)
    {
        var response = await _http.GetAsync(path, ct).ConfigureAwait(false);
        await EnsureSuccessAsync(response).ConfigureAwait(false);
        return await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
    }

    protected async Task PostAsync(string path, object? body = null, CancellationToken ct = default)
    {
        var response = await _http.PostAsync(path, Serialize(body), ct).ConfigureAwait(false);
        await EnsureSuccessAsync(response).ConfigureAwait(false);
    }

    protected async Task<T> PostAsync<T>(string path, object? body = null, CancellationToken ct = default)
    {
        var response = await _http.PostAsync(path, Serialize(body), ct).ConfigureAwait(false);
        return await ReadAsync<T>(response).ConfigureAwait(false);
    }

    protected async Task PostMultipartAsync(string path, string fileName, byte[] data, CancellationToken ct = default)
    {
        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(data);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(fileContent, "file", fileName);
        var response = await _http.PostAsync(path, form, ct).ConfigureAwait(false);
        await EnsureSuccessAsync(response).ConfigureAwait(false);
    }

    protected async Task DeleteAsync(string path, CancellationToken ct = default)
    {
        var response = await _http.DeleteAsync(path, ct).ConfigureAwait(false);
        await EnsureSuccessAsync(response).ConfigureAwait(false);
    }

    private static StringContent? Serialize(object? body)
    {
        if (body is null) return null;
        var json = JsonConvert.SerializeObject(body, _serializerSettings);
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        await EnsureSuccessAsync(response).ConfigureAwait(false);
        var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        return JsonConvert.DeserializeObject<T>(json, _serializerSettings)!;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;
        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        string? message = null;
        try { message = JsonConvert.DeserializeObject<ErrorBody>(body, _serializerSettings)?.Error; } catch { }
        throw new AccordionQ2ApiException((int)response.StatusCode, message ?? body);
    }

    private sealed class ErrorBody { public string? Error { get; set; } }
}
