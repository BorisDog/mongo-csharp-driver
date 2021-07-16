namespace MongoDB.Driver.Logger
{
    internal interface ILoggerFactory
    {
        public ILogger<TCatergory> CreateLogger<TCatergory>();
    }
}
