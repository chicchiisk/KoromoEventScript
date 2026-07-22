using System;
using System.Diagnostics;
using System.Linq;
using KoromoEventScript.Runtime.Core.Execution;
using KoromoEventScript.Runtime.Core.Klib;
using KoromoEventScript.Unity;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

internal static class KesPrimeSieveBenchmark
{
    private const int Candidate = 104707;
    private const int WarmupCount = 3;
    private const int MeasurementCount = 100;
    private const int MaxInstructionCount = 20_000_000;
    private const string KlibAssetPath = "Assets/Benchmarks/prime_sieve.klib";
    private const string ResultVariableName = "is_prime";
    private static BenchmarkRun currentRun;

    [MenuItem("Tools/KoromoEventScript/Run Prime Sieve Benchmark")]
    public static void Run()
    {
        if (currentRun != null)
        {
            throw new InvalidOperationException("Prime sieve benchmark is already running.");
        }

        var asset = AssetDatabase.LoadAssetAtPath<KesKlibAsset>(KlibAssetPath);
        if (asset == null)
        {
            throw new InvalidOperationException($"KES benchmark asset was not found: {KlibAssetPath}");
        }

        var loadResult = asset.LoadModule(KlibAssetPath);
        if (!loadResult.Succeeded || loadResult.Document == null)
        {
            throw new InvalidOperationException(
                "KES benchmark module could not be loaded: " +
                string.Join("; ", loadResult.Diagnostics.Select(diagnostic => diagnostic.Message)));
        }

        var document = loadResult.Document;
        var resultSlot = FindVariableSlot(document, ResultVariableName);
        var counters = new KesVmPerformanceCounters();
        var executor = new KesVmExecutor(performanceCounters: counters);

        for (var index = 0; index < WarmupCount; index++)
        {
            EnsurePrime(IsPrimeCSharp(Candidate), "C# warmup");
            EnsurePrime(RunKes(document, executor, resultSlot), "KES warmup");
        }

        counters.Reset();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        currentRun = new BenchmarkRun(document, executor, counters, resultSlot);
        EditorApplication.update += MeasureNextPair;
        Debug.Log($"[KES PRIME BENCHMARK] Started {MeasurementCount} measured runs for C# and KES.");
    }

    private static void MeasureNextPair()
    {
        try
        {
            var index = currentRun.CompletedCount;
            currentRun.CSharpTimings[index] = MeasureMilliseconds(
                () => EnsurePrime(IsPrimeCSharp(Candidate), $"C# iteration {index + 1}"));
            currentRun.KesTimings[index] = MeasureMilliseconds(
                () => EnsurePrime(
                    RunKes(currentRun.Document, currentRun.Executor, currentRun.ResultSlot),
                    $"KES iteration {index + 1}"));
            currentRun.CompletedCount++;
            if (currentRun.CompletedCount < MeasurementCount)
            {
                return;
            }

            var csharp = Statistics.Create(currentRun.CSharpTimings);
            var kes = Statistics.Create(currentRun.KesTimings);
            var performance = currentRun.Counters.CaptureSnapshot();
            EditorApplication.update -= MeasureNextPair;
            currentRun = null;
            LogResults(csharp, kes, performance);
        }
        catch (Exception exception)
        {
            EditorApplication.update -= MeasureNextPair;
            currentRun = null;
            Debug.LogException(exception);
        }
    }

    private static void LogResults(
        Statistics csharp,
        Statistics kes,
        KesVmPerformanceSnapshot performance)
    {
        Debug.Log(
            "[KES PRIME BENCHMARK]\n" +
            $"Candidate: {Candidate} (prime: true)\n" +
            $"Warmup: {WarmupCount}, measured runs: {MeasurementCount} each, alternating C# then KES\n" +
            $"CSharp: {csharp}\n" +
            $"KES: {kes}\n" +
            $"KES VM: runs={performance.RunInvocations:N0}, " +
            $"instructions={performance.TotalInstructions:N0}, " +
            $"instructions/sec={performance.TotalInstructions / (kes.TotalMilliseconds / 1000d):N0}, " +
            $"max stack depth={performance.MaximumObservedOperandStackDepth:N0}\n" +
            $"KES/CSharp total time ratio: {kes.TotalMilliseconds / csharp.TotalMilliseconds:F2}x");
    }

    internal static bool IsPrimeCSharp(int candidate)
    {
        if (candidate < 2)
        {
            return false;
        }

        var sieve = new bool[candidate + 1];
        Array.Fill(sieve, true);
        sieve[0] = false;
        sieve[1] = false;
        for (var factor = 2; factor * factor <= candidate; factor++)
        {
            if (!sieve[factor])
            {
                continue;
            }

            for (var multiple = factor * factor; multiple <= candidate; multiple += factor)
            {
                sieve[multiple] = false;
            }
        }

        return sieve[candidate];
    }

    private static bool RunKes(KlibDocument document, KesVmExecutor executor, int resultSlot)
    {
        var session = new KesVmSession(document);
        var executionResult = executor.Run(session, MaxInstructionCount);
        if (!executionResult.Succeeded)
        {
            throw new InvalidOperationException(
                "KES benchmark execution failed: " +
                string.Join("; ", executionResult.Diagnostics.Select(diagnostic => diagnostic.Message)));
        }

        if (!session.Variables.TryGetValue(resultSlot, out var result) ||
            result.Kind != RuntimeValueKind.Bool ||
            result.BoolValue is null)
        {
            throw new InvalidOperationException($"KES result variable '{ResultVariableName}' was not a bool.");
        }

        return result.BoolValue.Value;
    }

    private static int FindVariableSlot(KlibDocument document, string variableName)
    {
        for (var index = 0; index < document.Variables.Count; index++)
        {
            var nameIndex = document.Variables[index].NameIndex;
            if (nameIndex >= 0 &&
                nameIndex < document.Constants.Count &&
                string.Equals(document.Constants[nameIndex].StringValue, variableName, StringComparison.Ordinal))
            {
                return index;
            }
        }

        throw new InvalidOperationException($"KES variable '{variableName}' was not found.");
    }

    private static double MeasureMilliseconds(Action action)
    {
        var started = Stopwatch.GetTimestamp();
        action();
        return (Stopwatch.GetTimestamp() - started) * 1000d / Stopwatch.Frequency;
    }

    private static void EnsurePrime(bool result, string source)
    {
        if (!result)
        {
            throw new InvalidOperationException($"{source} reported that {Candidate} is not prime.");
        }
    }

    private sealed class Statistics
    {
        private Statistics(
            double totalMilliseconds,
            double meanMilliseconds,
            double medianMilliseconds,
            double minimumMilliseconds,
            double maximumMilliseconds,
            double standardDeviationMilliseconds)
        {
            TotalMilliseconds = totalMilliseconds;
            MeanMilliseconds = meanMilliseconds;
            MedianMilliseconds = medianMilliseconds;
            MinimumMilliseconds = minimumMilliseconds;
            MaximumMilliseconds = maximumMilliseconds;
            StandardDeviationMilliseconds = standardDeviationMilliseconds;
        }

        public double TotalMilliseconds { get; }
        public double MeanMilliseconds { get; }
        public double MedianMilliseconds { get; }
        public double MinimumMilliseconds { get; }
        public double MaximumMilliseconds { get; }
        public double StandardDeviationMilliseconds { get; }

        public static Statistics Create(double[] samples)
        {
            var sorted = samples.OrderBy(value => value).ToArray();
            var total = samples.Sum();
            var mean = total / samples.Length;
            var variance = samples.Sum(value => (value - mean) * (value - mean)) / samples.Length;
            var median = samples.Length % 2 == 0
                ? (sorted[(samples.Length / 2) - 1] + sorted[samples.Length / 2]) / 2
                : sorted[samples.Length / 2];
            return new Statistics(
                total,
                mean,
                median,
                sorted[0],
                sorted[^1],
                Math.Sqrt(variance));
        }

        public override string ToString()
        {
            return
                $"total={TotalMilliseconds:F3} ms, mean={MeanMilliseconds:F3} ms, " +
                $"median={MedianMilliseconds:F3} ms, min={MinimumMilliseconds:F3} ms, " +
                $"max={MaximumMilliseconds:F3} ms, stddev={StandardDeviationMilliseconds:F3} ms";
        }
    }

    private sealed class BenchmarkRun
    {
        public BenchmarkRun(
            KlibDocument document,
            KesVmExecutor executor,
            KesVmPerformanceCounters counters,
            int resultSlot)
        {
            Document = document;
            Executor = executor;
            Counters = counters;
            ResultSlot = resultSlot;
            CSharpTimings = new double[MeasurementCount];
            KesTimings = new double[MeasurementCount];
        }

        public KlibDocument Document { get; }
        public KesVmExecutor Executor { get; }
        public KesVmPerformanceCounters Counters { get; }
        public int ResultSlot { get; }
        public double[] CSharpTimings { get; }
        public double[] KesTimings { get; }
        public int CompletedCount { get; set; }
    }
}
