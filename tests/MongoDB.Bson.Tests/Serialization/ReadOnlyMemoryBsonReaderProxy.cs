using System;
using MongoDB.Bson.IO;

namespace MongoDB.Bson.Tests.Serialization;

public class ReadOnlyMemoryBsonReaderProxy(ReadOnlyMemory<byte> memory) : IBsonReaderInternal
{
    private readonly ReadOnlyMemoryBsonReader _reader = new(memory);

    public BsonType CurrentBsonType => _reader.CurrentBsonType;
    public BsonReaderState State => _reader.State;
    public void Close() => _reader.Close();
    public BsonReaderBookmark GetBookmark() => _reader.GetBookmark();
    public BsonType GetCurrentBsonType() => _reader.GetCurrentBsonType();
    public bool IsAtEndOfFile() => _reader.IsAtEndOfFile();
    public void PopSettings() => _reader.PopSettings();
    public void PushSettings(Action<BsonReaderSettings> configurator) => _reader.PushSettings(configurator);
    public BsonBinaryData ReadBinaryData() => _reader.ReadBinaryData();
    public bool ReadBoolean() => _reader.ReadBoolean();
    public BsonType ReadBsonType() => _reader.ReadBsonType();
    public byte[] ReadBytes() => _reader.ReadBytes();
    public long ReadDateTime() => _reader.ReadDateTime();
    public Decimal128 ReadDecimal128() => _reader.ReadDecimal128();
    public double ReadDouble() => _reader.ReadDouble();
    public void ReadEndArray() => _reader.ReadEndArray();
    public void ReadEndDocument() => _reader.ReadEndDocument();
    public Guid ReadGuid() => _reader.ReadGuid();
    public Guid ReadGuid(GuidRepresentation guidRepresentation) => _reader.ReadGuid(guidRepresentation);
    public int ReadInt32() => _reader.ReadInt32();
    public long ReadInt64() => _reader.ReadInt64();
    public string ReadJavaScript() => _reader.ReadJavaScript();
    public string ReadJavaScriptWithScope() => _reader.ReadJavaScriptWithScope();
    public void ReadMaxKey() => _reader.ReadMaxKey();
    public void ReadMinKey() => _reader.ReadMinKey();
    public virtual string ReadName(INameDecoder nameDecoder) => _reader.ReadName(nameDecoder);
    public void ReadNull() => _reader.ReadNull();
    public ObjectId ReadObjectId() => _reader.ReadObjectId();
    public IByteBuffer ReadRawBsonArray() => _reader.ReadRawBsonArray();
    public IByteBuffer ReadRawBsonDocument() => _reader.ReadRawBsonDocument();
    public BsonRegularExpression ReadRegularExpression() => _reader.ReadRegularExpression();
    public void ReadStartArray() => _reader.ReadStartArray();
    public void ReadStartDocument() => _reader.ReadStartDocument();
    public string ReadString() => _reader.ReadString();
    public string ReadSymbol() => _reader.ReadSymbol();
    public long ReadTimestamp() => _reader.ReadTimestamp();
    public void ReadUndefined() => _reader.ReadUndefined();
    public void ReturnToBookmark(BsonReaderBookmark bookmark) => _reader.ReturnToBookmark(bookmark);
    public void SkipName() => _reader.SkipName();
    public void SkipValue() => _reader.SkipValue();

#pragma warning disable CA2119 // Safely declare methods that override protected members
    public virtual bool ValidateName(string suggestedName, ReadOnlyMemory<byte> suggestedNameBytes) =>
        _reader.ValidateName(suggestedName, suggestedNameBytes);
#pragma warning restore CA2119

    public void Dispose() => _reader.Dispose();
}
