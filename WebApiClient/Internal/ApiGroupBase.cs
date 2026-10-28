using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace AccordionQ2.WebApiClient.Internal;

public abstract class ApiGroupBase
{
    private protected readonly ApiTransport _transport;

    private static readonly JsonSerializerSettings _serializerSettings = new JsonSerializerSettings
    {
        Converters = { new StringEnumConverter() },
        NullValueHandling = NullValueHandling.Ignore,
        MissingMemberHandling = MissingMemberHandling.Ignore
    };

    private protected ApiGroupBase(ApiTransport transport) => _transport = transport;

    protected async Task<T> GetAsync<T>(string path, CancellationToken ct = default)
    {
        using var response = await SendAsync(HttpMethod.Get, path, null, ct).ConfigureAwait(false);
        return await ReadAsync<T>(response).ConfigureAwait(false);
    }

    protected async Task<byte[]> GetBytesAsync(string path, CancellationToken ct = default)
    {
        using var response = await SendAsync(HttpMethod.Get, path, null, ct).ConfigureAwait(false);
        await EnsureSuccessAsync(response).ConfigureAwait(false);
        return await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
    }

    protected async Task PostAsync(string path, object? body = null, CancellationToken ct = default)
    {
        using var response = await SendAsync(HttpMethod.Post, path, Serialize(body), ct).ConfigureAwait(false);
        await EnsureSuccessAsync(response).ConfigureAwait(false);
    }

    protected async Task<T> PostAsync<T>(string path, object? body = null, CancellationToken ct = default)
    {
        using var response = await SendAsync(HttpMethod.Post, path, Serialize(body), ct).ConfigureAwait(false);
        return await ReadAsync<T>(response).ConfigureAwait(false);
    }

    protected async Task<T> PutAsync<T>(string path, object? body = null, CancellationToken ct = default)
    {
        using var response = await SendAsync(HttpMethod.Put, path, Serialize(body), ct).ConfigureAwait(false);
        return await ReadAsync<T>(response).ConfigureAwait(false);
    }

    protected async Task<T> PutBytesAsync<T>(string path, byte[] data, CancellationToken ct = default)
    {
        using var response = await SendAsync(HttpMethod.Put, path, Bytes(data), ct).ConfigureAwait(false);
        return await ReadAsync<T>(response).ConfigureAwait(false);
    }

    protected async Task<T> PostBytesAsync<T>(string path, byte[] data, CancellationToken ct = default)
    {
        using var response = await SendAsync(HttpMethod.Post, path, Bytes(data), ct).ConfigureAwait(false);
        return await ReadAsync<T>(response).ConfigureAwait(false);
    }

    protected async Task<T> PostStreamAsync<T>(string path, Stream data, CancellationToken ct = default)
    {
        // Wrapped: disposing the request disposes its content, and StreamContent disposes the stream it reads
        var content = new StreamContent(new NonClosingStream(data));
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        using var response = await SendAsync(HttpMethod.Post, path, content, ct).ConfigureAwait(false);
        return await ReadAsync<T>(response).ConfigureAwait(false);
    }

    protected async Task PostMultipartAsync(string path, string fileName, byte[] data, CancellationToken ct = default)
    {
        var form = new MultipartFormDataContent();
        form.Add(Bytes(data), "file", fileName);
        using var response = await SendAsync(HttpMethod.Post, path, form, ct).ConfigureAwait(false);
        await EnsureSuccessAsync(response).ConfigureAwait(false);
    }

    protected async Task DeleteAsync(string path, CancellationToken ct = default)
    {
        using var response = await SendAsync(HttpMethod.Delete, path, null, ct).ConfigureAwait(false);
        await EnsureSuccessAsync(response).ConfigureAwait(false);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, HttpContent? content, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, path) { Content = content };
        return await _transport.SendAsync(request, ct).ConfigureAwait(false);
    }

    private static ByteArrayContent Bytes(byte[] data)
    {
        var content = new ByteArrayContent(data);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        return content;
    }

    private static StringContent? Serialize(object? body)
    {
        if (body is null) return null;
        var json = JsonConvert.SerializeObject(body, _serializerSettings);
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    private async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        await EnsureSuccessAsync(response).ConfigureAwait(false);
        var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        return JsonConvert.DeserializeObject<T>(json, _serializerSettings)!;
    }

    private async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;
        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        throw _transport.CreateError((int)response.StatusCode, body);
    }
}
