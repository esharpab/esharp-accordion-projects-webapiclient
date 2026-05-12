namespace AccordionQ2.WebApiClient.Models;

/// <summary>
/// Describes one Calibration channel.
/// </summary>
public class CalibrationChannelDto
{
    /// <summary>Channel net name.</summary>
    public string NetName { get; set; } = string.Empty;

    /// <summary>Human-readable alias.</summary>
    public string Alias { get; set; } = string.Empty;
}

/// <summary>
/// A single row in a <see cref="CalibrationTableDto"/>.
/// </summary>
public class CalibrationRowDto
{
    /// <summary>Row key (e.g. channel name or composite field identifier).</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Gain correction factor.</summary>
    public double Gain { get; set; }

    /// <summary>Offset correction value.</summary>
    public double Offset { get; set; }
}

/// <summary>
/// JSON-serialisable representation of a CalibrationTable.
/// </summary>
public class CalibrationTableDto
{
    /// <summary>Product identifier.</summary>
    public string ProductId { get; set; } = string.Empty;

    /// <summary>Hardware revision.</summary>
    public string Revision { get; set; } = string.Empty;

    /// <summary>Unit serial number.</summary>
    public string SerialNumber { get; set; } = string.Empty;

    /// <summary>Calibration rows (key, gain, offset).</summary>
    public List<CalibrationRowDto> CalData { get; set; } = new();
}

/// <summary>
/// Request body for writing a <see cref="CalibrationTableDto"/> to a channel.
/// </summary>
public class CalibrationWriteRequest
{
    /// <summary>Net name (or alias) of the Calibration channel to write.</summary>
    /// <example>0.8.ESH10000590.CAL0</example>
    public string ChannelNetName { get; set; } = string.Empty;

    /// <summary>The calibration table to write.</summary>
    public CalibrationTableDto? Table { get; set; }
}
