using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AccordionQ2.WebApiClient.Tests;

[TestClass]
[TestCategory("Performance")]
public class PerformanceTests
{
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
}
