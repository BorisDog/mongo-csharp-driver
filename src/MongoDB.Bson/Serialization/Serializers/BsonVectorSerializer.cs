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
using MongoDB.Bson.IO;
using MongoDB.Bson.ObjectModel;

namespace MongoDB.Bson.Serialization.Serializers
{
    internal static class BsonVectorSerializer
    {
        public static BsonVectorSerializer<BsonVectorFloat32, float> BsonVectorFloat32Serializer { get; } = new BsonVectorSerializer<BsonVectorFloat32, float>(BsonVectorDataType.Float32);
        public static BsonVectorSerializer<BsonVectorInt8, byte> BsonVectorInt8Serializer { get; } = new BsonVectorSerializer<BsonVectorInt8, byte>(BsonVectorDataType.Int8);
        public static BsonVectorSerializer<BsonVectorPackedBit, byte> BsonVectorPackedBitSerializer { get; } = new BsonVectorSerializer<BsonVectorPackedBit, byte>(BsonVectorDataType.PackedBit);

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

            throw new NotSupportedException($"Type {type} is not supported for a binary vector.");

            Type GetItemType(Type collectionType)
            {
                var genericArguments = collectionType.GetGenericArguments();
                if (genericArguments.Length != 1)
                {
                    throw new NotSupportedException($"Type {type} is not supported for a binary vector.");
                }

                return genericArguments[0];
            }
        }

        private static IBsonSerializer CreateSerializerInstance(Type vectorSerializerType, BsonVectorDataType bsonVectorDataType) =>
             (IBsonSerializer)Activator.CreateInstance(vectorSerializerType, bsonVectorDataType);
    }

    /// <summary>
    /// Represents a serializer for BSON vector to/from a given collection.
    /// </summary>
    /// <typeparam name="TItemCollection">The collection type.</typeparam>
    /// <typeparam name="TItem">The .NET data type.</typeparam>
    internal abstract class BsonVectorSerializerBase<TItemCollection, TItem> : SerializerBase<TItemCollection>
         where TItem : struct
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BsonVectorSerializerBase{TItemCollection, TItem}"/> class.
        /// </summary>
        /// <param name="bsonVectorDataType">Type of the bson vector data.</param>
        public BsonVectorSerializerBase(BsonVectorDataType bsonVectorDataType)
        {
            BsonVectorReader.ValidateDataType<TItem>(bsonVectorDataType);

            VectorDataType = bsonVectorDataType;
        }

        /// <summary>
        /// Gets the type of the vector data.
        /// </summary>
        public BsonVectorDataType VectorDataType { get; }

        /// <inheritdoc/>
        public override int GetHashCode() => 0;

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            if (object.ReferenceEquals(obj, null)) { return false; }
            if (object.ReferenceEquals(this, obj)) { return true; }
            return
                base.Equals(obj) &&
                obj is BsonVectorSerializerBase<TItemCollection, TItem> other &&
                object.Equals(VectorDataType, other.VectorDataType);
        }

        protected BsonBinaryData ReadAndValidateBsonBinaryData(IBsonReader bsonReader)
        {
            var bsonType = bsonReader.GetCurrentBsonType();
            if (bsonType != BsonType.Binary)
            {
                throw CreateCannotDeserializeFromBsonTypeException(bsonType);
            }

            var binaryData = bsonReader.ReadBinaryData();

            return binaryData;
        }
    }

    /// <summary>
    /// Represents a serializer for <see cref="BsonVectorBase{TItem}"/>.
    /// </summary>
    /// <typeparam name="TItemCollection">The concrete type derived from <see cref="BsonVectorBase{T}"/>.</typeparam>
    /// <typeparam name="TItem">The .NET data type.</typeparam>
    internal sealed class BsonVectorSerializer<TItemCollection, TItem> : BsonVectorSerializerBase<TItemCollection, TItem>
        where TItemCollection : BsonVectorBase<TItem>
        where TItem : struct
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ReadonlyMemorySerializer{TItem}" /> class.
        /// </summary>
        public BsonVectorSerializer(BsonVectorDataType bsonVectorDataType) :
            base(bsonVectorDataType)
        {
        }

        /// <inheritdoc/>
        public override sealed TItemCollection Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        {
            var binaryData = ReadAndValidateBsonBinaryData(context.Reader);
            return (TItemCollection)binaryData.ToBsonVector<TItem>();
        }

        /// <inheritdoc/>
        public override sealed void Serialize(BsonSerializationContext context, BsonSerializationArgs args, TItemCollection bsonVector)
        {
            var binaryData = bsonVector.ToBsonBinaryData();

            context.Writer.WriteBinaryData(binaryData);
        }

        private static BsonVectorDataType GetDataType() =>
            typeof(TItemCollection) switch
            {
                _ when typeof(TItemCollection) == typeof(BsonVectorFloat32) => BsonVectorDataType.Float32,
                _ when typeof(TItemCollection) == typeof(BsonVectorInt8) => BsonVectorDataType.Int8,
                _ when typeof(TItemCollection) == typeof(BsonVectorPackedBit) => BsonVectorDataType.PackedBit,
                _ => throw new NotSupportedException($"{typeof(TItemCollection)} are not supported by {nameof(BsonVectorSerializer<TItemCollection, TItem>)}.")
            };
    }

    /// <summary>
    /// Represents a base class for serializers to/from collection of <typeparamref name="TItem"/>.
    /// </summary>
    /// <typeparam name="TItemCollection">The collection type.</typeparam>
    /// <typeparam name="TItem">The .NET data type.</typeparam>
    internal abstract class BsonVectorToCollectionSerializer<TItemCollection, TItem> : BsonVectorSerializerBase<TItemCollection, TItem>
         where TItem : struct
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BsonVectorToCollectionSerializer{TItemCollection, TItem}" /> class.
        /// </summary>
        public BsonVectorToCollectionSerializer(BsonVectorDataType bsonVectorDataType) :
            base(bsonVectorDataType)
        {
        }

        /// <inheritdoc/>
        public override sealed TItemCollection Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        {
            var binaryData = ReadAndValidateBsonBinaryData(context.Reader);
            var (elements, padding, _) = binaryData.ToBsonVectorAsArray<TItem>();

            if (padding != 0)
            {
                throw new FormatException($"Padding is supported only in {nameof(BsonVectorPackedBit)} data type.");
            }

            return CreateResult(elements);
        }

        /// <inheritdoc/>
        public override sealed void Serialize(BsonSerializationContext context, BsonSerializationArgs args, TItemCollection value)
        {
            var vectorData = GetItemsSpan(value);
            var bytes = BsonVectorWriter.WriteToBytes(vectorData, VectorDataType, 0);
            var binaryData = new BsonBinaryData(bytes, BsonBinarySubType.Vector);

            context.Writer.WriteBinaryData(binaryData);
        }

        private protected abstract TItemCollection CreateResult(TItem[] elements);
        private protected abstract ReadOnlySpan<TItem> GetItemsSpan(TItemCollection data);
    }

    /// <summary>
    /// Represents a serializer for BSON vector to/from array of <typeparamref name="TItem"/>.
    /// </summary>
    /// <typeparam name="TItem">The .NET data type.</typeparam>
    internal sealed class BsonVectorArraySerializer<TItem> : BsonVectorToCollectionSerializer<TItem[], TItem>
         where TItem : struct
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BsonVectorArraySerializer{TItem}" /> class.
        /// </summary>
        public BsonVectorArraySerializer(BsonVectorDataType bsonVectorDataType) : base(bsonVectorDataType)
        {
        }

        private protected override ReadOnlySpan<TItem> GetItemsSpan(TItem[] data) => data;
        private protected override TItem[] CreateResult(TItem[] elements) => elements;
    }

    /// <summary>
    /// Represents a serializer for BSON vector to/from <see cref="Memory{TItem}"/>
    /// </summary>
    /// <typeparam name="TItem">The .NET data type.</typeparam>
    internal sealed class BsonVectorMemorySerializer<TItem> : BsonVectorToCollectionSerializer<Memory<TItem>, TItem>
         where TItem : struct
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BsonVectorMemorySerializer{TItem}" /> class.
        /// </summary>
        public BsonVectorMemorySerializer(BsonVectorDataType bsonVectorDataType) : base(bsonVectorDataType)
        {
        }

        private protected override ReadOnlySpan<TItem> GetItemsSpan(Memory<TItem> data) =>
            data.Span;

        private protected override Memory<TItem> CreateResult(TItem[] elements) =>
            new(elements);
    }

    /// <summary>
    /// Represents a serializer for <see cref="ReadOnlyMemory{TItem}"/>.
    /// </summary>
    /// <typeparam name="TItem">The .NET data type.</typeparam>
    internal sealed class BsonVectorReadOnlyMemorySerializer<TItem> : BsonVectorToCollectionSerializer<ReadOnlyMemory<TItem>, TItem>
         where TItem : struct
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BsonVectorReadOnlyMemorySerializer{TItem}" /> class.
        /// </summary>
        public BsonVectorReadOnlyMemorySerializer(BsonVectorDataType bsonVectorDataType) : base(bsonVectorDataType)
        {
        }

        private protected override ReadOnlySpan<TItem> GetItemsSpan(ReadOnlyMemory<TItem> data) =>
            data.Span;

        private protected override ReadOnlyMemory<TItem> CreateResult(TItem[] elements) =>
            new(elements);
    }
}
