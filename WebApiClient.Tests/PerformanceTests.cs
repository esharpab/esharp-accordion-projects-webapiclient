using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AccordionQ2.WebApiClient.Tests;

[TestClass]
[TestCategory("Performance")]
public class PerformanceTests
{
    private static readonly string[] RainbowColors =
    [
        "Red", "Orange", "Yellow", "Green", "Blue", "Indigo", "Violet",
        "Cyan", "Magenta", "LightBlue", "LimeGreen", "Gold", "HotPink",
    ];

    [TestMethod]
    public async Task GetUptime_1000Roundtrips_MeasuresThroughput()
    {
        const int iterations = 1000;
        var client = TestSetup.Client;

        // Warm-up
        await client.Resources.GetValueAsync(TestConfig.UptimeResource);

        var sw = Stopwatch.StartNew();

        for (int i = 0; i < iterations; i++)
        {
            var value = await client.Resources.GetValueAsync(TestConfig.UptimeResource);
            Assert.IsNotNull(value);
        }

        sw.Stop();

        double totalMs = sw.Elapsed.TotalMilliseconds;
        double avgMs = totalMs / iterations;

        Console.WriteLine($"Roundtrip test: {iterations} iterations");
        Console.WriteLine($"  Total time : {totalMs:F1} ms");
        Console.WriteLine($"  Average    : {avgMs:F2} ms/request");
        Console.WriteLine($"  Throughput : {iterations / sw.Elapsed.TotalSeconds:F1} req/s");
    }

    [TestMethod]
    public async Task RainbowLeds_CycleColors_MeasuresThroughput()
    {
        const int cycles = 50;
        var client = TestSetup.Client;
        var channels = TestConfig.LedChannels;
        int colorCount = RainbowColors.Length;
        int totalSets = 0;

        // Warm-up: set all channels to first color
        foreach (var ch in channels)
            await client.Resources.SetValueAsync(ch, RainbowColors[0]);

        var sw = Stopwatch.StartNew();

        for (int cycle = 0; cycle < cycles; cycle++)
        {
            for (int offset = 0; offset < colorCount; offset++)
            {
                var values = new Dictionary<string, string>();
                for (int i = 0; i < channels.Length; i++)
                {
                    var colorIndex = (offset + i) % colorCount;
                    values[channels[i]] = RainbowColors[colorIndex];
                }

                await client.Resources.SetValuesAsync(values);
                totalSets++;
            }
        }

        sw.Stop();

        double totalMs = sw.Elapsed.TotalMilliseconds;
        double avgMs = totalMs / totalSets;

        Console.WriteLine($"Rainbow LED test: {cycles} cycles × {colorCount} steps = {totalSets} batch sets");
        Console.WriteLine($"  Channels   : {channels.Length}");
        Console.WriteLine($"  Total time : {totalMs:F1} ms");
        Console.WriteLine($"  Average    : {avgMs:F2} ms/batch");
        Console.WriteLine($"  Throughput : {totalSets / sw.Elapsed.TotalSeconds:F1} batches/s");
        Console.WriteLine($"  Per-channel: {totalSets * channels.Length / sw.Elapsed.TotalSeconds:F1} sets/s");

        // Reset all to off
        foreach (var ch in channels)
            await client.Resources.SetValueAsync(ch, "Black");
    }
}
