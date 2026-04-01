using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AccordionQ2.WebApiClient.Tests;

[TestClass]
public static class TestSetup
{
    public static AccordionQ2Client Client { get; private set; } = null!;

    [AssemblyInitialize]
    public static void AssemblyInit(TestContext _) =>
        Client = new AccordionQ2Client(TestConfig.BaseUrl);

    [AssemblyCleanup]
    public static void AssemblyCleanup() => Client?.Dispose();
}
