namespace MongoDB.Driver.Logger
{
    internal interface ILogger
    {
        public void Log(LogLevel logLevel, string format, params object[] arguments);
    }

    internal interface ILogger<TCatergory> : ILogger
    {
    }
}
