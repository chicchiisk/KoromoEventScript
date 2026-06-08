using System.Text;

namespace KoromoEventScript.Cli.Compilation;

public sealed class KlibArtifactWriter
{
    public void WriteBinary(string path, KlibDocument document)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(document);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);

        var moduleSection = BuildModuleSection(document);
        var constantSection = BuildConstantSection(document);
        var variableSection = BuildVariableSection(document);
        var importSection = BuildImportSection(document);
        var instructionSection = BuildInstructionSection(document);
        var labelSection = BuildLabelSection(document);
        var debugSection = BuildDebugSection(document);

        var sections = new List<(int Type, byte[] Data)>
        {
            (0x0001, moduleSection),
            (0x0002, constantSection),
            (0x0003, variableSection),
        };
        if (document.Imports.Count > 0)
        {
            sections.Add((0x0004, importSection));
        }

        sections.Add((0x0005, instructionSection));
        sections.Add((0x0006, labelSection));
        sections.Add((0x0007, debugSection));

        writer.Write(Encoding.ASCII.GetBytes("KLIB"));
        writer.Write(document.Version.Major);
        writer.Write(document.Version.Minor);
        writer.Write(document.Version.Patch);
        writer.Write(0);
        writer.Write(sections.Count);

        var offset = 4 + (5 * sizeof(int)) + (sections.Count * (3 * sizeof(int)));
        foreach (var section in sections)
        {
            writer.Write(section.Type);
            writer.Write(offset);
            writer.Write(section.Data.Length);
            offset += section.Data.Length;
        }

        foreach (var section in sections)
        {
            writer.Write(section.Data);
        }
    }

    public void WriteText(string path, KlibDocument document)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(document);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, RenderText(document).Replace("\r\n", "\n"), new UTF8Encoding(false));
    }

    public string RenderText(KlibDocument document)
    {
        var builder = new StringBuilder();
        builder.AppendLine(".klibtxt 1.0");
        builder.AppendLine($".module \"{document.Module.ModuleId}\"");
        builder.AppendLine($".script \"{document.Module.ScriptId}\"");
        builder.AppendLine($".source \"{document.Module.SourcePath}\"");
        builder.AppendLine($".imports.count {document.Imports.Count}");
        builder.AppendLine();
        builder.AppendLine(".imports");
        builder.AppendLine("{");
        foreach (var import in document.Imports)
        {
            builder.AppendLine($"  {import.ModuleId} {import.ScriptId} {import.SourcePath}");
        }

        if (document.Imports.Count == 0)
        {
            builder.AppendLine("  // none");
        }

        builder.AppendLine("}");
        builder.AppendLine();
        builder.AppendLine(".constants");
        builder.AppendLine("{");
        for (var index = 0; index < document.Constants.Count; index++)
        {
            var constant = document.Constants[index];
            builder.AppendLine($"  [{index}] {FormatConstant(constant, document.Constants)}");
        }

        builder.AppendLine("}");
        builder.AppendLine();
        builder.AppendLine(".variables");
        builder.AppendLine("{");
        for (var index = 0; index < document.Variables.Count; index++)
        {
            var variable = document.Variables[index];
            builder.AppendLine(
                $"  [{index}] id=cp[{variable.StableIdIndex}] name=cp[{variable.NameIndex}] type={(int)variable.Type}:{variable.Type.ToString().ToLowerInvariant()} scope={(int)variable.ScopeKind}:{variable.ScopeKind.ToString().ToLowerInvariant()} scopeId={variable.ScopeId}");
        }

        builder.AppendLine("}");
        builder.AppendLine();
        builder.AppendLine(".instructions");
        builder.AppendLine("{");
        foreach (var instruction in document.Instructions)
        {
            builder.Append("  ");
            builder.Append($"IL_{instruction.Offset:X4}: {instruction.OpCode.ToString().ToUpperInvariant()}");
            if (instruction.Operands.Count > 0)
            {
                builder.Append(' ');
                builder.Append(string.Join(" ", instruction.Operands));
            }

            if (instruction.SelectCases is { Count: > 0 })
            {
                builder.Append(" [");
                builder.Append(string.Join(", ", instruction.SelectCases.Select(@case => $"cp[{@case.TextIndex}] => {@case.Offset}")));
                builder.Append(']');
            }

            var mapping = document.Debug.SourceMappings.FirstOrDefault(mapping => mapping.BytecodeOffset == instruction.Offset);
            if (mapping is not null)
            {
                builder.Append($" // {document.Module.SourcePath}:{mapping.Line}:{mapping.Column}");
            }

            builder.AppendLine();
        }

        builder.AppendLine("}");
        builder.AppendLine();
        builder.AppendLine(".labels");
        builder.AppendLine("{");
        foreach (var label in document.Labels)
        {
            builder.AppendLine($"  cp[{label.NameIndex}] => {label.Offset} flags={label.Flags}");
        }

        builder.AppendLine("}");
        builder.AppendLine();
        builder.AppendLine(".debug");
        builder.AppendLine("{");
        builder.AppendLine("  .source-map");
        builder.AppendLine("  {");
        foreach (var mapping in document.Debug.SourceMappings)
        {
            builder.AppendLine($"    IL_{mapping.BytecodeOffset:X4} -> \"{document.Module.SourcePath}\":{mapping.Line}:{mapping.Column}");
        }

        builder.AppendLine("  }");
        builder.AppendLine("}");
        return builder.ToString();
    }

    private static byte[] BuildModuleSection(KlibDocument document)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        WriteString(writer, document.Module.ScriptId);
        WriteString(writer, document.Module.ModuleId);
        WriteString(writer, document.Module.SourcePath);
        writer.Write(document.Module.EntryLabel is null ? 0 : 1);
        if (document.Module.EntryLabel is not null)
        {
            WriteString(writer, document.Module.EntryLabel);
        }

        return stream.ToArray();
    }

    private static byte[] BuildConstantSection(KlibDocument document)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(document.Constants.Count);
        foreach (var constant in document.Constants)
        {
            writer.Write((int)constant.Kind);
            switch (constant.Kind)
            {
                case KlibConstantKind.String:
                    WriteString(writer, constant.StringValue ?? string.Empty);
                    break;

                case KlibConstantKind.Number:
                    writer.Write(constant.NumberValue ?? 0d);
                    break;

                case KlibConstantKind.Bool:
                    writer.Write(constant.BoolValue == true ? 1 : 0);
                    break;

                case KlibConstantKind.Null:
                    break;

                default:
                    writer.Write(constant.ReferenceIndex ?? 0);
                    break;
            }
        }

        return stream.ToArray();
    }

    private static byte[] BuildVariableSection(KlibDocument document)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(document.Variables.Count);
        foreach (var variable in document.Variables)
        {
            writer.Write(variable.StableIdIndex);
            writer.Write(variable.NameIndex);
            writer.Write((int)variable.Type);
            writer.Write((int)variable.ScopeKind);
            writer.Write(variable.ScopeId);
            writer.Write(variable.InitialValueIndex.HasValue ? 1 : 0);
            if (variable.InitialValueIndex.HasValue)
            {
                writer.Write(variable.InitialValueIndex.Value);
            }
        }

        return stream.ToArray();
    }

    private static byte[] BuildImportSection(KlibDocument document)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(document.Imports.Count);
        foreach (var import in document.Imports)
        {
            WriteString(writer, import.ModuleId);
            WriteString(writer, import.ScriptId);
            WriteString(writer, import.SourcePath);
            writer.Write(import.EntryLabel is null ? 0 : 1);
            if (import.EntryLabel is not null)
            {
                WriteString(writer, import.EntryLabel);
            }
        }

        return stream.ToArray();
    }

    private static byte[] BuildInstructionSection(KlibDocument document)
    {
        using var bodyStream = new MemoryStream();
        using (var bodyWriter = new BinaryWriter(bodyStream, Encoding.UTF8, leaveOpen: true))
        {
            foreach (var instruction in document.Instructions)
            {
                bodyWriter.Write((byte)instruction.OpCode);
                if (instruction.OpCode == KlibOpCode.Select)
                {
                    bodyWriter.Write(instruction.Operands[0]);
                    foreach (var @case in instruction.SelectCases ?? [])
                    {
                        bodyWriter.Write(@case.TextIndex);
                        bodyWriter.Write(@case.Offset);
                    }

                    continue;
                }

                foreach (var operand in instruction.Operands)
                {
                    bodyWriter.Write(operand);
                }
            }
        }

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write((int)bodyStream.Length);
        writer.Write(bodyStream.ToArray());
        return stream.ToArray();
    }

    private static byte[] BuildLabelSection(KlibDocument document)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(document.Labels.Count);
        foreach (var label in document.Labels)
        {
            writer.Write(label.NameIndex);
            writer.Write(label.Offset);
            writer.Write(label.Flags);
        }

        return stream.ToArray();
    }

    private static byte[] BuildDebugSection(KlibDocument document)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(document.Debug.ModuleDisplayNameIndex ?? 0);
        writer.Write(document.Debug.FileDisplayNameIndex ?? 0);
        writer.Write(document.Debug.SourceMappings.Count);
        foreach (var mapping in document.Debug.SourceMappings)
        {
            writer.Write(mapping.BytecodeOffset);
            writer.Write(mapping.FileIndex);
            writer.Write(mapping.Line);
            writer.Write(mapping.Column);
            writer.Write(mapping.EndLine);
            writer.Write(mapping.EndColumn);
            writer.Write((int)mapping.Kind);
        }

        writer.Write(0);
        return stream.ToArray();
    }

    private static string FormatConstant(KlibConstant constant, IReadOnlyList<KlibConstant> constants)
    {
        return constant.Kind switch
        {
            KlibConstantKind.String => $"string \"{constant.StringValue}\"",
            KlibConstantKind.Number => $"number {constant.NumberValue}",
            KlibConstantKind.Bool => $"bool {constant.BoolValue}",
            KlibConstantKind.Null => "null",
            _ => $"{constant.Kind.ToString().ToLowerInvariant()} -> [{constant.ReferenceIndex}] \"{constants[constant.ReferenceIndex!.Value].StringValue}\"",
        };
    }

    private static void WriteString(BinaryWriter writer, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }
}
