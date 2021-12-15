using System;
using System.Collections.Concurrent;

namespace Benchmark
{
    internal static class DocumentReaderRegistry
    {
        private readonly static ConcurrentDictionary<string, IDocumentReader> _readers = new ConcurrentDictionary<string, IDocumentReader>();

        public static IDocumentReader GetReader(string typeName)
        {
            _readers.TryGetValue(typeName, out var reader);
            return reader;
        }

        public static IDocumentReader GetReader<T>()
        {
            _readers.TryGetValue(typeof(T).Name, out var reader);
            return reader;
        }

        public static IDocumentReader GetReader(Type type, bool addId)
        {
            var result = _readers.GetOrAdd(type.Name, n =>
            {
                var readerGenericType = typeof(DocumentReader<>);
                var readerType = readerGenericType.MakeGenericType(type);
                var reader = (IDocumentReader)Activator.CreateInstance(readerType, (object)addId);

                return reader;
            });

            return result;
        }
    }
}
