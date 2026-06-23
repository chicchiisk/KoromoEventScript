using System.Globalization;
using KoromoEventScript.Runtime.Core.Diagnostics;

namespace KoromoEventScript.Runtime.Windows.Diagnostics;

public sealed class RuntimeDebugOverlayFormatter
{
    public IReadOnlyList<string> Format(RuntimeDiagnosticsSnapshot snapshot, RuntimeDiagnosticsMode mode)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var warningCount = snapshot.Diagnostics.Count(static diagnostic => diagnostic.Severity == RuntimeDiagnosticSeverity.Warning);
        var errorCount = snapshot.Diagnostics.Count(static diagnostic => diagnostic.Severity == RuntimeDiagnosticSeverity.Error);

        if (mode == RuntimeDiagnosticsMode.Normal)
        {
            return [$"Warnings: {warningCount} Errors: {errorCount}"];
        }

        var lines = new List<string>
        {
            $"FPS: {snapshot.Fps.ToString("0.00", CultureInfo.InvariantCulture)}",
            $"VM: {snapshot.VmPosition.ScriptId}#{snapshot.VmPosition.InstructionIndex}",
            $"Resources: loaded={snapshot.Resources.LoadedAssetCount} unresolved={FormatUnresolvedResources(snapshot.Resources)}",
            $"Audio: BGM={snapshot.Audio.Bgm?.AssetId ?? "-"} Voice={snapshot.Audio.Voice?.AssetId ?? "-"} SE={snapshot.Audio.SoundEffects.Count}",
            $"Input: {FormatInput(snapshot)}",
            $"Warnings: {warningCount} Errors: {errorCount}",
        };

        foreach (var diagnostic in snapshot.Diagnostics)
        {
            lines.Add($"{diagnostic.Severity}: {diagnostic.Code} {diagnostic.Message}");
        }

        return lines;
    }

    private static string FormatUnresolvedResources(RuntimeResourceDiagnostics resources)
    {
        return resources.UnresolvedAssetIds.Count == 0
            ? "-"
            : string.Join(",", resources.UnresolvedAssetIds);
    }

    private static string FormatInput(RuntimeDiagnosticsSnapshot snapshot)
    {
        return snapshot.LastInput is null
            ? "-"
            : $"{snapshot.LastInput.Action}/{snapshot.LastInput.Source}";
    }
}
