using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Benchmark
{
    internal class Program
    {
        static void Main(string[] args)
        {
            //ReadBenchmark.AddFlatData();
            //ReadBenchmark.AddNestedData();

            var results = new List<string>();

            if (args.Length == 1 && args[0] == "all")
            {
                var threadsCounts = new[] { 1, 2, 4, 8, 16 };

                var benchmarksAndIterations = new[]
                {
                    ("FlatBson", 2000000),
                    ("NestedBson", 1000000),
                    ("Flat", 5000),
                    ("Nested", 5000)
                };

                var allBenchmarks = from benchmarkAndIteration in benchmarksAndIterations
                                    from threadsCount in threadsCounts
                                    select new { Name = benchmarkAndIteration.Item1, Iterations = benchmarkAndIteration.Item2, ThreadsCount = threadsCount };

                results.Add($"Running {allBenchmarks.Count()} benchmarks at {DateTime.Now}");

                foreach (var benchmarkParams in allBenchmarks)
                {
                    results.Add(benchmarkParams.ToString());
                }

                foreach (var benchmarkParams in allBenchmarks)
                {
                    Run(benchmarkParams.Name, benchmarkParams.Iterations, benchmarkParams.ThreadsCount, false, false, results);

                    File.WriteAllLines("results.txt", results.ToArray());
                }
            }
            else
            {
                var threadCount = 1;
                var iterations = 500;
                var benchmark = "NestedBson";
                var onlyOptimized = false;

                if (args?.Length > 0)
                {
                    benchmark = args[0];
                    iterations = int.Parse(args[1]);
                    threadCount = int.Parse(args[2]);
                    onlyOptimized = args.Length > 3 ? bool.Parse(args[3]) : false;
                }

                Run(benchmark, iterations, threadCount, onlyOptimized, !Debugger.IsAttached, results);
            }

            foreach (var r in results)
                Console.WriteLine(r);
        }

        private static void Run(string benchmark, int iterations, int threadCount, bool onlyOptimized, bool readKey, List<string> results)
        {
            Console.WriteLine($"Running {benchmark}, {iterations} iterations, {threadCount} threads, onlyOptimized={onlyOptimized}");
            switch (benchmark)
            {
                case "Nested":
                    {
                        ReadBenchmark.NestedDataBenchmark(iterations, threadCount, onlyOptimized, results, readKey);
                        break;
                    }
                case "NestedBson":
                    {
                        ReadBenchmark.NestedBsonOnlyBenchmark(iterations, threadCount, onlyOptimized, results, readKey);
                        break;
                    }
                case "Flat":
                    {
                        ReadBenchmark.FlatDataBenchmark(iterations, threadCount, onlyOptimized, results, readKey);
                        break;
                    }
                case "FlatBson":
                    {
                        ReadBenchmark.FlatBsonOnlyBenchmark(iterations, threadCount, onlyOptimized, results, readKey);
                        break;
                    }
                default:
                    {
                        Console.WriteLine($"Unknown benchmark {benchmark}");
                        break;
                    }
            }
        }
    }
}
