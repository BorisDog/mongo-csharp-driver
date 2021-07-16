using MongoDB.Driver.Core.Misc;
using MongoDB.Driver.Logger;
using Xunit.Abstractions;

namespace MongoDB.Driver.Core.TestHelpers.Logging
{
    public class TestLoggable
    {
        private readonly ITestOutputHelper _output;

        public TestLoggable(ITestOutputHelper output)
        {
            _output = Ensure.IsNotNull(output, nameof(output));
        }

        internal ILogger<TCatergory> CreateLogger<TCatergory>() =>
            new XunitLogger<TCatergory>(_output);
    }
}
