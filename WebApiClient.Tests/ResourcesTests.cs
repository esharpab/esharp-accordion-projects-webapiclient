using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AccordionQ2.WebApiClient.Tests;

[TestClass]
[TestCategory("Integration")]
public class ResourcesTests
{

    [TestMethod]
    public async Task GetNames_ReturnsNonEmptyArray()
    {
        var names = await TestSetup.Client.Resources.GetNamesAsync();

        Console.WriteLine($"Resource names count: {names?.Length}");
        foreach (var n in names!)
            Console.WriteLine($"  Resource: {n}");

        Assert.IsNotNull(names);
        Assert.IsTrue(names.Length > 0, "Expected at least one resource name");
    }

    [TestMethod]
    public async Task GetNames_ContainsKnownResources()
    {
        var names = await TestSetup.Client.Resources.GetNamesAsync();

        Console.WriteLine($"Looking for: {TestConfig.CpuTempResource}, {TestConfig.Mon3V3Resource}, {TestConfig.UptimeResource}");
        Console.WriteLine($"Found CpuTemp: {names.Contains(TestConfig.CpuTempResource)}");
        Console.WriteLine($"Found Mon3V3: {names.Contains(TestConfig.Mon3V3Resource)}");
        Console.WriteLine($"Found Uptime: {names.Contains(TestConfig.UptimeResource)}");

        CollectionAssert.Contains(names, TestConfig.CpuTempResource);
        CollectionAssert.Contains(names, TestConfig.Mon3V3Resource);
        CollectionAssert.Contains(names, TestConfig.UptimeResource);
    }

    [TestMethod]
    public async Task GetValue_CpuTemp_ReturnsNumericValue()
    {
        var value = await TestSetup.Client.Resources.GetValueAsync(TestConfig.CpuTempResource);

        Console.WriteLine($"CpuTemp raw value: '{value}'");

        Assert.IsNotNull(value);
        Assert.IsTrue(
            double.TryParse(value, CultureInfo.InvariantCulture, out var temp),
            $"Expected numeric value, got '{value}'");
        Console.WriteLine($"CpuTemp parsed: {temp}°C");
        Assert.IsTrue(temp > 0 && temp < 120, $"CPU temp {temp}°C seems out of range");
    }

    [TestMethod]
    public async Task GetValue_Mon3V3_ReturnsVoltageInRange()
    {
        var value = await TestSetup.Client.Resources.GetValueAsync(TestConfig.Mon3V3Resource);

        Console.WriteLine($"Mon3V3 raw value: '{value}'");

        Assert.IsNotNull(value);
        Assert.IsTrue(
            double.TryParse(value, CultureInfo.InvariantCulture, out var voltage),
            $"Expected numeric value, got '{value}'");
        Console.WriteLine($"Mon3V3 parsed: {voltage}V");
        Assert.IsTrue(voltage > 2.5 && voltage < 4.0,
            $"3.3V rail at {voltage}V seems out of range");
    }

    [TestMethod]
    public async Task GetValue_Uptime_ReturnsNonEmpty()
    {
        var value = await TestSetup.Client.Resources.GetValueAsync(TestConfig.UptimeResource);

        Console.WriteLine($"Uptime: '{value}'");

        Assert.IsNotNull(value);
        Assert.IsFalse(string.IsNullOrWhiteSpace(value), "Uptime should not be empty");
    }

    [TestMethod]
    public async Task GetValues_MultipleResources_ReturnsDictionary()
    {
        var names = new[] { TestConfig.CpuTempResource, TestConfig.Mon3V3Resource, TestConfig.UptimeResource };
        var values = await TestSetup.Client.Resources.GetValuesAsync(names);

        Assert.IsNotNull(values);
        Console.WriteLine($"GetValues returned {values.Count} entries:");
        foreach (var kvp in values)
            Console.WriteLine($"  {kvp.Key} = '{kvp.Value}'");

        Assert.AreEqual(names.Length, values.Count, "Should return a value for each requested resource");
        foreach (var name in names)
        {
            Assert.IsTrue(values.ContainsKey(name), $"Missing value for '{name}'");
            Assert.IsFalse(string.IsNullOrEmpty(values[name]), $"Value for '{name}' should not be empty");
        }
    }
}
