using KoromoEventScript.Runtime.Core.Execution;

namespace KoromoEventScript.Runtime.Windows.Diagnostics;

public enum RuntimeProfileArea
{
    Draw,
    Vm,
    AssetLoad,
}

public sealed record RuntimeProfileMeasurement(
    RuntimeProfileArea Area,
    TimeSpan Elapsed,
    RuntimeExecutionPosition Position,
    string? Detail);

public sealed record RuntimeProfileSnapshot(
    IReadOnlyList<RuntimeProfileMeasurement> Measurements);

public sealed class RuntimeProfileCollector
{
    private readonly List<RuntimeProfileMeasurement> measurements = [];

    public void Record(
        RuntimeProfileArea area,
        TimeSpan elapsed,
        RuntimeExecutionPosition position,
        string? detail = null)
    {
        measurements.Add(new RuntimeProfileMeasurement(area, elapsed, position, detail));
    }

    public RuntimeProfileSnapshot Snapshot()
    {
        return new RuntimeProfileSnapshot(measurements.ToArray());
    }
}
