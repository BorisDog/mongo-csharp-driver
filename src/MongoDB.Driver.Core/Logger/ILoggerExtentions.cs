namespace MongoDB.Driver.Logger
{
    internal static class ILoggerExtentions
    {
        public static void LogInfo<T>(this ILogger<T> logger, string message) =>
            logger?.Log(LogLevel.Information, "{0}: {1}", typeof(T).Name, message);
    }
}
