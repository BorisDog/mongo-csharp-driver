/* Copyright 2010-present MongoDB Inc.
*
* Licensed under the Apache License, Version 2.0 (the "License");
* you may not use this file except in compliance with the License.
* You may obtain a copy of the License at
*
* http://www.apache.org/licenses/LICENSE-2.0
*
* Unless required by applicable law or agreed to in writing, software
* distributed under the License is distributed on an "AS IS" BASIS,
* WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
* See the License for the specific language governing permissions and
* limitations under the License.
*/

using System;
using MongoDB.Bson.ObjectModel;

namespace MongoDB.Bson.Serialization.Serializers
{
    internal static class BsonVectorSerializerBase
    {
        public static BsonVectorSerializer<BsonVectorFloat32, float> BsonVectorSerializerFloat32 { get; } = new BsonVectorSerializer<BsonVectorFloat32, float>(BsonVectorDataType.Float32);
        public static BsonVectorSerializer<BsonVectorInt8, byte> BsonVectorSerializerInt8 { get; } = new BsonVectorSerializer<BsonVectorInt8, byte>(BsonVectorDataType.Int8);
        public static BsonVectorSerializer<BsonVectorPackedBit, byte> BsonVectorSerializerPackedBit { get; } = new BsonVectorSerializer<BsonVectorPackedBit, byte>(BsonVectorDataType.PackedBit);

        public static IBsonSerializer CreateArraySerializer(Type itemType, BsonVectorDataType bsonVectorDataType) =>
            CreateSerializerInstance(typeof(BsonVectorArraySerializer<>).MakeGenericType(itemType), bsonVectorDataType);

        public static IBsonSerializer CreateBsonVectorSerializer(Type bsonVectorType, Type itemType, BsonVectorDataType bsonVectorDataType) =>
            CreateSerializerInstance(typeof(BsonVectorSerializer<,>).MakeGenericType(bsonVectorType, itemType), bsonVectorDataType);

        public static IBsonSerializer CreateMemorySerializer(Type itemType, BsonVectorDataType bsonVectorDataType) =>
            CreateSerializerInstance(typeof(BsonVectorMemorySerializer<>).MakeGenericType(itemType), bsonVectorDataType);

        public static IBsonSerializer CreateReadonlyMemorySerializer(Type itemType, BsonVectorDataType bsonVectorDataType) =>
            CreateSerializerInstance(typeof(BsonVectorReadOnlyMemorySerializer<>).MakeGenericType(itemType), bsonVectorDataType);

        public static IBsonSerializer CreateSerializer(Type type, BsonVectorDataType bsonVectorDataType)
        {
            // Arrays
            if (type.IsArray)
            {
                var itemType = type.GetElementType();
                return CreateArraySerializer(itemType, bsonVectorDataType);
            }

            // BsonVector
            if (type == typeof(BsonVectorFloat32) ||
                type == typeof(BsonVectorInt8) ||
                type == typeof(BsonVectorPackedBit))
            {
                return CreateBsonVectorSerializer(type, GetItemType(type.BaseType), bsonVectorDataType);
            }

            // Memory/ReadonlyMemory
            var genericTypeDefinition = type.IsGenericType ? type.GetGenericTypeDefinition() : null;
            if (genericTypeDefinition == typeof(Memory<>))
            {
                return CreateMemorySerializer(GetItemType(type), bsonVectorDataType);
            }
            else if (genericTypeDefinition == typeof(ReadOnlyMemory<>))
            {
                return CreateReadonlyMemorySerializer(GetItemType(type), bsonVectorDataType);
            }

            throw new InvalidOperationException($"Type {type} is not supported for a binary vector.");

            Type GetItemType(Type actualType)
            {
                var arguments = actualType.GetGenericArguments();
                if (arguments.Length != 1)
                {
                    throw new InvalidOperationException($"Type {type} is not supported for a binary vector.");
                }

                return arguments[0];
            }
        }

        private static IBsonSerializer CreateSerializerInstance(Type vectorSerializerType, BsonVectorDataType bsonVectorDataType) =>
             (IBsonSerializer)Activator.CreateInstance(vectorSerializerType, bsonVectorDataType);
    }

    internal abstract class BsonVectorSerializerBase<TItemCollection, TItem> : SerializerBase<TItemCollection>
         where TItem : struct
    {
        public BsonVectorSerializerBase(BsonVectorDataType bsonVectorDataType)
        {
            BsonVectorReader.ValidateDataType<TItem>(bsonVectorDataType);

            VectorDataType = bsonVectorDataType;
        }

        public BsonVectorDataType VectorDataType { get; }

        public override int GetHashCode() => 0;

        public override bool Equals(object obj)
        {
            if (object.ReferenceEquals(obj, null)) { return false; }
            if (object.ReferenceEquals(this, obj)) { return true; }
            return
                base.Equals(obj) &&
                obj is BsonVectorSerializerBase<TItemCollection, TItem> other &&
                object.Equals(VectorDataType, other.VectorDataType);
        }
    }

    internal sealed class BsonVectorSerializer<TItemCollection, TItem> : BsonVectorSerializerBase<TItemCollection, TItem>
        where TItemCollection : BsonVectorBase<TItem>
        where TItem : struct
    {
        public BsonVectorSerializer(BsonVectorDataType bsonVectorDataType) :
            base(bsonVectorDataType)
        {
        }

        public override sealed TItemCollection Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        {
            var reader = context.Reader;

            var bsonType = reader.GetCurrentBsonType();
            if (bsonType != BsonType.Binary)
            {
                throw CreateCannotDeserializeFromBsonTypeException(bsonType);
            }

            var binaryData = reader.ReadBinaryData();
            return binaryData.ToBsonVector<TItem>() as TItemCollection;
        }

        public override sealed void Serialize(BsonSerializationContext context, BsonSerializationArgs args, TItemCollection bsonVector)
        {
            var binaryData = bsonVector.ToBsonBinaryData();

            context.Writer.WriteBinaryData(binaryData);
        }
    }

    internal abstract class BsonVectorToCollectionSerializer<TItemCollection, TItem> : BsonVectorSerializerBase<TItemCollection, TItem>
         where TItem : struct
    {
        public BsonVectorToCollectionSerializer(BsonVectorDataType bsonVectorDataType) :
            base(bsonVectorDataType)
        {
        }

        public override sealed TItemCollection Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        {
            var reader = context.Reader;

            var bsonType = reader.GetCurrentBsonType();
            if (bsonType != BsonType.Binary)
            {
                throw CreateCannotDeserializeFromBsonTypeException(bsonType);
            }

            var binaryData = reader.ReadBinaryData();
            var (elements, _, _) = binaryData.ToBsonVectorAsArray<TItem>();

            return CreateResult(elements);
        }

        public override sealed void Serialize(BsonSerializationContext context, BsonSerializationArgs args, TItemCollection value)
        {
            byte padding = 0;
            if (value is BsonVectorPackedBit bsonVectorPackedBit)
            {
                padding = bsonVectorPackedBit.Padding;
            }

            var vectorData = GetSpan(value);
            var bytes = BsonVectorWriter.VectorDataToBytes(vectorData, VectorDataType, padding);
            var binaryData = new BsonBinaryData(bytes, BsonBinarySubType.Vector);

            context.Writer.WriteBinaryData(binaryData);
        }

        private protected abstract TItemCollection CreateResult(TItem[] elements);
        private protected abstract ReadOnlySpan<TItem> GetSpan(TItemCollection data);
    }

    internal sealed class BsonVectorArraySerializer<TItem> : BsonVectorToCollectionSerializer<TItem[], TItem>
         where TItem : struct
    {
        public BsonVectorArraySerializer(BsonVectorDataType bsonVectorDataType) : base(bsonVectorDataType)
        {
        }

        private protected override ReadOnlySpan<TItem> GetSpan(TItem[] data) => data;
        private protected override TItem[] CreateResult(TItem[] elements) => elements;
    }

    internal sealed class BsonVectorMemorySerializer<TItem> : BsonVectorToCollectionSerializer<Memory<TItem>, TItem>
         where TItem : struct
    {
        public BsonVectorMemorySerializer(BsonVectorDataType bsonVectorDataType) : base(bsonVectorDataType)
        {
        }

        private protected override ReadOnlySpan<TItem> GetSpan(Memory<TItem> data) =>
            data.Span;

        private protected override Memory<TItem> CreateResult(TItem[] elements) =>
            new(elements);
    }

    internal sealed class BsonVectorReadOnlyMemorySerializer<TItem> : BsonVectorToCollectionSerializer<ReadOnlyMemory<TItem>, TItem>
         where TItem : struct
    {
        public BsonVectorReadOnlyMemorySerializer(BsonVectorDataType bsonVectorDataType) : base(bsonVectorDataType)
        {
        }

        private protected override ReadOnlySpan<TItem> GetSpan(ReadOnlyMemory<TItem> data) =>
            data.Span;

        private protected override ReadOnlyMemory<TItem> CreateResult(TItem[] elements) =>
            new(elements);
    }
}
