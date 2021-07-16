using MongoDB.Driver.Logger;
using Xunit.Abstractions;

namespace MongoDB.Driver.Core.TestHelpers.Logging
{
    internal static class XunitLoggerFactory
    {
        public static ILogger<TCatergory> CreateLogger<TCatergory>(ITestOutputHelper testOutputHelper) =>
            new XunitLogger<TCatergory>(testOutputHelper);
    }
}
