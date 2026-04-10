using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DynamicDungeon.Core;
using DynamicDungeon.Core.Models;
using Xunit;
using Xunit.Abstractions;

namespace DynamicDungeon.Core.Tests
{
    /// <summary>
    /// Performance and reliability benchmarks for all three map generation algorithms.
    /// Run with: dotnet test DynamicDungeon.Core.Tests --verbosity normal
    /// CSV files are written to the test output directory (bin/Debug/net10.0/).
    /// </summary>
    public class AlgorithmBenchmarkTests
    {
        private readonly ITestOutputHelper _output;

        // Adjust these constants to trade accuracy for speed.
        private const int DefaultIterations = 30;   // per biome × difficulty combo
        private const int ScalingIterations = 20;   // per size × algorithm combo

        public AlgorithmBenchmarkTests(ITestOutputHelper output)
        {
            _output = output;
        }

        // ─── Result type ─────────────────────────────────────────────────────────

        private record BenchmarkResult(
            long   MinMs,
            long   MaxMs,
            double AvgMs,
            double MedianMs,
            double P95Ms,
            int    TotalGenerationFailures,   // runs where all MaxRegenerationAttempts were exhausted
            double AvgRetries,                // average failed-attempt count per successful run
            int    MaxRetries,                // worst-case failed-attempt count observed
            int    TotalRuns);

        // ─── Core runner ─────────────────────────────────────────────────────────

        private BenchmarkResult RunBenchmark(
            AlgorithmType  algorithm,
            BiomeType      biome,
            DifficultyLevel difficulty,
            int            width,
            int            height,
            int            iterations)
        {
            var generator      = new MapGenerator();
            var successTimes   = new List<long>(iterations);
            var successRetries = new List<int>(iterations);
            int failures       = 0;

            for (int i = 0; i < iterations; i++)
            {
                var parameters = new GenerationParameters
                {
                    Algorithm  = algorithm,
                    Biome      = biome,
                    Difficulty = difficulty,
                    Width      = width,
                    Height     = height,
                    Seed       = 1000 + i   // unique seed per run; deterministic across runs
                };

                // report is assigned by GenerateWithReport before any throw,
                // so it is safe to read even when an InvalidOperationException is caught.
                GenerationReport report = new();
                try
                {
                    generator.GenerateWithReport(parameters, out report);
                    successTimes.Add(report.TotalMs);
                    successRetries.Add(report.FailedAttempts);
                }
                catch (InvalidOperationException)
                {
                    failures++;
                }
            }

            if (successTimes.Count == 0)
            {
                // Every run failed — return zeros so the table still renders.
                return new BenchmarkResult(0, 0, 0, 0, 0, failures, 0, 0, iterations);
            }

            successTimes.Sort();
            successRetries.Sort();

            long   min    = successTimes[0];
            long   max    = successTimes[^1];
            double avg    = successTimes.Average();
            double median = successTimes.Count % 2 == 0
                ? (successTimes[successTimes.Count / 2 - 1] + successTimes[successTimes.Count / 2]) / 2.0
                : successTimes[successTimes.Count / 2];
            double p95    = successTimes[(int)(successTimes.Count * 0.95)];
            double avgRet = successRetries.Average();
            int    maxRet = successRetries.Max();

            return new BenchmarkResult(min, max, avg, median, p95, failures, avgRet, maxRet, iterations);
        }

        // ─── Output helpers ──────────────────────────────────────────────────────

        private void PrintTable(IEnumerable<(string Label, BenchmarkResult Result)> rows)
        {
            string header =
                $"{"Scenario",-38} {"Min",5} {"Max",5} {"Avg",7} {"P95",7} {"Fail",6} {"AvgRet",8}";
            _output.WriteLine(header);
            _output.WriteLine(new string('─', header.Length));

            foreach (var (label, r) in rows)
            {
                _output.WriteLine(
                    $"{label,-38} {r.MinMs,5} {r.MaxMs,5} {r.AvgMs,7:F1} {r.P95Ms,7:F1}" +
                    $" {r.TotalGenerationFailures,6} {r.AvgRetries,8:F2}");
            }
        }

        private void WriteCsv(string fileName, IEnumerable<(string Label, BenchmarkResult Result)> rows)
        {
            var path = Path.Combine(AppContext.BaseDirectory, fileName);
            using var w = new StreamWriter(path);
            w.WriteLine("Scenario,MinMs,MaxMs,AvgMs,MedianMs,P95Ms,TotalGenerationFailures,AvgRetries,MaxRetries,TotalRuns");
            foreach (var (label, r) in rows)
            {
                w.WriteLine(
                    $"{label},{r.MinMs},{r.MaxMs},{r.AvgMs:F2},{r.MedianMs:F2},{r.P95Ms:F2}," +
                    $"{r.TotalGenerationFailures},{r.AvgRetries:F2},{r.MaxRetries},{r.TotalRuns}");
            }
            _output.WriteLine($"\n  CSV → {path}");
        }

        // ─── Tests ───────────────────────────────────────────────────────────────

        [Fact]
        public void Benchmark_CellularAutomata()
        {
            var rows = new List<(string, BenchmarkResult)>();

            foreach (var biome in Enum.GetValues<BiomeType>())
            foreach (var diff  in Enum.GetValues<DifficultyLevel>())
            {
                var r = RunBenchmark(AlgorithmType.CellularAutomata, biome, diff, 50, 50, DefaultIterations);
                rows.Add(($"CA  | {biome,-10} | {diff}", r));
            }

            _output.WriteLine($"\n=== Cellular Automata — 50×50, {DefaultIterations} runs per combo ===\n");
            PrintTable(rows);
            WriteCsv("benchmark_ca.csv", rows);
        }

        [Fact]
        public void Benchmark_PerlinNoise()
        {
            var rows = new List<(string, BenchmarkResult)>();

            foreach (var biome in Enum.GetValues<BiomeType>())
            foreach (var diff  in Enum.GetValues<DifficultyLevel>())
            {
                var r = RunBenchmark(AlgorithmType.PerlinNoise, biome, diff, 50, 50, DefaultIterations);
                rows.Add(($"Perlin | {biome,-10} | {diff}", r));
            }

            _output.WriteLine($"\n=== Perlin Noise — 50×50, {DefaultIterations} runs per combo ===\n");
            PrintTable(rows);
            WriteCsv("benchmark_perlin.csv", rows);
        }

        [Fact]
        public void Benchmark_WaveFunctionCollapse()
        {
            var rows = new List<(string, BenchmarkResult)>();

            foreach (var biome in Enum.GetValues<BiomeType>())
            foreach (var diff  in Enum.GetValues<DifficultyLevel>())
            {
                var r = RunBenchmark(AlgorithmType.WaveFunctionCollapse, biome, diff, 50, 50, DefaultIterations);
                rows.Add(($"WFC | {biome,-10} | {diff}", r));
            }

            _output.WriteLine($"\n=== Wave Function Collapse — 50×50, {DefaultIterations} runs per combo ===\n");
            _output.WriteLine("  Note: WFC has an internal 50-attempt loop; outer failures are rare extremes.\n");
            PrintTable(rows);
            WriteCsv("benchmark_wfc.csv", rows);
        }

        /// <summary>
        /// Shows how generation time scales with map area.
        /// Fixed to Dungeon biome, Medium difficulty, 20 iterations per cell.
        /// </summary>
        [Fact]
        public void Benchmark_MapSizeScaling()
        {
            var rows = new List<(string, BenchmarkResult)>();
            int[] sizes = { 20, 40, 60, 80 };

            var algorithms = new (AlgorithmType Type, string Name)[]
            {
                (AlgorithmType.CellularAutomata,    "CA    "),
                (AlgorithmType.PerlinNoise,         "Perlin"),
                (AlgorithmType.WaveFunctionCollapse,"WFC   ")
            };

            foreach (var (algo, name) in algorithms)
            foreach (int  size        in sizes)
            {
                var r = RunBenchmark(algo, BiomeType.Dungeon, DifficultyLevel.Medium,
                                     size, size, ScalingIterations);
                rows.Add(($"{name} | {size,2}×{size,-2}", r));
            }

            _output.WriteLine($"\n=== Map Size Scaling — Dungeon/Medium, {ScalingIterations} runs per cell ===\n");
            PrintTable(rows);
            WriteCsv("benchmark_scaling.csv", rows);
        }
    }
}
