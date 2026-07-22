using KoromoEventScript.Cli.Commands;
using KoromoEventScript.Runtime.Core.Klib;
using KoromoEventScript.Runtime.Core.Execution;
using System.Diagnostics;

namespace KoromoEventScript.Cli.Tests.Execution;

public sealed class PrimeSievePerformanceSampleTests
{
    private const int Candidate = 104707;

    [Test]
    public void BuiltPrimeSieveSample_Reports104707AsPrime()
    {
        using var fixture = TemporaryProject.Create();
        CopyProject(GetTestDataPath("projects", "function-performance-sample"), fixture.Root);

        var exitCode = new CliApplication().Run(
            ["build", fixture.Root, "--target", "unity"],
            TextWriter.Null,
            TextWriter.Null,
            TestContext.CurrentContext.WorkDirectory);
        Assert.That(exitCode, Is.EqualTo((int)CliExitCode.Success));

        var loadResult = new KlibModuleLoader().Load(
            Path.Combine(fixture.Root, "build", "unity", "events", "prime_sieve.klib"));
        Assert.That(
            loadResult.Succeeded,
            Is.True,
            string.Join(Environment.NewLine, loadResult.Diagnostics.Select(static diagnostic => diagnostic.Message)));

        var document = loadResult.Document!;
        var resultSlot = FindVariableSlot(document, "is_prime");
        var session = new KesVmSession(document);
        var counters = new KesVmPerformanceCounters();
        var executionResult = new KesVmExecutor(performanceCounters: counters).Run(session, 20_000_000);
        var performance = counters.CaptureSnapshot();
        TestContext.Progress.WriteLine(
            $"Prime sieve VM optimized gate: {performance.TotalInstructions:N0} instructions, " +
            $"ARRAY_GET {GetOpcodeCount(performance, KlibOpCode.ArrayGet):N0}, " +
            $"ARRAY_SET {GetOpcodeCount(performance, KlibOpCode.ArraySet):N0}, " +
            $"ADD_VAR {GetOpcodeCount(performance, KlibOpCode.AddVar):N0}, " +
            $"INCREMENT_VAR {GetOpcodeCount(performance, KlibOpCode.IncrementVar):N0}, " +
            $"CALL_FUNCTION {GetOpcodeCount(performance, KlibOpCode.CallFunction):N0}, " +
            $"RETURN_VALUE {GetOpcodeCount(performance, KlibOpCode.ReturnValue):N0}, " +
            $"max stack depth {performance.MaximumObservedOperandStackDepth:N0}.");

        Assert.Multiple(() =>
        {
            Assert.That(
                executionResult.Succeeded,
                Is.True,
                string.Join(Environment.NewLine, executionResult.Diagnostics.Select(static diagnostic => diagnostic.Message)));
            Assert.That(session.Variables[resultSlot].BoolValue, Is.True);
            Assert.That(performance.RunInvocations, Is.EqualTo(1));
            Assert.That(performance.SuccessfulRunInvocations, Is.EqualTo(1));
            Assert.That(performance.TotalInstructions, Is.EqualTo(2_234_287));
            Assert.That(performance.TotalInstructions, Is.LessThan(2_300_000));
            Assert.That(GetOpcodeCount(performance, KlibOpCode.ArrayGet), Is.EqualTo(323));
            Assert.That(GetOpcodeCount(performance, KlibOpCode.ArraySet), Is.EqualTo(202_616));
            Assert.That(GetOpcodeCount(performance, KlibOpCode.AddVar), Is.EqualTo(202_614));
            Assert.That(GetOpcodeCount(performance, KlibOpCode.IncrementVar), Is.EqualTo(322));
            Assert.That(GetOpcodeCount(performance, KlibOpCode.ArrayNewFilled), Is.EqualTo(1));
            Assert.That(GetOpcodeCount(performance, KlibOpCode.CallFunction), Is.EqualTo(1));
            Assert.That(GetOpcodeCount(performance, KlibOpCode.ReturnValue), Is.EqualTo(1));
        });
    }

    [Test]
    [Explicit("Manual 100-run C# versus KES performance measurement.")]
    public void BuiltPrimeSieveSample_MeasuresCSharpAndKesOneHundredTimes()
    {
        using var fixture = TemporaryProject.Create();
        CopyProject(GetTestDataPath("projects", "function-performance-sample"), fixture.Root);
        var exitCode = new CliApplication().Run(
            ["build", fixture.Root, "--target", "unity"],
            TextWriter.Null,
            TextWriter.Null,
            TestContext.CurrentContext.WorkDirectory);
        Assert.That(exitCode, Is.EqualTo((int)CliExitCode.Success));

        var loadResult = new KlibModuleLoader().Load(
            Path.Combine(fixture.Root, "build", "unity", "events", "prime_sieve.klib"));
        Assert.That(loadResult.Succeeded, Is.True);
        var document = loadResult.Document!;
        var resultSlot = FindVariableSlot(document, "is_prime");
        var executor = new KesVmExecutor();

        for (var index = 0; index < 3; index++)
        {
            Assert.That(IsPrimeCSharp(Candidate), Is.True);
            Assert.That(RunKes(document, executor, resultSlot), Is.True);
        }

        var csharpMilliseconds = 0d;
        var kesMilliseconds = 0d;
        for (var index = 0; index < 100; index++)
        {
            csharpMilliseconds += MeasureMilliseconds(() =>
                Assert.That(IsPrimeCSharp(Candidate), Is.True));
            kesMilliseconds += MeasureMilliseconds(() =>
                Assert.That(RunKes(document, executor, resultSlot), Is.True));
        }

        TestContext.Progress.WriteLine(
            $"Prime sieve 100 runs: C# {csharpMilliseconds:F3} ms total, " +
            $"KES {kesMilliseconds:F3} ms total, " +
            $"KES/C# {kesMilliseconds / csharpMilliseconds:F2}x.");
    }

    private static bool RunKes(KlibDocument document, KesVmExecutor executor, int resultSlot)
    {
        var session = new KesVmSession(document);
        var execution = executor.Run(session, 20_000_000);
        return execution.Succeeded &&
            session.Variables.TryGetValue(resultSlot, out var result) &&
            result.BoolValue is true;
    }

    private static bool IsPrimeCSharp(int candidate)
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

    private static double MeasureMilliseconds(Action action)
    {
        var started = Stopwatch.GetTimestamp();
        action();
        return (Stopwatch.GetTimestamp() - started) * 1000d / Stopwatch.Frequency;
    }

    private static long GetOpcodeCount(KesVmPerformanceSnapshot snapshot, KlibOpCode opCode)
    {
        return snapshot.OpcodeCounts.TryGetValue(opCode, out var count) ? count : 0;
    }

    private static int FindVariableSlot(KlibDocument document, string variableName)
    {
        for (var index = 0; index < document.Variables.Count; index++)
        {
            var nameIndex = document.Variables[index].NameIndex;
            if (string.Equals(document.Constants[nameIndex].StringValue, variableName, StringComparison.Ordinal))
            {
                return index;
            }
        }

        Assert.Fail($"Variable '{variableName}' was not found.");
        return -1;
    }

    private static string GetTestDataPath(params string[] segments)
    {
        return Path.GetFullPath(Path.Combine(GetRepositoryRoot(), "testdata", Path.Combine(segments)));
    }

    private static void CopyProject(string sourceRoot, string destinationRoot)
    {
        foreach (var directory in Directory.EnumerateDirectories(sourceRoot, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(destinationRoot, Path.GetRelativePath(sourceRoot, directory)));
        }

        foreach (var file in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            var destination = Path.Combine(destinationRoot, Path.GetRelativePath(sourceRoot, file));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, overwrite: true);
        }
    }

    private static string GetRepositoryRoot()
    {
        return Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", ".."));
    }
}
