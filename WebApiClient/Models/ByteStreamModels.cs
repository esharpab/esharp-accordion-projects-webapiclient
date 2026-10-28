namespace AccordionQ2.WebApiClient.Models;

/// <summary>
/// The status of a ByteStream file channel (GET /api/channels/bytestream, firmware 7.0.5): for MicroHAPS's
/// <c>JTAG.FPGA_IMAGE</c>, the FPGA image load.
/// </summary>
public sealed class ByteStreamStatusDto
{
    /// <summary><c>idle</c>, <c>validating</c>, <c>loading</c>, <c>waitingDone</c>, <c>done</c> or <c>failed</c>.</summary>
    public string State { get; set; } = "idle";
    public string? Target { get; set; }
    /// <summary>0 to 100 while loading (openFPGALoader's own percentage).</summary>
    public double Progress { get; set; }
    /// <summary>The progress bar's label, e.g. <c>Load SRAM</c>.</summary>
    public string? Phase { get; set; }
    /// <summary>What happened, or why it failed.</summary>
    public string? Message { get; set; }
    /// <summary>What the file is called (<c>FPGA image</c>).</summary>
    public string? Label { get; set; }
    /// <summary>The extensions the channel takes, e.g. <c>.bit</c>.</summary>
    public string? Accept { get; set; }
    /// <summary>The targets this firmware supports (<c>sram</c>).</summary>
    public List<string> Targets { get; set; } = ["sram"];
    /// <summary>The largest file the channel takes.</summary>
    public long MaxBytes { get; set; }
    /// <summary>The last image the channel took; null before the first.</summary>
    public ByteStreamImageDto? Image { get; set; }

    /// <summary>A load is running.</summary>
    public bool IsRunning => State is "validating" or "loading" or "waitingDone";

    /// <summary>The load has ended, well or not.</summary>
    public bool IsEnded => State is "done" or "failed";
}

/// <summary>The last image a file channel took: only the last one is kept.</summary>
public sealed class ByteStreamImageDto
{
    public string? FileName { get; set; }
    public long Size { get; set; }
    /// <summary>The SHA-256 of what was loaded, lowercase hex.</summary>
    public string? Sha256 { get; set; }
    /// <summary><c>bit</c> or <c>bin</c>.</summary>
    public string? Format { get; set; }
    /// <summary>The .bit header's design field (name, UserID, Vivado version).</summary>
    public string? Design { get; set; }
    /// <summary>The .bit header's part, e.g. <c>xcku5p-ffvb676-1-e</c>.</summary>
    public string? Part { get; set; }
    /// <summary>The .bit header's date and time.</summary>
    public string? Built { get; set; }
    public DateTimeOffset? Started { get; set; }
    public DateTimeOffset? Finished { get; set; }
    public long? DurationMs { get; set; }
    /// <summary><c>ok</c>, or the error; null while the load runs.</summary>
    public string? Result { get; set; }
    /// <summary>The FPGA runs this image now; false once a POWER cycle or a PROGRAM_B pulse has replaced it.</summary>
    public bool Loaded { get; set; }
    /// <summary>Why it is no longer loaded.</summary>
    public string? Replaced { get; set; }
    /// <summary>The DesignID device's Info.Keys from the first HCI.Scan after the load.</summary>
    public string? DesignKeys { get; set; }
}
