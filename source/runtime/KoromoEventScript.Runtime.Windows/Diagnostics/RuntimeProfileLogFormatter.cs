using System.Globalization;
using KoromoEventScript.Runtime.Core.Klib;

namespace KoromoEventScript.Runtime.Windows.Diagnostics;

public sealed class RuntimeProfileLogFormatter
{
    private readonly RuntimeSourceMappingResolver sourceMappingResolver;

    public RuntimeProfileLogFormatter(RuntimeSourceMappingResolver sourceMappingResolver)
    {
        this.sourceMappingResolver = sourceMappingResolver;
    }

    public IReadOnlyList<string> Format(RuntimeProfileSnapshot snapshot, KlibDocument document)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(document);

        return snapshot.Measurements
            .Select(measurement => FormatMeasurement(measurement, document))
            .ToArray();
    }

    private string FormatMeasurement(RuntimeProfileMeasurement measurement, KlibDocument document)
    {
        var area = FormatArea(measurement.Area);
        var elapsed = measurement.Elapsed.TotalMilliseconds.ToString("0.00", CultureInfo.InvariantCulture);
        var source = sourceMappingResolver.Resolve(document, measurement.Position);
        var detail = string.IsNullOrWhiteSpace(measurement.Detail) ? string.Empty : $" asset={measurement.Detail}";
        return $"PROFILE {area} {elapsed}ms {source}{detail}";
    }

    private static string FormatArea(RuntimeProfileArea area)
    {
        return area switch
        {
            RuntimeProfileArea.Draw => "draw",
            RuntimeProfileArea.Vm => "vm",
            RuntimeProfileArea.AssetLoad => "assetLoad",
            _ => area.ToString(),
        };
    }
}
