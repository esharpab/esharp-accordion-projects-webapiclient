using AccordionQ2.WebApiClient.Internal;
using AccordionQ2.WebApiClient.Models;

namespace AccordionQ2.WebApiClient.Groups;

/// <summary>
/// Operations for reading and writing Calibration channels.
/// </summary>
/// <remarks>
/// Calibration channels carry a <c>CalibrationTable</c> encoded as a Base64 binary payload.
/// The server transparently decodes and encodes that payload, so callers work with plain JSON.
///
/// Typical workflow:
/// <code>
/// // 1. Discover which Calibration channels exist
/// var channels = await client.Calibration.GetChannelsAsync();
///
/// // 2. Read the current table from a channel (by NetName or Alias)
/// var table = await client.Calibration.GetTableAsync(channels[0].NetName);
/// foreach (var row in table.CalData)
///     Console.WriteLine($"{row.Key}: gain={row.Gain}, offset={row.Offset}");
///
/// // 3. Modify and write back
/// table.CalData[0] = new CalibrationRowDto { Key = table.CalData[0].Key, Gain = 1.01, Offset = 0.002 };
/// await client.Calibration.SetTableAsync(channels[0].NetName, table);
/// </code>
/// </remarks>
public sealed class CalibrationGroup : ApiGroupBase
{
    internal CalibrationGroup(HttpClient http) : base(http) { }

    /// <summary>Returns all Calibration channels.</summary>
    public Task<List<CalibrationChannelDto>> GetChannelsAsync(CancellationToken ct = default)
        => GetAsync<List<CalibrationChannelDto>>("api/calibration/channels", ct);

    /// <summary>
    /// Reads and decodes the CalibrationTable from a Calibration channel.
    /// </summary>
    /// <param name="channelName">Net name or Alias of the Calibration channel.</param>
    /// <param name="ct">Cancellation token.</param>
    public Task<CalibrationTableDto> GetTableAsync(string channelName, CancellationToken ct = default)
        => GetAsync<CalibrationTableDto>(
            $"api/calibration/table?channel={Uri.EscapeDataString(channelName)}", ct);

    /// <summary>
    /// Encodes and writes a CalibrationTable to a Calibration channel.
    /// </summary>
    /// <param name="channelName">Net name or Alias of the Calibration channel.</param>
    /// <param name="table">The calibration table to write.</param>
    /// <param name="ct">Cancellation token.</param>
    public Task SetTableAsync(string channelName, CalibrationTableDto table, CancellationToken ct = default)
        => PostAsync("api/calibration/table", new CalibrationWriteRequest
        {
            ChannelNetName = channelName,
            Table = table
        }, ct);
}
