using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using MongoDB.Bson;
using MongoDB.Bson.IO;

namespace Benchmark
{
    internal class PInvoke
    {
        [DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int memcmp(IntPtr b1, IntPtr b2, IntPtr count);
    }

    internal interface IDocumentReader
    {
        public object ReadDocument(Stream stream);

        public object ReadDocument(IntPtr buffer, int documentSize);
        public Array ReadDocumentArray(IntPtr buffer, int documentSize);
    }

    internal sealed class DocumentReader<T> : IDocumentReader where T : class, new()
    {
        private sealed class NestedReaderContext
        {
            public string Name { get; set; }
            public byte[] NameBytes { get; set; }

            public IDocumentReader Reader { get; set; }
        }

        private sealed class ElementContext
        {
            public string Name { get; set; }
            public byte[] NameBytes { get; set; }

            public Type UnderlyingType { get; set; }

            public Action<T, int> SetterInt32 { get; set; }
            public Action<T, long> SetterInt64 { get; set; }
            public Action<T, double> SetterDouble { get; set; }
            public Action<T, string> SetterString { get; set; }

            public Action<T, IntPtr, int> ArraySetter { get; set; }
            public Action<T, IntPtr, int> ObjectSetter { get; set; }

            public override string ToString() => Name;
        }

        [ThreadStatic]
        private static byte[] s_streamBuffer;

        [ThreadStatic]
        private static IntPtr s_buffer = IntPtr.Zero;

        [ThreadStatic]
        private static T[] s_docsArray = null;

        private const int c_descriminator = 4 | (95 << 8) | (116 << 16); // (bsonType == array and '_t')

        private readonly ElementContext[] _elementContexts;
        private readonly IDictionary<string, ElementContext> _elementContextsDictionary = new Dictionary<string, ElementContext>();

        private readonly IDictionary<string, IDocumentReader> _nestedTypeReaders = new Dictionary<string, IDocumentReader>();
        private readonly NestedReaderContext[] _nestedReaderContexts;

        public DocumentReader(bool addId)
        {
            var mainType = typeof(T);
            var derivedTypes = from t in Assembly.GetExecutingAssembly().GetTypes()
                               where t.IsSubclassOf(mainType)
                               select t;

            var nestedReaderContexts = new List<NestedReaderContext>();
            foreach (var derivedType in derivedTypes)
            {
                var reader = DocumentReaderRegistry.GetReader(derivedType, false);
                _nestedTypeReaders[derivedType.Name] = reader;

                nestedReaderContexts.Add(new NestedReaderContext()
                {
                    Name = derivedType.Name,
                    NameBytes = Utf8Encodings.Strict.GetBytes(derivedType.Name),
                    Reader = reader
                });
            };

            _nestedReaderContexts = nestedReaderContexts.ToArray();

            var elementContexts = new List<ElementContext>();
            var genericActionType = typeof(Action<,>);

            if (addId)
            {
                elementContexts.Add(new ElementContext()
                {
                    Name = "_id",
                    NameBytes = Utf8Encodings.Strict.GetBytes("_id"),
                    UnderlyingType = null
                });
            }

            IEnumerable<PropertyInfo> GetProperties(Type type)
            {
                if (type != typeof(object))
                {

                    if (type.BaseType != typeof(object))
                    {
                        foreach (var p in GetProperties(type.BaseType))
                            yield return p;
                    }

                    foreach (var p in type.GetMembers().OfType<PropertyInfo>().Where(p => p.DeclaringType == type))
                        yield return p;
                }
            }

            var allprops = GetProperties(mainType).ToArray();

            foreach (var propInfo in GetProperties(mainType))
            {
                Delegate propDelegate = null;
                Type propType = propInfo.PropertyType;
                var underlyingType = propType;
                Action<T, IntPtr, int> arraySetter = null;
                Action<T, IntPtr, int> objectSetter = null;
                Action<T, int> setterInt32 = null;
                Action<T, long> setterInt64 = null;
                Action<T, double> setterDouble = null;
                Action<T, string> setterString = null;

                if (IsCustomType(propType))
                {
                    var propReader = DocumentReaderRegistry.GetReader(propType, false);

                    var tParameter = Expression.Parameter(typeof(T), "obj");
                    var valueParameter = Expression.Parameter(typeof(object), "value");

                    var lambdaExpression = Expression.Lambda<Action<T, object>>(
                        Expression.Call(
                            Expression.Convert(tParameter, propInfo.DeclaringType),
                            propInfo.SetMethod,
                            Expression.Convert(valueParameter, propType)
                        ),
                        tParameter,
                        valueParameter
                    );

                    var setter = lambdaExpression.Compile();

                    objectSetter = (t, p, s) =>
                    {
                        var obj = propReader.ReadDocument(p, s);
                        setter(t, obj);
                    };
                }
                else if (propType.IsArray)
                {
                    underlyingType = propType.GetElementType();

                    if (IsCustomType(underlyingType))
                    {
                        var tParameter = Expression.Parameter(typeof(T), "obj");
                        var valueParameter = Expression.Parameter(typeof(Array), "array");

                        var lambdaExpression = Expression.Lambda<Action<T, Array>>(
                            Expression.Call(
                                Expression.Convert(tParameter, propInfo.DeclaringType),
                                propInfo.SetMethod,
                                Expression.Convert(valueParameter, propInfo.PropertyType)
                            ),
                            tParameter,
                            valueParameter
                        );

                        var propSetter = lambdaExpression.Compile();
                        var typeReader = DocumentReaderRegistry.GetReader(underlyingType, false);

                        arraySetter = (t, p, s) =>
                        {
                            var array = typeReader.ReadDocumentArray(p, s);
                            propSetter(t, array);
                        };
                    }
                    else if (underlyingType == typeof(int))
                    {
                        var setterDelegateType = genericActionType.MakeGenericType(typeof(T), typeof(int[]));
                        var setter = (Action<T, int[]>)propInfo.SetMethod.CreateDelegate(setterDelegateType);

                        arraySetter = (t, p, s) =>
                        {
                            var array = ReadArrayInt32(p, s);
                            setter(t, array);
                        };
                    }
                    else if (underlyingType == typeof(double))
                    {
                        var setterDelegateType = genericActionType.MakeGenericType(typeof(T), typeof(double[]));
                        var setter = (Action<T, double[]>)propInfo.SetMethod.CreateDelegate(setterDelegateType);

                        arraySetter = (t, p, s) =>
                        {
                            var array = ReadArrayDouble(p, s);
                            setter(t, array);
                        };
                    }
                }
                else
                {
                    var setterDelegateType = genericActionType.MakeGenericType(typeof(T), propType);
                    propDelegate = propInfo.SetMethod.CreateDelegate(setterDelegateType);

                    if (propType == typeof(int))
                    {
                        setterInt32 = (Action<T, int>)propDelegate;
                    }
                    else if (propType == typeof(long))
                    {
                        setterInt64 = (Action<T, long>)propDelegate;
                    }
                    else if (propType == typeof(double))
                    {
                        setterDouble = (Action<T, double>)propDelegate;
                    }
                    else if (propType == typeof(string))
                    {
                        setterString = (Action<T, string>)propDelegate;
                    }
                }

                var context = new ElementContext()
                {
                    Name = propInfo.Name,
                    NameBytes = Utf8Encodings.Strict.GetBytes(propInfo.Name),
                    SetterDouble = setterDouble,
                    SetterInt32 = setterInt32,
                    SetterInt64 = setterInt64,
                    SetterString = setterString,
                    UnderlyingType = underlyingType,
                    ArraySetter = arraySetter,
                    ObjectSetter = objectSetter
                };

                elementContexts.Add(context);
            }

            _elementContexts = elementContexts.ToArray();
        }

        private static bool IsCustomType(Type type) =>
            type.IsClass && !type.IsArray && type != typeof(string);

        public unsafe object ReadDocument(Stream stream)
        {
            if (s_streamBuffer == null)
            {
                s_streamBuffer = new byte[1024 * 1024 * 10];
            }

            stream.Read(s_streamBuffer, 0, (int)stream.Length);

            fixed (byte* pBuffer = &s_streamBuffer[0])
            {
                var docSize = *(int*)pBuffer - 4 - 1;
                var result = ReadDocument(new IntPtr(pBuffer + 4), docSize);
                return result;
            }
        }

        public unsafe object ReadDocument(IntPtr buffer, int documentSize)
        {
            var pBuffer = (byte*)buffer;
            T result = new T();

            int propIndex = 0;
            var pMax = pBuffer + documentSize;

            while (pBuffer < pMax)
            {
                var bsonType = (BsonType)(*pBuffer++);

                ElementContext elementContext = null;

                // read name and fetch it's context
                if (propIndex >= 0)
                {
                    var nextContext = _elementContexts[propIndex++];
                    var compareLength = nextContext.NameBytes.Length;

                    if (pBuffer[compareLength] == 0 &&
                        AreEqual(nextContext.NameBytes, pBuffer, compareLength))
                    {
                        pBuffer += compareLength + 1;
                        elementContext = nextContext;
                    }
                    else
                    {
                        propIndex = -1;
                    }
                }

                // if next context does not match to current name, fetch it from dictionary, todo optimize
                if (propIndex == -1)
                {
                    var pNameStart = pBuffer;
                    var b = *pBuffer;
                    while (b != 0)
                    {
                        b = *pBuffer++;
                    }

                    var name = Utf8Encodings.Strict.GetString(pNameStart, (int)(pBuffer - pNameStart) - 1);
                    elementContext = _elementContextsDictionary[name];
                }

                switch (bsonType)
                {
                    case BsonType.Array:
                        {
                            var size = *(int*)pBuffer;

                            elementContext.ArraySetter(result, new IntPtr(pBuffer + 4), size - 5);
                            pBuffer += size;

                            break;
                        }
                    case BsonType.Document:
                        {
                            var size = *(int*)pBuffer;

                            elementContext.ObjectSetter(result, new IntPtr(pBuffer + 4), size - 5);
                            pBuffer += size;

                            break;
                        }
                    case BsonType.ObjectId:
                        {
                            pBuffer += 12;
                            break;
                        }
                    case BsonType.String:
                        {
                            var length = *(int*)pBuffer;
                            pBuffer += 4;
                          
                            var value = Utf8Encodings.Strict.GetString(pBuffer, length - 1);
                            pBuffer += length;

                            if (elementContext != null)
                                elementContext.SetterString(result, value);
                            break;
                        }
                    case BsonType.Int32:
                        {
                            var value = *(int*)pBuffer;
                            pBuffer += 4;

                            if (elementContext != null)
                                elementContext.SetterInt32(result, value);
                            break;
                        }
                    case BsonType.Int64:
                        {
                            var value = *(long*)pBuffer;
                            pBuffer += 8;

                            if (elementContext != null)
                                elementContext.SetterInt64(result, value);
                            break;
                        }
                    case BsonType.Double:
                        {
                            var value = *(double*)pBuffer;
                            pBuffer += 8;

                            if (elementContext != null)
                                elementContext.SetterDouble(result, value);
                            break;
                        }
                }
            }

            return result;
        }

        public unsafe Array ReadDocumentArray(IntPtr buffer, int arraySize)
        {
            if (s_docsArray == null)
            {
                s_docsArray = new T[1024];
            }

            var pBuffer = (byte*)buffer;
            var bsonType = (BsonType)(*pBuffer++);

            var size = 0;

            while (bsonType != BsonType.EndOfDocument)
            {
                while (*pBuffer++ != 0) { }

                var documentSize = *(int*)pBuffer;
                pBuffer += 4;

                T document;
                var typeAndDescriminator = *(int*)pBuffer;
                if (typeAndDescriminator == c_descriminator)
                {
                    var pDocBuffer = pBuffer + 4;
                    var descriminatorArraySize = *(int*)pDocBuffer;
                    var typeReader = ReadDescriminator(new IntPtr(pDocBuffer + 4), descriminatorArraySize - 5);

                    document = (T)typeReader.ReadDocument(new IntPtr(pDocBuffer + descriminatorArraySize), documentSize - (descriminatorArraySize + 9));
                }
                else
                {
                    document = ReadDocument(new IntPtr(pBuffer), documentSize) as T;
                }

                s_docsArray[size++] = document;

                pBuffer += documentSize - 4;
                bsonType = (BsonType)(*pBuffer++);
            }

            var result = new T[size];
            Array.Copy(s_docsArray, result, size);

            return result;
        }

        private static unsafe int[] ReadArrayInt32(IntPtr pBuffer, int arraySizeBytes)
        {
            if (s_buffer == IntPtr.Zero)
            {
                s_buffer = Marshal.AllocHGlobal(1024 * 1024 * 10);
            }

            var pDst = (int*)s_buffer.ToPointer();
            var pSrc = (byte*)pBuffer + 1; // Skip type, todo validate
            var pMax = pSrc + arraySizeBytes;

            while (pSrc < pMax)
            {
                while (*pSrc++ != 0) { }

                *pDst = *(int*)pSrc;
                pSrc += 5; // 4 int bytes + type byte
                pDst++;
            }

            var sizeInBytes = (long)pDst - s_buffer.ToInt64();

            var result = new int[sizeInBytes >> 2];
            fixed (int* pResult = &result[0])
            {
                Buffer.MemoryCopy(s_buffer.ToPointer(), pResult, sizeInBytes, sizeInBytes);
            }

            return result;
        }

        private static unsafe double[] ReadArrayDouble(IntPtr pBuffer, int arraySizeBytes)
        {
            if (s_buffer == IntPtr.Zero)
            {
                s_buffer = Marshal.AllocHGlobal(1024 * 1024 * 10);
            }

            var pDst = (double*)s_buffer.ToPointer();
            var pSrc = (byte*)pBuffer + 1; // Skip type, todo validate
            var pMax = pSrc + arraySizeBytes;
            var size = 0;

            while (pSrc < pMax)
            {
                while (*pSrc++ != 0) { }

                *pDst = *(double*)pSrc;
                pSrc += 9; // 8 double bytes + type byte
                pDst++;

                size++;
            }

            var result = new double[size];
            fixed (double* pResult = &result[0])
            {
                Buffer.MemoryCopy(s_buffer.ToPointer(), pResult, size * 8, size * 8);
            }

            return result;
        }

        private unsafe IDocumentReader ReadDescriminator(IntPtr pBuffer, int arraySizeBytes)
        {
            var pSrc = (byte*)pBuffer + 1; // Skip type, todo validate
            var pMax = pSrc + arraySizeBytes;

            while (pSrc < pMax)
            {
                while (*pSrc++ != 0) { }
                var stringSize = *(int*)pSrc;
                pSrc += 4;
                var nextP = pSrc + stringSize + 1; // 4 size byte + string value + type byte

                if (nextP >= pMax)
                {
                    var stringBytesCount = stringSize - 1;
                    foreach (var context in _nestedReaderContexts)
                    {
                        if (context.NameBytes.Length == stringBytesCount &&
                            AreEqual(context.NameBytes, pSrc, stringBytesCount))
                        {
                            return context.Reader;
                        }
                    }
                }

                pSrc = nextP;
            }

            return null;
        }

        private static unsafe string[] ReadArrayString(IntPtr pBuffer, int arraySizeBytes)
        {
            if (s_buffer == IntPtr.Zero)
            {
                s_buffer = Marshal.AllocHGlobal(1024 * 1024 * 10);
            }

            var result = new List<string>();
            var pSrc = (byte*)pBuffer + 1; // Skip type, todo validate
            var pMax = pSrc + arraySizeBytes;

            while (pSrc < pMax)
            {
                while (*pSrc++ != 0) { }
                var stringSize = *(int*)pSrc;

                var value = Utf8Encodings.Strict.GetString(pSrc + 4, stringSize - 1);

                pSrc += 4 + stringSize + 1; // 4 size byte + string value + type byte
                result.Add(value);
            }

            return result.ToArray();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe bool AreEqual(byte[] pBuffer1, byte* pBuffer2, int length)
        {
            for (int i = 0; i < length; i++)
            {
                if (pBuffer1[i] != *pBuffer2++)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
