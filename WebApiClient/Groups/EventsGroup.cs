using System.Net.Http.Headers;
using System.Text;
using AccordionQ2.WebApiClient.Internal;
using AccordionQ2.WebApiClient.Models;

namespace AccordionQ2.WebApiClient.Groups;

/// <summary>The WebApi's event stream, <c>GET /api/events</c> (contract section 4).</summary>
public sealed class EventsGroup
{
    /// <summary>The contract's limit: no data at all (events or pings) for this long means a dead stream.</summary>
    public static readonly TimeSpan SilenceLimit = TimeSpan.FromSeconds(30);

    private readonly ApiTransport _transport;

    internal EventsGroup(ApiTransport transport) => _transport = transport;

    /// <summary>
    /// Opens the stream and calls <paramref name="onEvent"/> for each event, in order, until
    /// <paramref name="ct"/> is cancelled; then it returns.
    /// </summary>
    /// <remarks>
    /// Throws <see cref="TimeoutException"/> after <see cref="SilenceLimit"/> without data,
    /// <see cref="IOException"/> when the server ends the stream, and <see cref="AccordionQ2ApiException"/>
    /// (404 for a WebApi without the stream; <see cref="AccordionQ2SecurityException"/> for 401/403; otherwise with the
    /// station's own message when it gives one) when it can't be opened. A station in security mode On answers plain
    /// HTTP with 308; the exception's message names the https URL. A station that slows this client down answers 429:
    /// the opening waits out its Retry-After up to <see cref="AccordionQ2ClientOptions.MaxThrottleWait"/>, else throws
    /// <see cref="AccordionQ2ThrottledException"/>, whose <see cref="AccordionQ2ThrottledException.RetryAfter"/> a
    /// reconnect loop should wait before it opens again. Reconnecting is the caller's: after
    /// opening again reload channels, as the contract asks, and also whenever <c>generation</c> changes or
    /// the ids have a gap. Missed events aren't replayed.
    /// </remarks>
    public async Task ListenAsync(Func<ServerEventDto, Task> onEvent, CancellationToken ct = default)
    {
        HttpRequestMessage Request()
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "api/events");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
            return request;
        }
        using var response = await _transport.SendAsync(Request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var status = (int)response.StatusCode;
            var body = await ApiTransport.ReadErrorBodyAsync(response, ct).ConfigureAwait(false);
            throw _transport.CreateError(response, body, $"The event stream couldn't be opened: HTTP {status}");
        }

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
