using System.Net.Http.Headers;
using System.Text;
using AccordionQ2.WebApiClient.Models;

namespace AccordionQ2.WebApiClient.Groups;

/// <summary>The WebApi's event stream, <c>GET /api/events</c> (contract section 4).</summary>
public sealed class EventsGroup
{
    /// <summary>The contract's limit: no data at all (events or pings) for this long means a dead stream.</summary>
    public static readonly TimeSpan SilenceLimit = TimeSpan.FromSeconds(30);

    private readonly HttpClient _http;

    internal EventsGroup(HttpClient http) => _http = http;

    /// <summary>
    /// Opens the stream and calls <paramref name="onEvent"/> for each event, in order, until
    /// <paramref name="ct"/> is cancelled; then it returns.
    /// </summary>
    /// <remarks>
    /// Throws <see cref="TimeoutException"/> after <see cref="SilenceLimit"/> without data,
    /// <see cref="IOException"/> when the server ends the stream, and <see cref="AccordionQ2ApiException"/>
    /// (404 for a WebApi without the stream) when it can't be opened. Reconnecting is the caller's: after
    /// opening again reload channels, as the contract asks, and also whenever <c>generation</c> changes or
    /// the ids have a gap. Missed events aren't replayed.
    /// </remarks>
    public async Task ListenAsync(Func<ServerEventDto, Task> onEvent, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/events");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new AccordionQ2ApiException((int)response.StatusCode, $"The event stream couldn't be opened: HTTP {(int)response.StatusCode}");

        using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        // Closing the stream is the only way to end a read that waits, on .NET Framework too.
        using var close = ct.Register(() => stream.Dispose());

        var current = new ServerEventDto();
        var data = new StringBuilder();
        while (true)
        {
            var read = reader.ReadLineAsync();
            var first = await Task.WhenAny(read, Task.Delay(SilenceLimit, ct)).ConfigureAwait(false);
            if (ct.IsCancellationRequested)
                return;
            if (first != read)
            {
                stream.Dispose();
                throw new TimeoutException($"No data from the event stream for {SilenceLimit.TotalSeconds:0} s");
            }

            string? line;
            try { line = await read.ConfigureAwait(false); }
            catch (Exception) when (ct.IsCancellationRequested) { return; }
            if (line is null)
                throw new IOException("The event stream ended");

            if (line.Length == 0)
            {
                if (data.Length > 0)
                {
                    current.Data = data.ToString();
                    await onEvent(current).ConfigureAwait(false);
                }
                current = new ServerEventDto();
                data.Clear();
                continue;
            }
            if (line[0] == ':')
                continue; // a ping: it only proves the stream is alive

            var colon = line.IndexOf(':');
            var field = colon < 0 ? line : line.Substring(0, colon);
            var value = colon < 0 ? string.Empty : line.Substring(colon + 1);
            if (value.StartsWith(" ", StringComparison.Ordinal))
                value = value.Substring(1);
            switch (field)
            {
                case "id":
                    if (long.TryParse(value, out var id))
                        current.Id = id;
                    break;
                case "event":
                    current.Name = value;
                    break;
                case "data":
                    if (data.Length > 0)
                        data.Append('\n');
                    data.Append(value);
                    break;
            }
        }
    }
}
