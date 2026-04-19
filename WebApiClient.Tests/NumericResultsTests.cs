using AccordionQ2.WebApiClient.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AccordionQ2.WebApiClient.Tests;

[TestClass]
[TestCategory("Integration")]
public class NumericResultsTests
{
    private static NumericResultChannelDto? _channel;
    private static string? _target;

    [ClassInitialize]
    public static async Task ClassInit(TestContext _)
    {
        var channels = await TestSetup.Client.NumericResults.GetChannelsAsync();
        _channel = channels?.FirstOrDefault(c => c.PossibleTargetNames.Length > 0);
        _target = _channel?.PossibleTargetNames.FirstOrDefault();

        Console.WriteLine($"NumericResult channel for tests: {_channel?.Alias ?? "(none found)"}");
        if (_target != null)
            Console.WriteLine($"Target: {_target}");
    }

    private static void RequireChannel()
    {
        if (_channel == null || _target == null)
            Assert.Inconclusive("No NumericResult channels with targets found on this device — skipping test");
    }

    // -------------------------------------------------------------------------
    // Discovery
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task GetChannels_ReturnsValidList()
    {
        var channels = await TestSetup.Client.NumericResults.GetChannelsAsync();

        Assert.IsNotNull(channels);
        Console.WriteLine($"NumericResult channels found: {channels.Count}");
        foreach (var ch in channels)
            Console.WriteLine($"  {ch.NetName} ({ch.Alias}) — {ch.PossibleTargetNames.Length} targets, {ch.SampleRate}Hz");
    }

    [TestMethod]
    public async Task GetTargets_ForFirstChannel_ReturnsArray()
    {
        RequireChannel();

        var targets = await TestSetup.Client.NumericResults.GetTargetsAsync(_channel!.NetName);

        Assert.IsNotNull(targets);
        Console.WriteLine($"Targets for {_channel.Alias}:");
        foreach (var t in targets)
            Console.WriteLine($"  {t}");
        Assert.IsTrue(targets.Length > 0, "Expected at least one target");
    }

    [TestMethod]
    public async Task GetTargets_InvalidChannel_ThrowsApiException()
    {
        await Assert.ThrowsExactlyAsync<AccordionQ2ApiException>(
            () => TestSetup.Client.NumericResults.GetTargetsAsync("NonExistent.Channel.12345"));
    }

    // -------------------------------------------------------------------------
    // Measure — ReducedSet=true (default)
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task Measure_ReducedSet_ReturnsMeta()
    {
        RequireChannel();

        var result = await TestSetup.Client.NumericResults.MeasureAsync(new NumericMeasureRequest
        {
            ChannelNetName = _channel!.NetName,
            TargetNetName  = _target!,
            Samples        = 100,
            ReducedSet     = true
        });

        Assert.IsNotNull(result);
        Assert.AreEqual(_channel.NetName, result.ChannelNetName);
        Assert.AreEqual(_target, result.TargetNetName);
        Assert.IsTrue(result.ReducedSet);
        Assert.AreEqual(0, result.SampleCount,
            "SampleCount must be 0 when ReducedSet=true (firmware discards samples)");
        Assert.IsTrue(result.SampleRate > 0, "SampleRate should be positive");

        Console.WriteLine($"Acquired: ReducedSet=true, SampleRate={result.SampleRate}Hz, Duration={result.Duration}");
    }

    // -------------------------------------------------------------------------
    // Value endpoints after ReducedSet measure
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task GetMean_AfterMeasure_ReturnsFiniteValue()
    {
        RequireChannel();
        await MeasureAsync(reducedSet: true);

        var mean = await TestSetup.Client.NumericResults.GetMeanAsync(_channel!.NetName);

        Console.WriteLine($"Mean: {mean}");
        Assert.IsFalse(double.IsNaN(mean),      "Mean must not be NaN");
        Assert.IsFalse(double.IsInfinity(mean), "Mean must not be Infinity");
    }

    [TestMethod]
    public async Task GetMin_LessThanOrEqualToMax()
    {
        RequireChannel();
        await MeasureAsync(reducedSet: true);

        var min = await TestSetup.Client.NumericResults.GetMinAsync(_channel!.NetName);
        var max = await TestSetup.Client.NumericResults.GetMaxAsync(_channel!.NetName);

        Console.WriteLine($"Min={min}, Max={max}");
        Assert.IsTrue(min <= max, $"Min ({min}) must be <= Max ({max})");
    }

    [TestMethod]
    public async Task GetStdDev_AfterMeasure_ReturnsNonNegative()
    {
        RequireChannel();
        await MeasureAsync(reducedSet: true);

        var stdev = await TestSetup.Client.NumericResults.GetStdDevAsync(_channel!.NetName);

        Console.WriteLine($"StdDev: {stdev}");
        Assert.IsTrue(stdev >= 0, $"Standard deviation ({stdev}) must be >= 0");
    }

    // -------------------------------------------------------------------------
    // Samples endpoint
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task GetSamples_WithReducedSet_Throws400()
    {
        RequireChannel();
        await MeasureAsync(reducedSet: true);

        var ex = await Assert.ThrowsExactlyAsync<AccordionQ2ApiException>(
            () => TestSetup.Client.NumericResults.GetSamplesAsync(_channel!.NetName));

        Assert.AreEqual(400, ex.StatusCode,
            "Expected HTTP 400 when requesting samples from a ReducedSet measurement");
        Console.WriteLine($"Got expected 400: {ex.Message}");
    }

    [TestMethod]
    public async Task GetSamples_WithFullSet_ReturnsSampleArray()
    {
        RequireChannel();
        await MeasureAsync(samples: 100, reducedSet: false);

        var samples = await TestSetup.Client.NumericResults.GetSamplesAsync(_channel!.NetName);

        Console.WriteLine($"Samples returned: {samples.Length}");
        Assert.IsNotNull(samples);
        Assert.IsTrue(samples.Length > 0, "Expected a non-empty sample array");
    }

    // -------------------------------------------------------------------------
    // No-result guard
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task GetMean_WithoutPriorMeasure_Throws404()
    {
        // Use a channel name that has never been measured — the server cache will have no entry for it
        var ex = await Assert.ThrowsExactlyAsync<AccordionQ2ApiException>(
            () => TestSetup.Client.NumericResults.GetMeanAsync("No.Such.NumericResult.Channel.99999"));

        Assert.AreEqual(404, ex.StatusCode);
        Console.WriteLine($"Got expected 404: {ex.Message}");
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static Task<NumericMeasureResultDto> MeasureAsync(int samples = 100, bool reducedSet = true)
        => TestSetup.Client.NumericResults.MeasureAsync(new NumericMeasureRequest
        {
            ChannelNetName = _channel!.NetName,
            TargetNetName  = _target!,
            Samples        = samples,
            ReducedSet     = reducedSet
        });
}
