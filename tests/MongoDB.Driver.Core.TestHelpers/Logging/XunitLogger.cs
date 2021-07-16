using MongoDB.Driver.Core.Misc;
using MongoDB.Driver.Logger;
using Xunit.Abstractions;

namespace MongoDB.Driver.Core.TestHelpers.Logging
{
    internal sealed class XunitLogger<TCategory> : ILogger<TCategory>
    {
        private readonly ITestOutputHelper _output;

        public XunitLogger(ITestOutputHelper output)
        {
            _output = Ensure.IsNotNull(output, nameof(output));
        }

        public void Log(LogLevel logLevel, string format, params object[] arguments)
        {
            _output.WriteLine($"{logLevel}>{format}", arguments);
        }
    }
}
