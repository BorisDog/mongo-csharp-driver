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
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using FluentAssertions;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Bson.TestHelpers;
using MongoDB.TestHelpers.XunitExtensions;
using Moq;
using Xunit;

namespace MongoDB.Bson.Tests.Serialization
{
    public class BsonClassMapSerializerTests
    {
        private static readonly BsonClassMap __classMap1;
        private static readonly BsonClassMap __classMap2;

        static BsonClassMapSerializerTests()
        {
            __classMap1 = new BsonClassMap(typeof(MyModel));
            __classMap1.AutoMap();
            __classMap1.Freeze();

            __classMap2 = new BsonClassMap(typeof(MyModel));
            __classMap2.AutoMap();
            __classMap2.MapProperty("Id").SetSerializer(new StringSerializer(BsonType.ObjectId));
            __classMap2.Freeze();
        }

        // public methods
        [Theory]
        [ParameterAttributeData]
        public void Deserialize_should_not_throw_when_all_required_elements_are_present(
            [Values(0, 1, 8, 23, 63, 111, 127, 128, 129, 555, 1024, 2500)]int membersCount)
        {
            var subject = BuildTypeAndGetSerializer(membersCount);
            var json = BuildJson(Enumerable.Range(0, membersCount));

            using var reader = new JsonReader(json);
            var context = BsonDeserializationContext.CreateRoot(reader);

            var obj = subject.Deserialize(context);

            ValidateObject(obj, Enumerable.Range(0, membersCount));
        }

        [Theory]
        [ParameterAttributeData]
        public void Deserialize_should_not_use_trie_when_all_elements_are_present([Values(0, 1, 8, 23)]int membersCount)
        {
            var subject = BuildTypeAndGetSerializer(membersCount);
            var json = BuildJson(Enumerable.Range(0, membersCount));

            var bson = BsonDocument.Parse(json).ToBson();

            var bsonReaderMock = new Mock<ReadOnlyMemoryBsonReaderProxy>(new ReadOnlyMemory<byte>(bson))
            {
                CallBase = true
            };

            var context = BsonDeserializationContext.CreateRoot(bsonReaderMock.Object);
            var obj = subject.Deserialize(context);

            ValidateObject(obj, Enumerable.Range(0, membersCount));

            bsonReaderMock.Verify(r => r.ReadName(It.IsAny<INameDecoder>()), Times.Never);
            bsonReaderMock.Verify(r => r.ValidateName(It.IsAny<string>(), It.IsAny<ReadOnlyMemory<byte>>()),
                Times.Exactly(membersCount));
        }

        [Theory]
        [MemberData(nameof(Deserialize_should_fallback_to_trie_when_some_elements_are_not_in_sequential_order_MemberData))]
        public void Deserialize_should_fallback_to_trie_when_some_elements_are_not_in_sequential_order(
            int pocoMembersCount,
            int[] actualDataMembersIndexes,
            int expectedTrieInvocations,
            int expectedValidateNameInvocations)
        {
            var subject = BuildTypeAndGetSerializer(pocoMembersCount, isMemberRequired: false, ignoreExtraElements: true);
            var json = BuildJson(actualDataMembersIndexes);
            var bson = BsonDocument.Parse(json).ToBson();

            var bsonReaderMock = new Mock<ReadOnlyMemoryBsonReaderProxy>(new ReadOnlyMemory<byte>(bson))
            {
                CallBase = true
            };

            var context = BsonDeserializationContext.CreateRoot(bsonReaderMock.Object);
            var obj = subject.Deserialize(context);

            ValidateObject(obj, actualDataMembersIndexes.Where(i => i < pocoMembersCount));

            bsonReaderMock.Verify(r => r.ReadName(It.IsAny<INameDecoder>()), Times.Exactly(expectedTrieInvocations));
            bsonReaderMock.Verify(r => r.ValidateName(It.IsAny<string>(), It.IsAny<ReadOnlyMemory<byte>>()), Times.Exactly(expectedValidateNameInvocations));
        }

        public static IEnumerable<object[]> Deserialize_should_fallback_to_trie_when_some_elements_are_not_in_sequential_order_MemberData()
        {
            yield return [1, new[] { 0 }, 0, 1 ];
            yield return [1, Array.Empty<int>(), 0, 0];
            yield return [10, Enumerable.Range(0, 10).Except([1]).ToArray(), 8, 2];
            yield return [10, Enumerable.Range(0, 10).Except([8]).ToArray(), 1, 9];
            yield return [10, Enumerable.Range(0, 10).Except([9]).ToArray(), 0, 9];
            yield return [10, Enumerable.Range(0, 10).Except([3, 5]).ToArray(), 5, 4];
            yield return [10, Enumerable.Range(0, 10).Reverse().ToArray(), 10, 1];
            yield return [32, Enumerable.Range(0, 32).Except([20]).ToArray(), 11, 21];
            yield return [32, Enumerable.Range(0, 32).Except([20]).Concat([90]).ToArray(), 12, 21];
            yield return [32, Enumerable.Range(0, 32).Except([20]).Concat([90, 100, 20]).ToArray(), 14, 21];
        }

        [Theory]
        [InlineData(1, 0)]
        [InlineData(8, 0)]
        [InlineData(8, 7)]
        [InlineData(256, 1)]
        [InlineData(256, 255)]
        [InlineData(555, 333)]
        [InlineData(555, 551)]
        [InlineData(555, 554)]
        [InlineData(1024, 0)]
        [InlineData(1024, 555)]
        [InlineData(1024, 992)]
        [InlineData(1024, 993)]
        [InlineData(1024, 1000)]
        [InlineData(1024, 1023)]
        public void Deserialize_should_throw_FormatException_when_required_element_is_not_found(int membersCount, int missingMemberIndex)
        {
            var memberIndexes = Enumerable.Range(0, membersCount).Except([missingMemberIndex]).ToArray();
            var subject = BuildTypeAndGetSerializer(membersCount);
            var json = BuildJson(memberIndexes);

            using var reader = new JsonReader(json);
            var context = BsonDeserializationContext.CreateRoot(reader);

            var exception = Record.Exception(() => subject.Deserialize(context));
            exception.Should()
                .BeOfType<FormatException>()
                .Subject.Message.Should().Contain($"Prop_{missingMemberIndex}");
        }

        [Fact]
        public void Deserialize_should_throw_invalidOperationException_when_creator_returns_null()
        {
            var bsonClassMap = new BsonClassMap<MyModel>();
            bsonClassMap.SetCreator(() => null);
            bsonClassMap.Freeze();

            var subject = new BsonClassMapSerializer<MyModel>(bsonClassMap);

            using var reader = new JsonReader("{ \"_id\": \"just_an_id\" }");
            var context = BsonDeserializationContext.CreateRoot(reader);

            var exception = Record.Exception(() => subject.Deserialize(context));
            exception.Should().BeOfType<BsonSerializationException>();
        }

        [Fact]
        public void Deserialize_should_throw_when_no_creators_found()
        {
            var bsonClassMap = new BsonClassMap<ModelWithCtor>();
            bsonClassMap.AutoMap();
            bsonClassMap.Freeze();

            var subject = new BsonClassMapSerializer<ModelWithCtor>(bsonClassMap);

            using var reader = new JsonReader("{ \"_id\": \"just_an_id\" }");
            var context = BsonDeserializationContext.CreateRoot(reader);

            var exception = Record.Exception(() => subject.Deserialize(context));
            exception.Should().BeOfType<BsonSerializationException>()
                .Subject.Message.Should().Be($"No matching creator found for class {typeof(ModelWithCtor).FullName}.");
        }

        [Fact]
        public void Equals_null_should_return_false()
        {
            var x = new BsonClassMapSerializer<MyModel>(__classMap1);

            var result = x.Equals(null);

            result.Should().Be(false);
        }

        [Fact]
        public void Equals_object_should_return_false()
        {
            var x = new BsonClassMapSerializer<MyModel>(__classMap1);
            var y = new object();

            var result = x.Equals(y);

            result.Should().Be(false);
        }

        [Fact]
        public void Equals_self_should_return_true()
        {
            var x = new BsonClassMapSerializer<MyModel>(__classMap1);

            var result = x.Equals(x);

            result.Should().Be(true);
        }

        [Fact]
        public void Equals_with_equal_fields_should_return_true()
        {
            var x = new BsonClassMapSerializer<MyModel>(__classMap1);
            var y = new BsonClassMapSerializer<MyModel>(__classMap1);

            var result = x.Equals(y);

            result.Should().Be(true);
        }

        [Fact]
        public void Equals_with_not_equal_field_should_return_false()
        {
            var x = new BsonClassMapSerializer<MyModel>(__classMap1);
            var y = new BsonClassMapSerializer<MyModel>(__classMap2);

            var result = x.Equals(y);

            result.Should().Be(false);
        }

        [Theory]
        [InlineData(0, 0, true)]
        [InlineData(1, 1, true)]
        [InlineData(2, 1, true)]
        [InlineData(32, 1, true)]
        [InlineData(33, 2, true)]
        [InlineData(256, 8, true)]
        [InlineData(257, 9, false)]
        public void FastMemberMapHelper_GetMembersBitArrayLength_should_return_correctValue(int memberCount, int expectedLengthInUInts, bool expectedUseStackAlloc)
        {
            var (lengthInUInts, useStackAlloc) = BsonClassMapSerializer<MyModel>.FastMemberMapHelper.GetLengthInUInts(memberCount);

            lengthInUInts.ShouldBeEquivalentTo(expectedLengthInUInts);
            useStackAlloc.ShouldBeEquivalentTo(expectedUseStackAlloc);
        }

        [Fact]
        public void FastMemberMapHelper_GetMembersBitArray_with_span_should_use_the_provided_span()
        {
            var backingArray = new uint[] { 1, 2, 3 };
            using var bitArray =  BsonClassMapSerializer<MyModel>.FastMemberMapHelper.GetMembersBitArray(backingArray);

            bitArray.Span.ToArray().ShouldBeEquivalentTo(new uint[] { 0, 0, 0 });
            bitArray.ArrayPool.Should().Be(null);

            bitArray.Span[0] = 12;
            backingArray.ShouldBeEquivalentTo(new uint[] { 12, 0, 0 });
        }

        [Theory]
        [InlineData(3)]
        [InlineData(25)]
        public void FastMemberMapHelper_GetMembersBitArray_with_length_should_allocate_span(int length)
        {
            using var bitArray = BsonClassMapSerializer<MyModel>.FastMemberMapHelper.GetMembersBitArray(length);

            bitArray.Span.ToArray().ShouldBeEquivalentTo(Enumerable.Repeat<uint>(0, length));
            bitArray.ArrayPool.Should().Be(ArrayPool<uint>.Shared);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        public void FastMemberMapHelper_MembersBitArray_with_arraypool_should_dispose_only_once(int disposeCount)
        {
            var backingArray = new uint[] { 1, 2, 3 };

            var mockArrayPool = new Mock<ArrayPool<uint>>();
            mockArrayPool.Setup(p => p.Rent(backingArray.Length)).Returns(backingArray);
            var bitArray = new BsonClassMapSerializer<MyModel>.FastMemberMapHelper.MembersBitArray(backingArray.Length, mockArrayPool.Object);

            for (int i = 0; i < disposeCount; i++)
            {
                bitArray.Dispose();
            }

            mockArrayPool.Verify(a => a.Return(backingArray, false), Times.Once);
        }

        [Theory]
        [InlineData(1, 0)]
        [InlineData(8, 0)]
        [InlineData(8, 7)]
        [InlineData(99, 100)]
        [InlineData(266, 255)]
        [InlineData(544, 0)]
        [InlineData(621, 255)]
        public void FastMemberMapHelper_GetMembersBitArray_SetMemberIndex_should_set_correct_bit(int membersCount, int memberIndex)
        {
            var (length, _) = BsonClassMapSerializer<MyModel>.FastMemberMapHelper.GetLengthInUInts(membersCount);
            using var bitArray = BsonClassMapSerializer<MyModel>.FastMemberMapHelper.GetMembersBitArray(length);

            var span = bitArray.Span;
            var blockIndex = memberIndex >> 5;
            var bitIndex = memberIndex & 31;

            bitArray.SetMemberIndex(memberIndex);

            for (var i = 0; i < span.Length; i++)
            {
                for (int b = 0; b < 32; b++)
                {
                    var bit = span[i] & (1U << b);

                    if (i == blockIndex && b == bitIndex)
                    {
                        bit.Should().Be(1U << b);
                    }
                    else
                    {
                        bit.Should().Be(0);
                    }
                }
            }
        }

        [Fact]
        public void GetHashCode_should_return_zero()
        {
            var x = new BsonClassMapSerializer<MyModel>(__classMap1);

            var result = x.GetHashCode();
            result.Should().Be(0);
        }

        private IBsonSerializer BuildTypeAndGetSerializer(int propertiesCount, bool isMemberRequired = true, bool ignoreExtraElements = false)
        {
            var assemblyName = new AssemblyName("DynamicAssembly");
            var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
            var moduleBuilder = assemblyBuilder.DefineDynamicModule("DynamicModule");

            var typeBuilder = moduleBuilder.DefineType($"MyDynamicClass_{propertiesCount}", TypeAttributes.Public | TypeAttributes.Class);

            for (var i = 0; i < propertiesCount; i++)
            {
                _ = typeBuilder.DefineField($"Prop_{i}",
                    typeof(string),
                    FieldAttributes.Public);
            }

            var newType = typeBuilder.CreateType();

            var classMap = new BsonClassMap(newType);
            for (var i = 0; i < propertiesCount; i++)
            {
                classMap
                    .MapField($"Prop_{i}")
                    .SetIsRequired(isMemberRequired);
            }
            classMap.SetIgnoreExtraElements(ignoreExtraElements);
            classMap.Freeze();

            var classMapSerializerType = typeof(BsonClassMapSerializer<>).MakeGenericType(newType);
            var classMapSerializer = (IBsonSerializer)Activator.CreateInstance(classMapSerializerType, classMap);

            return classMapSerializer;
        }

        private static string BuildJson(IEnumerable<int> memberIndexes) =>
            $"{{{string.Join(",", memberIndexes.Select(i => $"\"Prop_{i}\" : \"Value_{i}\""))}}}";

        private static void ValidateObject(object obj, IEnumerable<int> memberIndexes)
        {
            foreach (var index in  memberIndexes)
            {
                Reflector.GetFieldValue(obj, $"Prop_{index}", BindingFlags.Public | BindingFlags.Instance)
                    .Should().Be($"Value_{index}");
            }
        }

        // nested classes
        private class MyModel
        {
            public string Id { get; set; }
        }

        private class ModelWithCtor
        {
            private readonly string _myId;
            private readonly int _i;

            public ModelWithCtor(string id, int i)
            {
                _myId = id;
                _i = i;
            }

            public string Id => _myId;
            public int I => _i;
        }
    }
}
