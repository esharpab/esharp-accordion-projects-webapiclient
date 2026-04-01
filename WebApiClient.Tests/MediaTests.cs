using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AccordionQ2.WebApiClient.Tests;

[TestClass]
[TestCategory("Integration")]
public class MediaTests
{

    [TestMethod]
    public async Task ListFiles_ReturnsArray()
    {
        var files = await TestSetup.Client.Media.ListFilesAsync();

        Console.WriteLine($"Media files count: {files?.Length}");
        foreach (var f in files!)
            Console.WriteLine($"  Media: {f}");

        Assert.IsNotNull(files);
        // May be empty, but should not be null
    }
}
