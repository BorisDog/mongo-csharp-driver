using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace Benchmark
{
    internal static class ReadBenchmark
    {
        private static readonly Random s_random = new Random();

        static ReadBenchmark()
        {
            BsonSerializer.RegisterSerializer(new CustomSerializer<PersonCopy>(true));
            BsonSerializer.RegisterSerializer(new CustomSerializer<PatientDataCopy>(true));
            BsonSerializer.RegisterSerializer(new CustomSerializer<PersonCopyNoId>(false));
            BsonSerializer.RegisterSerializer(new CustomSerializer<PatientDataCopyNoId>(false));
        }

        public static void AddFlatData()
        {
            Console.WriteLine("Adding data...");

            var dbClient = new MongoClient();
            var db = dbClient.GetDatabase("benchmark");
            db.DropCollection("PatientData");
            var collection = db.GetCollection<PatientData>("PatientData");

            var data = Enumerable.Range(0, 10000).Select(i => new PatientData()
            {
                Age = s_random.Next(0, 200),
                Comments = RandomString(128),
                LastName = RandomString(24),
                Name = RandomString(24),
                PhoneNumber = RandomString(24),
                Temperature = 100 * s_random.NextDouble(),
                Weight = 100 * s_random.NextDouble()
            });

            collection.InsertMany(data);

            Console.WriteLine("Data added");
        }

        public static void AddNestedData()
        {
            Console.WriteLine("Adding persons data...");

            var dbClient = new MongoClient();
            var db = dbClient.GetDatabase("benchmark");
            db.DropCollection("PersonsData");
            var collection = db.GetCollection<Person>("PersonsData");

            var persons = GetPersonsRandom(1000);
            collection.InsertMany(persons);

            Console.WriteLine($"Data added");
        }

        public static void FlatDataBenchmark(int iterations, int threadsCount, bool onlyOptimized, List<string> results, bool readKey = true)
        {
            const int totalCount = 10000;
            const int chunkSize = 1000;
            const int maxSkip = totalCount - chunkSize;
            int mixedIterations = 2;

            var dbClient = new MongoClient();
            var db = dbClient.GetDatabase("benchmark");
            var collectionOpt = db.GetCollection<PatientDataCopy>("PatientData");
            var collectionReg = db.GetCollection<PatientData>("PatientData");

            Console.WriteLine("Warming up...");

            // warm up
            for (int i = 0; i < 10; i++)
            {
                var data1 = collectionOpt.Find(FilterDefinition<PatientDataCopy>.Empty).Skip(s_random.Next(0, maxSkip)).Limit(s_random.Next(chunkSize)).ToList();
                var data2 = collectionReg.Find(FilterDefinition<PatientData>.Empty).Skip(s_random.Next(0, maxSkip)).Limit(s_random.Next(chunkSize)).ToList();
                //var dataBson = collectionBson.Find(FilterDefinition<BsonDocument>.Empty).Skip(s_random.Next(0, maxSkip)).Limit(s_random.Next(chunkSize)).ToList();
            }

            Console.WriteLine($"Press any key to start the benchmark: {iterations}x{mixedIterations} iterations, {threadsCount} threads");
            if (readKey)
            {
                Console.ReadKey();
            }

            Console.WriteLine("Benchmark started");

            var totalDataCount = 0;
            Stopwatch swReg = new Stopwatch(), swOpt = new Stopwatch();

            for (int i = 0; i < mixedIterations; i++)
            {
                ExectureOnNewThreads(swOpt, threadsCount, i => Run(true, i));

                if (!onlyOptimized)
                {
                    ExectureOnNewThreads(swReg, threadsCount, i => Run(false, i));
                }
            }

            var p = (int)((1.0 - swOpt.ElapsedMilliseconds / (double)swReg.ElapsedMilliseconds) * 100);
            var times = (double)swReg.ElapsedMilliseconds / swOpt.ElapsedMilliseconds;
            results.Add($"FlatCombined {iterations}x{mixedIterations} iterations {threadsCount} threads. Reg {swReg.ElapsedMilliseconds}ms, Opt {swOpt.ElapsedMilliseconds}ms, {p}% diff x{times:0.##} faster");

            void Run(bool isOpt, int threadIndex)
            {
                Console.WriteLine($"Running FlatCombined {iterations} with optimized={isOpt} thread {threadIndex}");

                BsonUtils.Optimized = isOpt;

                if (isOpt)
                {
                    for (int i = 0; i < iterations; i++)
                    {
                        var data = collectionOpt.Find(FilterDefinition<PatientDataCopy>.Empty).
                            Skip((chunkSize * i) % maxSkip).Limit(chunkSize).ToList();

                        totalDataCount += data.Count;
                    }
                }
                else
                {
                    for (int i = 0; i < iterations; i++)
                    {
                        var data = collectionReg.Find(FilterDefinition<PatientData>.Empty).
                            Skip((chunkSize * i) % maxSkip).Limit(chunkSize).ToList();

                        totalDataCount += data.Count;
                    }
                }
            }
        }

        public static void FlatBsonOnlyBenchmark(int iterations, int threadsCount, bool onlyOptimized, List<string> results, bool readKey = true)
        {
            const int totalCount = 10000;
            const int chunkSize = 1000;
            const int maxSkip = totalCount - chunkSize;
            int mixedIterations = 2;

            if (Debugger.IsAttached)
            {
                iterations = 5;
            }

            var patientData = Enumerable.Range(0, 1).Select(i => new PatientData()
            {
                Age = s_random.Next(0, 200),
                Comments = "Comments_" + RandomString(128),
                LastName = "LastName_" + RandomString(24),
                Name = "Name_" + RandomString(24),
                PhoneNumber = "Phone_" + RandomString(24),
                Temperature = 100 * s_random.NextDouble(),
                Weight = 100 * s_random.NextDouble()
            }).ToArray();

            var dataBson = patientData[0].ToBson();

            Console.WriteLine("Warming up...");

            // warm up
            for (int i = 0; i < 1000; i++)
            {
                var data = BsonSerializer.Deserialize<PatientData>(dataBson);
            }

            Console.WriteLine($"Press any key to start the benchmark: {iterations}x{mixedIterations} iterations, {threadsCount} threads");

            if (readKey)
            {
                Console.ReadKey();
            }

            Console.WriteLine("Benchmark started");

            Stopwatch swReg = new Stopwatch(), swOpt = new Stopwatch();

            for (int i = 0; i < mixedIterations; i++)
            {
                ExectureOnNewThreads(swOpt, threadsCount, i => Run(true, i));

                if (!onlyOptimized)
                {
                    ExectureOnNewThreads(swReg, threadsCount, i => Run(false, i));
                }
            }

            var p = (int)((1.0 - swOpt.ElapsedMilliseconds / (double)swReg.ElapsedMilliseconds) * 100);
            var times = (double)swReg.ElapsedMilliseconds / swOpt.ElapsedMilliseconds;
            results.Add($"FlatBson {iterations}x{mixedIterations} iterations {threadsCount} threads, Reg {swReg.ElapsedMilliseconds}ms, Opt {swOpt.ElapsedMilliseconds}ms, {p}% diff x{times:0.##} faster");
            
            void Run(bool isOpt, int threadIndex)
            {
                Console.WriteLine($"Running FlatBson {iterations} with optimized={isOpt} thread={threadIndex}");

                BsonUtils.Optimized = isOpt;
                var memStream = new MemoryStream(dataBson);

                if (isOpt)
                {
                    for (int i = 0; i < iterations; i++)
                    {
                        memStream.Position = 0;
                        //var data = BsonSerializer.Deserialize<Person>(memStream);

                        var docReader = DocumentReaderRegistry.GetReader<PatientDataCopyNoId>();
                        var data = docReader.ReadDocument(memStream);

                        //var data = BsonSerializer.Deserialize<PatientDataCopy>(dataBson);
                    }
                }
                else
                {
                    for (int i = 0; i < iterations; i++)
                    {
                        memStream.Position = 0;
                        var data = BsonSerializer.Deserialize<PatientData>(memStream);
                    }
                }
            }
        }

        public static void NestedDataBenchmark(int iterations, int threadsCount, bool onlyOptimized, List<string> results, bool readKey = true)
        {
            const int totalCount = 1000;
            const int chunkSize = 100;
            const int maxSkip = totalCount - chunkSize;
            int mixedIterations = 2;

            if (Debugger.IsAttached)
            {
                iterations = 5;
            }

            var dbClient = new MongoClient();
            var db = dbClient.GetDatabase("benchmark");
            var collectionOpt = db.GetCollection<PersonCopy>("PersonsData");
            var collectionReg = db.GetCollection<Person>("PersonsData");

            Console.WriteLine("Warming up...");

            // warm up
            for (int i = 0; i < 10; i++)
            {
                var data2 = collectionReg.Find(FilterDefinition<Person>.Empty).Skip(s_random.Next(0, maxSkip)).Limit(s_random.Next(chunkSize)).ToList();
                var data1 = collectionOpt.Find(FilterDefinition<PersonCopy>.Empty).Skip(s_random.Next(0, maxSkip)).Limit(s_random.Next(chunkSize)).ToList();
            }

            Console.WriteLine($"Press any key to start the benchmark: {iterations}x{mixedIterations} iterations, {threadsCount} threads");
            if (!Debugger.IsAttached && readKey)
            {
                Console.ReadKey();
            }

            Console.WriteLine("Benchmark started");

            var totalDataCount = 0;
            Stopwatch swReg = new Stopwatch(), swOpt = new Stopwatch();

            for (int i = 0; i < mixedIterations; i++)
            {
                ExectureOnNewThreads(swOpt, threadsCount, i => Run(true, i));

                if (!onlyOptimized)
                {
                    ExectureOnNewThreads(swReg, threadsCount, i => Run(false, i));
                }
            }

            var p = (int)((1.0 - swOpt.ElapsedMilliseconds / (double)swReg.ElapsedMilliseconds) * 100);
            var times = (double)swReg.ElapsedMilliseconds / swOpt.ElapsedMilliseconds;
            results.Add($"NestedCombined {iterations}x{mixedIterations} iterations {threadsCount} threads . Reg {swReg.ElapsedMilliseconds}ms, Opt {swOpt.ElapsedMilliseconds}ms, {p}% diff, x{times:0.##} faster");

            void Run(bool isOpt, int threadIndex)
            {
                Console.WriteLine($"Running NestedCombined {iterations} with optimized={isOpt} thread {threadIndex}");

                BsonUtils.Optimized = isOpt;
                if (isOpt)
                {
                    for (int i = 0; i < iterations; i++)
                    {
                        var data = collectionOpt.Find(FilterDefinition<PersonCopy>.Empty).
                            Skip((chunkSize * i) % maxSkip).Limit(chunkSize).ToList();

                        totalDataCount += data.Count;
                    }
                }
                else
                {
                    for (int i = 0; i < iterations; i++)
                    {
                        var data = collectionReg.Find(FilterDefinition<Person>.Empty).
                            Skip((chunkSize * i) % maxSkip).Limit(chunkSize).ToList();

                        totalDataCount += data.Count;
                    }
                }
            }
        }

        public static void NestedBsonOnlyBenchmark(int iterations, int threadsCount, bool onlyOptimized, List<string> results, bool readKey = true)
        {
            const int totalCount = 10000;
            const int chunkSize = 1000;
            const int maxSkip = totalCount - chunkSize;
            int mixedIterations = 2;

            if (Debugger.IsAttached)
            {
                iterations = 5;
            }

            var person = GetPersonsRandom(1)[0];

            var dataBson = person.ToBson();

            Console.WriteLine("Warming up...");

            // warm up
            for (int i = 0; i < 1000; i++)
            {
                var data1 = BsonSerializer.Deserialize<Person>(dataBson);
            }

            Console.WriteLine($"Press any key to start the benchmark: {iterations}x{mixedIterations} iterations, {threadsCount} threads, 1 object {dataBson.Length} bytes");
            if (!Debugger.IsAttached && readKey)
            {
                Console.ReadKey();
            }

            Console.WriteLine("Benchmark started");

            Stopwatch swReg = new Stopwatch(), swOpt = new Stopwatch();

            for (int i = 0; i < mixedIterations; i++)
            {
                ExectureOnNewThreads(swOpt, threadsCount, i => Run(true, i));

                if (!onlyOptimized)
                {
                    ExectureOnNewThreads(swReg, threadsCount, i => Run(false, i));
                }
            }

            var p = (int)((1.0 - swOpt.ElapsedMilliseconds / (double)swReg.ElapsedMilliseconds) * 100);
            var times = (double)swReg.ElapsedMilliseconds / swOpt.ElapsedMilliseconds;
            results.Add($"NestedBson {iterations}x{mixedIterations} iterations {threadsCount} threads. Reg {swReg.ElapsedMilliseconds}ms, Opt {swOpt.ElapsedMilliseconds}ms, {p}% diff, x{times:0.##} faster");

            void Run(bool isOpt, int threadIndex)
            {
                Console.WriteLine($"Running NestedBson {iterations} with optimized={isOpt}, thread {threadIndex}");

                BsonUtils.Optimized = isOpt;

                var memStream = new MemoryStream(dataBson);

                if (isOpt)
                {
                    for (int i = 0; i < iterations; i++)
                    {
                        //var data = BsonSerializer.Deserialize<PersonCopy>(dataBson);

                        memStream.Position = 0;
                        //var data = BsonSerializer.Deserialize<Person>(memStream);

                        var docReader = DocumentReaderRegistry.GetReader<PersonCopyNoId>();
                        var data = docReader.ReadDocument(memStream);
                    }
                }
                else
                {
                    for (int i = 0; i < iterations; i++)
                    {
                        memStream.Position = 0;
                        var data = BsonSerializer.Deserialize<Person>(memStream);
                        //var data = BsonSerializer.Deserialize<Person>(dataBson);
                    }
                }
            }
        }

        private static string RandomString(int length) =>
            new string(Enumerable.Range(0, length).Select(i => (char)s_random.Next(50, 90)).ToArray());

        private static Person[] GetPersonsRandom(int count)
        {
            var data = Enumerable.Range(0, count).Select(i => new Person()
            {
                Address = new Address()
                {
                    Street = "Stree_" + RandomString(32),
                    City = "City_" + RandomString(32),
                },

                Age = s_random.Next(0, 200),
                Name = "Name_" + RandomString(32),
                LastName = "LN_" + RandomString(128),
                LastName2 = "LN2_" + RandomString(128),
                LastName3 = "LN3_" + RandomString(128),
                LastName4 = "LN4_" + RandomString(128),
                LastName5 = "LN5_" + RandomString(128),
                Numbers = Enumerable.Range(0, 30).Select(i => s_random.Next()).ToArray(),
                //Numbers = Enumerable.Range(0, 30).Select(i => i).ToArray(),
                //Readings = Enumerable.Range(0, 40).Select(i => (double)i).ToArray(),
                Pets = Enumerable.Range(0, 5).Select(i => GetPet()).ToArray(),
                Properties = Enumerable.Range(0, 5).Select(i => GetProperty()).ToArray()
            });

            return data.ToArray();

            Animal GetPet()
            {
                if (s_random.NextDouble() < 0.5)
                {
                    return new Dog()
                    {
                        Info = "Info_" + RandomString(32),
                        Name = "Name_" + RandomString(32),
                        Vaccinations = "DVAC_" + RandomString(32)
                    };
                }
                else
                {
                    return new Cat()
                    {
                        Info = "Info_" + RandomString(32),
                        Name = "Name_" + RandomString(32),
                        Vaccinations = "CVAC_" + RandomString(32)
                    };
                }
            }

            REProperty GetProperty()
            {
                switch (s_random.Next(0, 8))
                {
                    case 0:
                        return new Townhouse()
                        {
                            Floors = s_random.Next(1, 3),
                            Name = "TH_" + RandomString(32),
                            Notes = "Notes_" + RandomString(32)
                        };
                    case 1:
                        return new Condo()
                        {
                            FloorNum = s_random.Next(1, 200),
                            Name = "Condo_" + RandomString(32),
                            Notes = "Notes_" + RandomString(32)
                        };
                    case 2:
                        return new CondoNested1()
                        {
                            FloorNum = s_random.Next(1, 200),
                            Name = "Condo1_" + RandomString(32),
                            Notes = "Notes_" + RandomString(32)
                        };
                    case 3:
                        return new CondoNested2()
                        {
                            FloorNum = s_random.Next(1, 200),
                            Name = "Condo2_" + RandomString(32),
                            Notes = "Notes_" + RandomString(32)
                        };
                    case 4:
                        return new CondoNested3()
                        {
                            FloorNum = s_random.Next(1, 200),
                            Name = "Condo3_" + RandomString(32),
                            Notes = "Notes_" + RandomString(32)
                        };
                    case 5:
                        return new CondoNested4()
                        {
                            FloorNum = s_random.Next(1, 200),
                            Name = "Condo4_" + RandomString(32),
                            Notes = "Notes_" + RandomString(32)
                        };
                    case 6:
                        return new CondoNested5()
                        {
                            FloorNum = s_random.Next(1, 200),
                            Name = "Condo5_" + RandomString(32),
                            Notes = "Notes_" + RandomString(32)
                        };
                    case 7:
                        return new CondoNested6()
                        {
                            FloorNum = s_random.Next(1, 200),
                            Name = "Condo6_" + RandomString(32),
                            Notes = "Notes_" + RandomString(32)
                        };
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        private static void ExectureOnNewThreads(Stopwatch sw, int threadsCount, Action<int> action)
        {
            var startEvent = new ManualResetEventSlim(false);

            var threads = Enumerable.Range(0, threadsCount).Select(i =>
            {
                var thread = new Thread(_ =>
                {
                    startEvent.Wait();
                    action(i);
                });

                thread.IsBackground = false;
                thread.Start();

                return thread;
            })
            .ToArray();

            sw.Start();
            startEvent.Set();

            foreach (var thread in threads)
            {
                thread.Join();
            }

            sw.Stop();
        }
    }
}
