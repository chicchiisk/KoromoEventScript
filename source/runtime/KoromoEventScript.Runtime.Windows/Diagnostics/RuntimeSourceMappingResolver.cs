using KoromoEventScript.Runtime.Core.Execution;
using KoromoEventScript.Runtime.Core.Klib;

namespace KoromoEventScript.Runtime.Windows.Diagnostics;

public sealed class RuntimeSourceMappingResolver
{
    public string Resolve(KlibDocument document, RuntimeExecutionPosition position)
    {
        ArgumentNullException.ThrowIfNull(document);

        var instruction = document.Instructions.FirstOrDefault(instruction => instruction.Index == position.InstructionIndex);
        if (instruction is null)
        {
            return Fallback(position);
        }

        var mapping = document.Debug.SourceMappings.FirstOrDefault(mapping => mapping.BytecodeOffset == instruction.Offset);
        if (mapping is null)
        {
            return Fallback(position);
        }

        var file = ResolveFileName(document, mapping.FileIndex) ?? document.Module.SourcePath;
        return $"{file}:{mapping.Line}:{mapping.Column}";
    }

    private static string Fallback(RuntimeExecutionPosition position)
    {
        return $"{position.ScriptId}#{position.InstructionIndex}";
    }

    private static string? ResolveFileName(KlibDocument document, int fileIndex)
    {
        if (fileIndex < 0 || fileIndex >= document.Constants.Count)
        {
            return null;
        }

        var constant = document.Constants[fileIndex];
        return constant.Kind == KlibConstantKind.String
            ? constant.StringValue
            : null;
    }
}
