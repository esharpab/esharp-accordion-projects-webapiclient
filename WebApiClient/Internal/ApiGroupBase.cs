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

    /// <summary>A GET whose answer is text (Markdown, HTML), as the station sent it.</summary>
    private protected async Task<string> GetTextAsync(string path, CancellationToken ct = default)
    {
        using var response = await SendAsync(HttpMethod.Get, path, null, ct).ConfigureAwait(false);
        await EnsureSuccessAsync(response).ConfigureAwait(false);
        return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
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

    protected async Task PutAsync(string path, object? body = null, CancellationToken ct = default)
    {
        using var response = await SendAsync(HttpMethod.Put, path, Serialize(body), ct).ConfigureAwait(false);
        await EnsureSuccessAsync(response).ConfigureAwait(false);
    }

    protected async Task<T> PutBytesAsync<T>(string path, byte[] data, CancellationToken ct = default)
    {
        using var response = await SendAsync(HttpMethod.Put, path, () => Bytes(data), ct).ConfigureAwait(false);
        return await ReadAsync<T>(response).ConfigureAwait(false);
    }

    protected async Task<T> PostBytesAsync<T>(string path, byte[] data, CancellationToken ct = default)
    {
        using var response = await SendAsync(HttpMethod.Post, path, () => Bytes(data), ct).ConfigureAwait(false);
        return await ReadAsync<T>(response).ConfigureAwait(false);
    }

    protected async Task<T> PostStreamAsync<T>(string path, Stream data, CancellationToken ct = default)
    {
        HttpContent Content()
        {
            // Wrapped: disposing the request disposes its content, and StreamContent disposes the stream it reads
            var content = new StreamContent(new NonClosingStream(data));
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            return content;
        }
        // Not resent after a 429: the stream may have been read already
        using var response = await _transport.SendAsync(() => new HttpRequestMessage(HttpMethod.Post, path) { Content = Content() }, ct,
            resendable: false).ConfigureAwait(false);
        return await ReadAsync<T>(response).ConfigureAwait(false);
    }

    protected async Task PostMultipartAsync(string path, string fileName, byte[] data, CancellationToken ct = default)
    {
        HttpContent Form()
        {
            var form = new MultipartFormDataContent();
            form.Add(Bytes(data), "file", fileName);
            return form;
        }
        using var response = await SendAsync(HttpMethod.Post, path, Form, ct).ConfigureAwait(false);
        await EnsureSuccessAsync(response).ConfigureAwait(false);
    }

    protected async Task DeleteAsync(string path, CancellationToken ct = default)
    {
        using var response = await SendAsync(HttpMethod.Delete, path, null, ct).ConfigureAwait(false);
        await EnsureSuccessAsync(response).ConfigureAwait(false);
    }

    /// <summary>
    /// A JSON request with a chosen credential (<see cref="Credentials"/>): the logins (FW7 SEC-03). The answer is
    /// read as <typeparamref name="T"/>, or nothing for <c>object</c> with 204; <paramref name="passwordCheck"/> makes
    /// the endpoint's own 401 a <see cref="AccordionQ2SecurityReason.WrongPassword"/>.
    /// </summary>
    private protected async Task<T> SendJsonAsync<T>(HttpMethod method, string path, object? body, Credentials credentials, bool passwordCheck,
        CancellationToken ct)
    {
        var json = body is null ? null : JsonConvert.SerializeObject(body, _serializerSettings);
        using var response = await _transport.SendAsync(
            () => new HttpRequestMessage(method, path) { Content = json is null ? null : new StringContent(json, Encoding.UTF8, "application/json") },
            ct, credentials: credentials).ConfigureAwait(false);
        await EnsureSuccessAsync(response, passwordCheck).ConfigureAwait(false);
        var text = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        return text.Length == 0 ? default! : JsonConvert.DeserializeObject<T>(text, _serializerSettings)!;
    }

    /// <summary>A GET whose 404 means "none", not an error: null then.</summary>
    private protected async Task<T?> GetOrNullAsync<T>(string path, CancellationToken ct) where T : class
    {
        using var response = await SendAsync(HttpMethod.Get, path, null, ct).ConfigureAwait(false);
        if ((int)response.StatusCode == 404)
            return null;
        return await ReadAsync<T>(response).ConfigureAwait(false);
    }

    // The content is made for each attempt: a request (and its content) is sent only once
    private Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, Func<HttpContent>? content, CancellationToken ct) =>
        _transport.SendAsync(() => new HttpRequestMessage(method, path) { Content = content?.Invoke() }, ct);

    private static ByteArrayContent Bytes(byte[] data)
    {
        var content = new ByteArrayContent(data);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        return content;
    }

    private static Func<HttpContent>? Serialize(object? body)
    {
        if (body is null) return null;
        var json = JsonConvert.SerializeObject(body, _serializerSettings);
        return () => new StringContent(json, Encoding.UTF8, "application/json");
    }

    private async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        await EnsureSuccessAsync(response).ConfigureAwait(false);
        var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        return JsonConvert.DeserializeObject<T>(json, _serializerSettings)!;
    }

    private async Task EnsureSuccessAsync(HttpResponseMessage response, bool passwordCheck = false)
    {
        if (response.IsSuccessStatusCode) return;
        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        throw _transport.CreateError(response, body, passwordCheck: passwordCheck);
    }
}
