using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AccordionQ2.WebApiClient.Tests;

[TestClass]
[TestCategory("Integration")]
public class MediaTests
{
    private static AccordionQ2Client _client = null!;

    [ClassInitialize]
    public static void ClassInit(TestContext _) =>
        _client = new AccordionQ2Client(TestConfig.BaseUrl);

    [ClassCleanup]
    public static void ClassCleanup() => _client?.Dispose();

    [TestMethod]
    public async Task ListFiles_ReturnsArray()
    {
        var files = await _client.Media.ListFilesAsync();

        Console.WriteLine($"Media files count: {files?.Length}");
        foreach (var f in files!)
            Console.WriteLine($"  Media: {f}");

        Assert.IsNotNull(files);
        // May be empty, but should not be null
    }
}
