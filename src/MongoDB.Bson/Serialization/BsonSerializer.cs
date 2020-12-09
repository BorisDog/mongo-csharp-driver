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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

// don't add using statement for MongoDB.Bson.Serialization.Serializers to minimize dependencies on DefaultSerializer
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.IdGenerators;

namespace MongoDB.Bson.Serialization
{
    /// <summary>
    /// A static class that represents the BSON serialization functionality.
    /// </summary>
    public static class BsonSerializer
    {
        // private static fields
        private static readonly ConcurrentDictionary<Type, IIdGenerator> __idGenerators = new ConcurrentDictionary<Type, IIdGenerator>();
        private static readonly ConcurrentDictionary<Type, IDiscriminatorConvention> __discriminatorConventions = new ConcurrentDictionary<Type, IDiscriminatorConvention>();
        private static readonly ConcurrentDictionary<BsonValue, HashSet<Type>> __discriminators = new ConcurrentDictionary<BsonValue, HashSet<Type>>();
        private static readonly ConcurrentDictionary<Type, Type> __discriminatedTypes = new ConcurrentDictionary<Type, Type>();
        private static readonly BsonSerializerRegistry __serializerRegistry = new BsonSerializerRegistry();
        private static readonly TypeMappingSerializationProvider __typeMappingSerializationProvider = new TypeMappingSerializationProvider();
        private static readonly ConcurrentDictionary<Type, Type> __typesWithRegisteredKnownTypes = new ConcurrentDictionary<Type, Type>();

        private static bool __useNullIdChecker = false;
        private static bool __useZeroIdChecker = false;

        // static constructor
        static BsonSerializer()
        {
            RegisterSerializationProviders();
            RegisterIdGenerators();
        }

        // public static properties
        /// <summary>
        /// Gets the serializer registry.
        /// </summary>
        public static IBsonSerializerRegistry SerializerRegistry
        {
            get { return __serializerRegistry; }
        }

        /// <summary>
        /// Gets or sets whether to use the NullIdChecker on reference Id types that don't have an IdGenerator registered.
        /// </summary>
        public static bool UseNullIdChecker
        {
            get { return __useNullIdChecker; }
            set { __useNullIdChecker = value; }
        }

        /// <summary>
        /// Gets or sets whether to use the ZeroIdChecker on value Id types that don't have an IdGenerator registered.
        /// </summary>
        public static bool UseZeroIdChecker
        {
            get { return __useZeroIdChecker; }
            set { __useZeroIdChecker = value; }
        }

        // public static methods
        /// <summary>
        /// Deserializes an object from a BsonDocument.
        /// </summary>
        /// <typeparam name="TNominalType">The nominal type of the object.</typeparam>
        /// <param name="document">The BsonDocument.</param>
        /// <param name="configurator">The configurator.</param>
        /// <returns>A deserialized value.</returns>
        public static TNominalType Deserialize<TNominalType>(BsonDocument document, Action<BsonDeserializationContext.Builder> configurator = null)
        {
            using (var bsonReader = new BsonDocumentReader(document))
            {
                return Deserialize<TNominalType>(bsonReader, configurator);
            }
        }

        /// <summary>
        /// Deserializes a value.
        /// </summary>
        /// <typeparam name="TNominalType">The nominal type of the object.</typeparam>
        /// <param name="bsonReader">The BsonReader.</param>
        /// <param name="configurator">The configurator.</param>
        /// <returns>A deserialized value.</returns>
        public static TNominalType Deserialize<TNominalType>(IBsonReader bsonReader, Action<BsonDeserializationContext.Builder> configurator = null)
        {
            var serializer = LookupSerializer<TNominalType>();
            var context = BsonDeserializationContext.CreateRoot(bsonReader, configurator);
            return serializer.Deserialize(context);
        }

        /// <summary>
        /// Deserializes an object from a BSON byte array.
        /// </summary>
        /// <typeparam name="TNominalType">The nominal type of the object.</typeparam>
        /// <param name="bytes">The BSON byte array.</param>
        /// <param name="configurator">The configurator.</param>
        /// <returns>A deserialized value.</returns>
        public static TNominalType Deserialize<TNominalType>(byte[] bytes, Action<BsonDeserializationContext.Builder> configurator = null)
        {
            using (var buffer = new ByteArrayBuffer(bytes, isReadOnly: true))
            using (var stream = new ByteBufferStream(buffer))
            {
                return Deserialize<TNominalType>(stream, configurator);
            }
        }

        /// <summary>
        /// Deserializes an object from a BSON Stream.
        /// </summary>
        /// <typeparam name="TNominalType">The nominal type of the object.</typeparam>
        /// <param name="stream">The BSON Stream.</param>
        /// <param name="configurator">The configurator.</param>
        /// <returns>A deserialized value.</returns>
        public static TNominalType Deserialize<TNominalType>(Stream stream, Action<BsonDeserializationContext.Builder> configurator = null)
        {
            using (var bsonReader = new BsonBinaryReader(stream))
            {
                return Deserialize<TNominalType>(bsonReader, configurator);
            }
        }

        /// <summary>
        /// Deserializes an object from a JSON string.
        /// </summary>
        /// <typeparam name="TNominalType">The nominal type of the object.</typeparam>
        /// <param name="json">The JSON string.</param>
        /// <param name="configurator">The configurator.</param>
        /// <returns>A deserialized value.</returns>
        public static TNominalType Deserialize<TNominalType>(string json, Action<BsonDeserializationContext.Builder> configurator = null)
        {
            using (var bsonReader = new JsonReader(json))
            {
                return Deserialize<TNominalType>(bsonReader, configurator);
            }
        }

        /// <summary>
        /// Deserializes an object from a JSON TextReader.
        /// </summary>
        /// <typeparam name="TNominalType">The nominal type of the object.</typeparam>
        /// <param name="textReader">The JSON TextReader.</param>
        /// <param name="configurator">The configurator.</param>
        /// <returns>A deserialized value.</returns>
        public static TNominalType Deserialize<TNominalType>(TextReader textReader, Action<BsonDeserializationContext.Builder> configurator = null)
        {
            using (var bsonReader = new JsonReader(textReader))
            {
                return Deserialize<TNominalType>(bsonReader, configurator);
            }
        }

        /// <summary>
        /// Deserializes an object from a BsonDocument.
        /// </summary>
        /// <param name="document">The BsonDocument.</param>
        /// <param name="nominalType">The nominal type of the object.</param>
        /// <param name="configurator">The configurator.</param>
        /// <returns>A deserialized value.</returns>
        public static object Deserialize(BsonDocument document, Type nominalType, Action<BsonDeserializationContext.Builder> configurator = null)
        {
            using (var bsonReader = new BsonDocumentReader(document))
            {
                return Deserialize(bsonReader, nominalType, configurator);
            }
        }

        /// <summary>
        /// Deserializes a value.
        /// </summary>
        /// <param name="bsonReader">The BsonReader.</param>
        /// <param name="nominalType">The nominal type of the object.</param>
        /// <param name="configurator">The configurator.</param>
        /// <returns>A deserialized value.</returns>
        public static object Deserialize(IBsonReader bsonReader, Type nominalType, Action<BsonDeserializationContext.Builder> configurator = null)
        {
            var serializer = LookupSerializer(nominalType);
            var context = BsonDeserializationContext.CreateRoot(bsonReader, configurator);
            return serializer.Deserialize(context);
        }

        /// <summary>
        /// Deserializes an object from a BSON byte array.
        /// </summary>
        /// <param name="bytes">The BSON byte array.</param>
        /// <param name="nominalType">The nominal type of the object.</param>
        /// <param name="configurator">The configurator.</param>
        /// <returns>A deserialized value.</returns>
        public static object Deserialize(byte[] bytes, Type nominalType, Action<BsonDeserializationContext.Builder> configurator = null)
        {
            using (var buffer = new ByteArrayBuffer(bytes, isReadOnly: true))
            using (var stream = new ByteBufferStream(buffer))
            {
                return Deserialize(stream, nominalType, configurator);
            }
        }

        /// <summary>
        /// Deserializes an object from a BSON Stream.
        /// </summary>
        /// <param name="stream">The BSON Stream.</param>
        /// <param name="nominalType">The nominal type of the object.</param>
        /// <param name="configurator">The configurator.</param>
        /// <returns>A deserialized value.</returns>
        public static object Deserialize(Stream stream, Type nominalType, Action<BsonDeserializationContext.Builder> configurator = null)
        {
            using (var bsonReader = new BsonBinaryReader(stream))
            {
                return Deserialize(bsonReader, nominalType, configurator);
            }
        }

        /// <summary>
        /// Deserializes an object from a JSON string.
        /// </summary>
        /// <param name="json">The JSON string.</param>
        /// <param name="nominalType">The nominal type of the object.</param>
        /// <param name="configurator">The configurator.</param>
        /// <returns>A deserialized value.</returns>
        public static object Deserialize(string json, Type nominalType, Action<BsonDeserializationContext.Builder> configurator = null)
        {
            using (var bsonReader = new JsonReader(json))
            {
                return Deserialize(bsonReader, nominalType, configurator);
            }
        }

        /// <summary>
        /// Deserializes an object from a JSON TextReader.
        /// </summary>
        /// <param name="textReader">The JSON TextReader.</param>
        /// <param name="nominalType">The nominal type of the object.</param>
        /// <param name="configurator">The configurator.</param>
        /// <returns>A deserialized value.</returns>
        public static object Deserialize(TextReader textReader, Type nominalType, Action<BsonDeserializationContext.Builder> configurator = null)
        {
            using (var bsonReader = new JsonReader(textReader))
            {
                return Deserialize(bsonReader, nominalType, configurator);
            }
        }

        /// <summary>
        /// Returns whether the given type has any discriminators registered for any of its subclasses.
        /// </summary>
        /// <param name="type">A Type.</param>
        /// <returns>True if the type is discriminated.</returns>
        public static bool IsTypeDiscriminated(Type type)
        {
            var typeInfo = type.GetTypeInfo();
            return typeInfo.IsInterface || __discriminatedTypes.ContainsKey(type);
        }

        /// <summary>
        /// Looks up the actual type of an object to be deserialized.
        /// </summary>
        /// <param name="nominalType">The nominal type of the object.</param>
        /// <param name="discriminator">The discriminator.</param>
        /// <returns>The actual type of the object.</returns>
        public static Type LookupActualType(Type nominalType, BsonValue discriminator)
        {
            if (discriminator == null)
            {
                return nominalType;
            }

            // note: EnsureKnownTypesAreRegistered handles its own locking so call from outside any lock
            EnsureKnownTypesAreRegistered(nominalType);

            var nominalTypeInfo = nominalType.GetTypeInfo();
            Type actualType = null;

            HashSet<Type> hashSet;
            if (__discriminators.TryGetValue(discriminator, out hashSet))
            {
                foreach (var type in hashSet)
                {
                    if (nominalTypeInfo.IsAssignableFrom(type))
                    {
                        if (actualType == null)
                        {
                            actualType = type;
                        }
                        else
                        {
                            string message = string.Format("Ambiguous discriminator '{0}'.", discriminator);
                            throw new BsonSerializationException(message);
                        }
                    }
                }
            }

            if (actualType == null && discriminator.IsString)
            {
                actualType = TypeNameDiscriminator.GetActualType(discriminator.AsString); // see if it's a Type name
            }

            if (actualType == null)
            {
                string message = string.Format("Unknown discriminator value '{0}'.", discriminator);
                throw new BsonSerializationException(message);
            }

            if (!nominalTypeInfo.IsAssignableFrom(actualType))
            {
                string message = string.Format(
                    "Actual type {0} is not assignable to expected type {1}.",
                    actualType.FullName, nominalType.FullName);
                throw new BsonSerializationException(message);
            }

            return actualType;
        }

        /// <summary>
        /// Looks up the discriminator convention for a type.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns>A discriminator convention.</returns>
        public static IDiscriminatorConvention LookupDiscriminatorConvention(Type type)
        {
            var typeInfo = type.GetTypeInfo();

            var convention = __discriminatorConventions.GetOrAdd(type, t =>
            {
                IDiscriminatorConvention newConvention = null;

                if (type == typeof(object))
                {
                    // if there is no convention registered for object register the default one
                    newConvention = new ObjectDiscriminatorConvention("_t");
                    RegisterDiscriminatorConvention(typeof(object), newConvention);
                }
                else if (typeInfo.IsInterface)
                {
                    // TODO: should convention for interfaces be inherited from parent interfaces?
                    newConvention = LookupDiscriminatorConvention(typeof(object));
                    RegisterDiscriminatorConvention(type, newConvention);
                }
                else
                {
                    // inherit the discriminator convention from the closest parent (that isn't object) that has one
                    // otherwise default to the standard hierarchical convention
                    Type parentType = type.GetTypeInfo().BaseType;
                    while (newConvention == null)
                    {
                        if (parentType == typeof(object))
                        {
                            newConvention = StandardDiscriminatorConvention.Hierarchical;
                            break;
                        }
                        if (__discriminatorConventions.TryGetValue(parentType, out newConvention))
                        {
                            break;
                        }
                        parentType = parentType.GetTypeInfo().BaseType;
                    }

                    // register this convention for all types between this and the parent type where we found the convention
                    var unregisteredType = type;
                    while (unregisteredType != parentType)
                    {
                        RegisterDiscriminatorConvention(unregisteredType, newConvention);
                        unregisteredType = unregisteredType.GetTypeInfo().BaseType;
                    }
                }

                return newConvention;
            });

            return convention;
        }

        /// <summary>
        /// Looks up an IdGenerator.
        /// </summary>
        /// <param name="type">The Id type.</param>
        /// <returns>An IdGenerator for the Id type.</returns>
        public static IIdGenerator LookupIdGenerator(Type type)
        {
            var idGenerator = __idGenerators.GetOrAdd(type, t =>
            {
                IIdGenerator newGenerator = null;

                if (t.GetTypeInfo().IsValueType && __useZeroIdChecker)
                {
                    var iEquatableDefinition = typeof(IEquatable<>);
                    var iEquatableType = iEquatableDefinition.MakeGenericType(type);
                    if (iEquatableType.GetTypeInfo().IsAssignableFrom(type))
                    {
                        var zeroIdCheckerDefinition = typeof(ZeroIdChecker<>);
                        var zeroIdCheckerType = zeroIdCheckerDefinition.MakeGenericType(type);
                        newGenerator = (IIdGenerator)Activator.CreateInstance(zeroIdCheckerType);
                    }
                }
                else if (__useNullIdChecker)
                {
                    newGenerator = NullIdChecker.Instance;
                }

                // remember it even if it's null
                return newGenerator;
            });

            return idGenerator;
        }

        /// <summary>
        /// Looks up a serializer for a Type.
        /// </summary>
        /// <typeparam name="T">The type.</typeparam>
        /// <returns>A serializer for type T.</returns>
        public static IBsonSerializer<T> LookupSerializer<T>()
        {
            return (IBsonSerializer<T>)LookupSerializer(typeof(T));
        }

        /// <summary>
        /// Looks up a serializer for a Type.
        /// </summary>
        /// <param name="type">The Type.</param>
        /// <returns>A serializer for the Type.</returns>
        public static IBsonSerializer LookupSerializer(Type type)
        {
            return __serializerRegistry.GetSerializer(type);
        }

        /// <summary>
        /// Registers the discriminator for a type.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <param name="discriminator">The discriminator.</param>
        public static void RegisterDiscriminator(Type type, BsonValue discriminator)
        {
            var typeInfo = type.GetTypeInfo();
            if (typeInfo.IsInterface)
            {
                var message = string.Format("Discriminators can only be registered for classes, not for interface {0}.", type.FullName);
                throw new BsonSerializationException(message);
            }

            var hashSet = __discriminators.GetOrAdd(discriminator, d => new HashSet<Type>());

            if (hashSet.Add(type))
            {
                // mark all base types as discriminated (so we know that it's worth reading a discriminator)
                for (var baseType = typeInfo.BaseType; baseType != null; baseType = baseType.GetTypeInfo().BaseType)
                {
                    __discriminatedTypes.AddOrUpdate(baseType, baseType, (t, b) => t);
                }
            }
        }

        /// <summary>
        /// Registers the discriminator convention for a type.
        /// </summary>
        /// <param name="type">Type type.</param>
        /// <param name="convention">The discriminator convention.</param>
        public static void RegisterDiscriminatorConvention(Type type, IDiscriminatorConvention convention)
        {
            var conventionExisting = __discriminatorConventions.GetOrAdd(type, convention);

            if (conventionExisting != convention)
            {
                var message = string.Format("There is already a discriminator convention registered for type {0}.", type.FullName);
                throw new BsonSerializationException(message);
            }
        }

        /// <summary>
        /// Registers a generic serializer definition for a generic type.
        /// </summary>
        /// <param name="genericTypeDefinition">The generic type.</param>
        /// <param name="genericSerializerDefinition">The generic serializer definition.</param>
        public static void RegisterGenericSerializerDefinition(
            Type genericTypeDefinition,
            Type genericSerializerDefinition)
        {
            __typeMappingSerializationProvider.RegisterMapping(genericTypeDefinition, genericSerializerDefinition);
        }

        /// <summary>
        /// Registers an IdGenerator for an Id Type.
        /// </summary>
        /// <param name="type">The Id Type.</param>
        /// <param name="idGenerator">The IdGenerator for the Id Type.</param>
        public static void RegisterIdGenerator(Type type, IIdGenerator idGenerator)
        {
            __idGenerators[type] = idGenerator;
        }

        /// <summary>
        /// Registers a serialization provider.
        /// </summary>
        /// <param name="provider">The serialization provider.</param>
        public static void RegisterSerializationProvider(IBsonSerializationProvider provider)
        {
            __serializerRegistry.RegisterSerializationProvider(provider);
        }

        /// <summary>
        /// Registers a serializer for a type.
        /// </summary>
        /// <typeparam name="T">The type.</typeparam>
        /// <param name="serializer">The serializer.</param>
        public static void RegisterSerializer<T>(IBsonSerializer<T> serializer)
        {
            RegisterSerializer(typeof(T), serializer);
        }

        /// <summary>
        /// Registers a serializer for a type.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <param name="serializer">The serializer.</param>
        public static void RegisterSerializer(Type type, IBsonSerializer serializer)
        {
            __serializerRegistry.RegisterSerializer(type, serializer);
        }

        /// <summary>
        /// Serializes a value.
        /// </summary>
        /// <typeparam name="TNominalType">The nominal type of the object.</typeparam>
        /// <param name="bsonWriter">The BsonWriter.</param>
        /// <param name="value">The object.</param>
        /// <param name="configurator">The serialization context configurator.</param>
        /// <param name="args">The serialization args.</param>
        public static void Serialize<TNominalType>(
            IBsonWriter bsonWriter,
            TNominalType value,
            Action<BsonSerializationContext.Builder> configurator = null,
            BsonSerializationArgs args = default(BsonSerializationArgs))
        {
            args.SetOrValidateNominalType(typeof(TNominalType), "<TNominalType>");
            var serializer = LookupSerializer<TNominalType>();
            var context = BsonSerializationContext.CreateRoot(bsonWriter, configurator);
            serializer.Serialize(context, args, value);
        }

        /// <summary>
        /// Serializes a value.
        /// </summary>
        /// <param name="bsonWriter">The BsonWriter.</param>
        /// <param name="nominalType">The nominal type of the object.</param>
        /// <param name="value">The object.</param>
        /// <param name="configurator">The serialization context configurator.</param>
        /// <param name="args">The serialization args.</param>
        public static void Serialize(
            IBsonWriter bsonWriter,
            Type nominalType,
            object value,
            Action<BsonSerializationContext.Builder> configurator = null,
            BsonSerializationArgs args = default(BsonSerializationArgs))
        {
            args.SetOrValidateNominalType(nominalType, "nominalType");
            var serializer = LookupSerializer(nominalType);
            var context = BsonSerializationContext.CreateRoot(bsonWriter, configurator);
            serializer.Serialize(context, args, value);
        }

        // internal static methods
        internal static void EnsureKnownTypesAreRegistered(Type nominalType)
        {
            __typesWithRegisteredKnownTypes.GetOrAdd(nominalType, type =>
           {
                // only call LookupClassMap for classes with a BsonKnownTypesAttribute
#if NET452
                var knownTypesAttribute = nominalType.GetTypeInfo().GetCustomAttributes(typeof(BsonKnownTypesAttribute), false);
#else
                var knownTypesAttribute = nominalType.GetTypeInfo().GetCustomAttributes(typeof(BsonKnownTypesAttribute), false).ToArray();
#endif
                if (knownTypesAttribute?.Length > 0)
               {
                    // try and force a scan of the known types
                    LookupSerializer(type);
               }
               return type;
           });
        }

        // private static methods
        private static void RegisterSerializationProviders()
        {
            // order matters. It's in reverse order of how they'll get consumed
            __serializerRegistry.RegisterSerializationProvider(new BsonClassMapSerializationProvider());
            __serializerRegistry.RegisterSerializationProvider(new DiscriminatedInterfaceSerializationProvider());
            __serializerRegistry.RegisterSerializationProvider(new CollectionsSerializationProvider());
            __serializerRegistry.RegisterSerializationProvider(new PrimitiveSerializationProvider());
            __serializerRegistry.RegisterSerializationProvider(new AttributedSerializationProvider());
            __serializerRegistry.RegisterSerializationProvider(__typeMappingSerializationProvider);
            __serializerRegistry.RegisterSerializationProvider(new BsonObjectModelSerializationProvider());
        }

        private static void RegisterIdGenerators()
        {
            RegisterIdGenerator(typeof(BsonObjectId), BsonObjectIdGenerator.Instance);
            RegisterIdGenerator(typeof(Guid), GuidGenerator.Instance);
            RegisterIdGenerator(typeof(ObjectId), ObjectIdGenerator.Instance);
        }
    }
}
