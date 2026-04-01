namespace AccordionQ2.WebApiClient.Tests;

/// <summary>
/// Shared configuration for integration tests.
/// Override the base URL by setting the ACCORDIONQ2_API_URL environment variable.
/// </summary>
internal static class TestConfig
{
    public const string DefaultBaseUrl = "http://agent64.local:5000";

    public static string BaseUrl =>
        Environment.GetEnvironmentVariable("ACCORDIONQ2_API_URL")
        ?? DefaultBaseUrl;

    // --- Well-known resource names on agent64 hardware ---
    public const string CpuTempResource   = "TempRegulator.CPU_TEMP";
    public const string Mon3V3Resource    = "0.1.ESH10000158.MON_3V3";
    public const string Mon1V8Resource    = "0.1.ESH10000158.MON_1V8";
    public const string ExtVolt5VResource = "0.7.ESH10000183.5V_EXT_VOLT";
    public const string ExtCurr5VResource = "0.7.ESH10000183.5V_EXT_CURR";
    public const string UptimeResource    = "Engine.Uptime";
    public const string FirmwareResource  = "Engine.FirmwareRev";

    // --- Well-known channel aliases ---
    public const string AnalogChannelAlias = "0.1.ESH10000158.MON_3V3";
    public const string AdcChannelAlias    = "0.8.ESH10000590.ADC01";
    public const string I2cChannelAlias    = "0.ESH10000023.I2C09";

    // --- LED channel aliases (ESH10000355) ---
    public static readonly string[] LedChannels =
    [
        "0.11.ESH10000355.A1",
        "0.11.ESH10000355.B1",
        "0.11.ESH10000355.C1",
        "0.11.ESH10000355.D1",
        "0.11.ESH10000355.E1",
        "0.11.ESH10000355.F1",
    ];

    // --- Physical system expectations ---
    public const string BaseModuleProductId   = "ESH10000158";
    public const string BaseModuleName        = "AGENT Q2 Base";
}
