using System;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;

namespace Benchmark
{
    internal class CustomSerializer<T> : IBsonSerializer<T> where T : class, new()
    {
        private readonly IDocumentReader _reader;

        [ThreadStatic]
        private static byte[] _buffer;

        public Type ValueType => typeof(T);

        public CustomSerializer(bool addId)
        {
            _reader = DocumentReaderRegistry.GetReader(typeof(T), addId);
        }

        public void Serialize(BsonSerializationContext context, BsonSerializationArgs args, T value)
        {
        }

        public unsafe T Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        {
            if (_buffer == null)
            {
                _buffer = new byte[1024 * 1024 * 10];
            }

            var bsonReader = context.Reader as BsonBinaryReader;

            var bsonType = bsonReader.State != BsonReaderState.Value ? bsonReader.ReadBsonType() : bsonReader.CurrentBsonType;
            if (bsonReader.State == BsonReaderState.Name)
                bsonReader.SkipName();
            var docSize = bsonReader.BsonStream.ReadInt32() - 4 - 1;

            bsonReader.BsonStream.ReadBytes(_buffer, 0, docSize + 1);

            T result;
            fixed (byte* pBuffer = &_buffer[0])
            {
                result = _reader.ReadDocument(new IntPtr(pBuffer), docSize) as T;
            }

            bsonReader.State = bsonReader.IsArrayContext ? BsonReaderState.EndOfDocument : BsonReaderState.Type;

            return result;
        }

        object IBsonSerializer.Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        {
            return new T();
        }

        public void Serialize(BsonSerializationContext context, BsonSerializationArgs args, object value)
        {
        }
    }
}
