#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using KoromoEventScript.Runtime.Core.Diagnostics;

namespace KoromoEventScript.Runtime.Core.Klib
{

public interface IKlibModuleLoader
{
    KlibModuleLoadResult Load(string path);

    KlibModuleLoadResult Load(ReadOnlyMemory<byte> data, string sourceName);
}

public sealed class KlibModuleLoader : IKlibModuleLoader
{
    private const int ModuleInfoSectionType = 0x0001;
    private const int ConstantSectionType = 0x0002;
    private const int VariableSectionType = 0x0003;
    private const int ImportSectionType = 0x0004;
    private const int InstructionSectionType = 0x0005;
    private const int LabelSectionType = 0x0006;
    private const int DebugSectionType = 0x0007;
    private const int FunctionSectionType = 0x0008;

    public KlibModuleLoadResult Load(string path)
    {
        if (!File.Exists(path))
        {
            return KlibModuleLoadResult.Failure(
                RuntimeFailureKind.Io,
                RuntimeDiagnostic.Error(
                    "KESR2101",
                    $"Klib file was not found: {path}",
                    RuntimeFailureKind.Io));
        }

        try
        {
            return Load(File.ReadAllBytes(path), path);
        }
        catch (IOException exception)
        {
            return KlibModuleLoadResult.Failure(
                RuntimeFailureKind.Io,
                RuntimeDiagnostic.Error(
                    "KESR2101",
                    $"Klib file could not be read: {path}. {exception.Message}",
                    RuntimeFailureKind.Io));
        }
    }

    public KlibModuleLoadResult Load(ReadOnlyMemory<byte> data, string sourceName)
    {
        if (string.IsNullOrEmpty(sourceName))
        {
            sourceName = "<memory>";
        }

        try
        {
            using var stream = new MemoryStream(data.ToArray(), writable: false);
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: false);

            var magic = reader.ReadBytes(4);
            if (!magic.SequenceEqual(Encoding.ASCII.GetBytes("KLIB")))
            {
                return InvalidKlib(sourceName, "Klib file has an invalid magic header.");
            }

            var version = new KlibVersion(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
            var features = reader.ReadInt32();
            if (features != 0)
            {
                return InvalidKlib(sourceName, $"Klib file uses unsupported feature flags: {features}.");
            }

            var sectionCount = reader.ReadInt32();
            if (sectionCount < 0)
            {
                return InvalidKlib(sourceName, "Klib file has an invalid section count.");
            }

            var sections = new Dictionary<int, SectionHeader>();
            for (var i = 0; i < sectionCount; i++)
            {
                var section = new SectionHeader(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
                if (!IsValidSection(stream.Length, section))
                {
                    return InvalidKlib(sourceName, $"Klib file has an invalid section range for section type '{section.Type}'.");
                }

                sections[section.Type] = section;
            }

            if (!sections.TryGetValue(ModuleInfoSectionType, out var moduleSection))
            {
                return InvalidKlib(sourceName, "Klib file does not contain a module info section.");
            }

            if (!sections.TryGetValue(ConstantSectionType, out var constantSection))
            {
                return InvalidKlib(sourceName, "Klib file does not contain a constant section.");
            }

            if (!sections.TryGetValue(VariableSectionType, out var variableSection))
            {
                return InvalidKlib(sourceName, "Klib file does not contain a variable section.");
            }

            if (!sections.TryGetValue(InstructionSectionType, out var instructionSection))
            {
                return InvalidKlib(sourceName, "Klib file does not contain an instruction section.");
            }

            if (!sections.TryGetValue(LabelSectionType, out var labelSection))
            {
                return InvalidKlib(sourceName, "Klib file does not contain a label section.");
            }

            if (!sections.TryGetValue(DebugSectionType, out var debugSection))
            {
                return InvalidKlib(sourceName, "Klib file does not contain a debug section.");
            }

            var module = ReadSection(stream, moduleSection, ReadModuleInfo);
            var constants = ReadSection(stream, constantSection, ReadConstants);
            var variables = ReadSection(stream, variableSection, ReadVariables);
            var imports = sections.TryGetValue(ImportSectionType, out var importSection)
                ? ReadSection(stream, importSection, ReadImports)
                : Array.Empty<KlibImport>();
            var labels = ReadSection(stream, labelSection, ReadLabels);
            var debug = ReadSection(stream, debugSection, ReadDebugInfo);
            var instructions = ReadSection(stream, instructionSection, reader => ReadInstructions(reader, constants, debug));
            var functions = sections.TryGetValue(FunctionSectionType, out var functionSection)
                ? ReadSection(stream, functionSection, ReadFunctions)
                : Array.Empty<KlibFunction>();

            var document = new KlibDocument(
                version,
                module,
                imports,
                constants,
                variables,
                instructions,
                labels,
                debug,
                functions);

            return KlibModuleLoadResult.Success(document);
        }
        catch (EndOfStreamException exception)
        {
            return InvalidKlib(sourceName, exception.Message);
        }
        catch (InvalidDataException exception)
        {
            return InvalidKlib(sourceName, exception.Message);
        }
    }

    private static T ReadSection<T>(Stream stream, SectionHeader section, Func<BinaryReader, T> readerFunc)
    {
        stream.Position = section.Offset;
        var sectionBytes = new byte[section.Size];
        if (stream.Read(sectionBytes, 0, sectionBytes.Length) != sectionBytes.Length)
        {
            throw new InvalidDataException($"Klib file ended before section '{section.Type}' could be read.");
        }

        using var sectionStream = new MemoryStream(sectionBytes, writable: false);
        using var sectionReader = new BinaryReader(sectionStream, Encoding.UTF8, leaveOpen: false);
        return readerFunc(sectionReader);
    }

    private static KlibModuleInfo ReadModuleInfo(BinaryReader reader)
    {
        var scriptId = ReadString(reader);
        var moduleId = ReadString(reader);
        var sourcePath = ReadString(reader);
        var hasEntry = reader.ReadInt32();
        if (hasEntry is not 0 and not 1)
        {
            throw new InvalidDataException("Klib module info has an invalid entry label flag.");
        }

        var entryLabel = hasEntry == 1 ? ReadString(reader) : null;
        return new KlibModuleInfo(scriptId, moduleId, sourcePath, entryLabel);
    }

    private static IReadOnlyList<KlibConstant> ReadConstants(BinaryReader reader)
    {
        var count = reader.ReadInt32();
        if (count < 0)
        {
            throw new InvalidDataException("Klib constant count must not be negative.");
        }

        var constants = new List<KlibConstant>(count);
        for (var i = 0; i < count; i++)
        {
            var kind = (KlibConstantKind)reader.ReadInt32();
            constants.Add(kind switch
            {
                KlibConstantKind.String => new KlibConstant(kind, StringValue: ReadString(reader)),
                KlibConstantKind.Number => new KlibConstant(kind, NumberValue: reader.ReadDouble()),
                KlibConstantKind.Bool => new KlibConstant(kind, BoolValue: ReadBool(reader)),
                KlibConstantKind.Null => new KlibConstant(kind),
                KlibConstantKind.ActorRef or
                KlibConstantKind.AssetRef or
                KlibConstantKind.LocaleKey or
                KlibConstantKind.ClassRef or
                KlibConstantKind.FieldRef or
                KlibConstantKind.MethodRef => ReadReferenceConstant(reader, kind, constants),
                _ => throw new InvalidDataException($"Unsupported Klib constant kind '{kind}'."),
            });
        }

        return constants;
    }

    private static IReadOnlyList<KlibFunction> ReadFunctions(BinaryReader reader)
    {
        var count = reader.ReadInt32();
        if (count < 0)
        {
            throw new InvalidDataException("Klib function count must not be negative.");
        }

        var functions = new KlibFunction[count];
        for (var index = 0; index < count; index++)
        {
            var nameIndex = reader.ReadInt32();
            var entryOffset = reader.ReadInt32();
            var returnsValue = ReadBool(reader);
            var parameterCount = reader.ReadInt32();
            if (parameterCount < 0)
            {
                throw new InvalidDataException("Klib function parameter count must not be negative.");
            }

            var parameterSlots = new int[parameterCount];
            for (var parameterIndex = 0; parameterIndex < parameterCount; parameterIndex++)
            {
                parameterSlots[parameterIndex] = reader.ReadInt32();
            }

            var localCount = reader.ReadInt32();
            if (localCount < 0)
            {
                throw new InvalidDataException("Klib function local count must not be negative.");
            }

            var localSlots = new int[localCount];
            for (var localIndex = 0; localIndex < localCount; localIndex++)
            {
                localSlots[localIndex] = reader.ReadInt32();
            }

            functions[index] = new KlibFunction(nameIndex, entryOffset, parameterSlots, localSlots, returnsValue);
        }

        return functions;
    }

    private static KlibConstant ReadReferenceConstant(BinaryReader reader, KlibConstantKind kind, IReadOnlyList<KlibConstant> constants)
    {
        var referenceIndex = reader.ReadInt32();
        if (referenceIndex < 0 || referenceIndex >= constants.Count)
        {
            throw new InvalidDataException($"Klib reference constant '{kind}' points to invalid string index '{referenceIndex}'.");
        }

        var target = constants[referenceIndex];
        if (target.Kind != KlibConstantKind.String)
        {
            throw new InvalidDataException($"Klib reference constant '{kind}' must point to a string constant.");
        }

        return new KlibConstant(kind, StringValue: target.StringValue, ReferenceIndex: referenceIndex);
    }

    private static IReadOnlyList<KlibVariable> ReadVariables(BinaryReader reader)
    {
        var count = reader.ReadInt32();
        if (count < 0)
        {
            throw new InvalidDataException("Klib variable count must not be negative.");
        }

        var variables = new List<KlibVariable>(count);
        for (var i = 0; i < count; i++)
        {
            var stableIdIndex = reader.ReadInt32();
            var nameIndex = reader.ReadInt32();
            var type = (KlibVariableType)reader.ReadInt32();
            var scopeKind = (KlibScopeKind)reader.ReadInt32();
            var scopeId = reader.ReadInt32();
            var hasInitialValue = reader.ReadInt32();
            if (hasInitialValue is not 0 and not 1)
            {
                throw new InvalidDataException("Klib variable table has an invalid initial-value flag.");
            }

            int? initialValueIndex = null;
            if (hasInitialValue == 1)
            {
                initialValueIndex = reader.ReadInt32();
            }
            variables.Add(new KlibVariable(stableIdIndex, nameIndex, type, scopeKind, scopeId, initialValueIndex));
        }

        return variables;
    }

    private static IReadOnlyList<KlibImport> ReadImports(BinaryReader reader)
    {
        var count = reader.ReadInt32();
        if (count < 0)
        {
            throw new InvalidDataException("Klib import count must not be negative.");
        }

        var imports = new List<KlibImport>(count);
        for (var i = 0; i < count; i++)
        {
            var moduleId = ReadString(reader);
            var scriptId = ReadString(reader);
            var sourcePath = ReadString(reader);
            var hasEntry = reader.ReadInt32();
            if (hasEntry is not 0 and not 1)
            {
                throw new InvalidDataException("Klib import section has an invalid entry label flag.");
            }

            var entryLabel = hasEntry == 1 ? ReadString(reader) : null;
            imports.Add(new KlibImport(moduleId, scriptId, sourcePath, entryLabel));
        }

        return imports;
    }

    private static IReadOnlyList<KlibInstruction> ReadInstructions(
        BinaryReader reader,
        IReadOnlyList<KlibConstant> constants,
        KlibDebugInfo debugInfo)
    {
        var bytecodeSize = reader.ReadInt32();
        if (bytecodeSize < 0)
        {
            throw new InvalidDataException("Klib bytecode size must not be negative.");
        }

        var bytecode = reader.ReadBytes(bytecodeSize);
        if (bytecode.Length != bytecodeSize)
        {
            throw new InvalidDataException("Klib file ended before the instruction bytecode could be read.");
        }

        var sourceMappings = debugInfo.SourceMappings.ToDictionary(mapping => mapping.BytecodeOffset);
        var instructions = new List<KlibInstruction>();
        using var bytecodeStream = new MemoryStream(bytecode, writable: false);
        using var bytecodeReader = new BinaryReader(bytecodeStream, Encoding.UTF8, leaveOpen: false);
        var index = 0;
        while (bytecodeStream.Position < bytecodeStream.Length)
        {
            var offset = checked((int)bytecodeStream.Position);
            var opCode = (KlibOpCode)bytecodeReader.ReadByte();
            IReadOnlyList<int> operands;
            IReadOnlyList<KlibSelectCase>? selectCases = null;

            if (opCode == KlibOpCode.Select)
            {
                var count = bytecodeReader.ReadInt32();
                operands = new[] { count };
                var cases = new List<KlibSelectCase>(count);
                for (var i = 0; i < count; i++)
                {
                    cases.Add(new KlibSelectCase(bytecodeReader.ReadInt32(), bytecodeReader.ReadInt32()));
                }

                selectCases = cases;
            }
            else
            {
                var operandCount = OperandCount(opCode);
                var values = new int[operandCount];
                for (var i = 0; i < operandCount; i++)
                {
                    values[i] = bytecodeReader.ReadInt32();
                }

                operands = values;
            }

            sourceMappings.TryGetValue(offset, out var sourceMapping);
            instructions.Add(new KlibInstruction(
                index++,
                offset,
                opCode,
                operands,
                sourceMapping is null ? null : new KlibSourceLocation(sourceMapping.Line, sourceMapping.Column),
                sourceMapping?.Kind ?? KlibMappingKind.Statement,
                selectCases));
        }

        ValidateInstructionReferences(instructions, constants);
        return instructions;
    }

    private static IReadOnlyList<KlibLabel> ReadLabels(BinaryReader reader)
    {
        var count = reader.ReadInt32();
        if (count < 0)
        {
            throw new InvalidDataException("Klib label count must not be negative.");
        }

        var labels = new List<KlibLabel>(count);
        for (var i = 0; i < count; i++)
        {
            labels.Add(new KlibLabel(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32()));
        }

        return labels;
    }

    private static KlibDebugInfo ReadDebugInfo(BinaryReader reader)
    {
        var moduleDisplayNameIndex = reader.ReadInt32();
        var fileDisplayNameIndex = reader.ReadInt32();
        var sourceMappingCount = reader.ReadInt32();
        if (sourceMappingCount < 0)
        {
            throw new InvalidDataException("Klib source mapping count must not be negative.");
        }

        var sourceMappings = new List<KlibSourceMapping>(sourceMappingCount);
        for (var i = 0; i < sourceMappingCount; i++)
        {
            sourceMappings.Add(new KlibSourceMapping(
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                (KlibMappingKind)reader.ReadInt32()));
        }

        return new KlibDebugInfo(
            moduleDisplayNameIndex == 0 ? null : moduleDisplayNameIndex,
            fileDisplayNameIndex == 0 ? null : fileDisplayNameIndex,
            sourceMappings);
    }

    private static void ValidateInstructionReferences(IReadOnlyList<KlibInstruction> instructions, IReadOnlyList<KlibConstant> constants)
    {
        foreach (var instruction in instructions)
        {
            if (instruction.OpCode != KlibOpCode.Select)
            {
                continue;
            }

            foreach (var selectCase in instruction.SelectCases ?? Array.Empty<KlibSelectCase>())
            {
                if (selectCase.TextIndex < 0 || selectCase.TextIndex >= constants.Count)
                {
                    throw new InvalidDataException($"Klib SELECT case references invalid constant index '{selectCase.TextIndex}'.");
                }
            }
        }
    }

    private static int OperandCount(KlibOpCode opCode)
    {
        return opCode switch
        {
            KlibOpCode.PushConst or
            KlibOpCode.PushInt or
            KlibOpCode.LoadVar or
            KlibOpCode.StoreVar or
            KlibOpCode.DefVar or
            KlibOpCode.Jump or
            KlibOpCode.JumpFalse or
            KlibOpCode.ArrayNew or
            KlibOpCode.GetField or
            KlibOpCode.SetField => 1,
            KlibOpCode.Label or
            KlibOpCode.Call or
            KlibOpCode.CallVoid or
            KlibOpCode.SysCall or
            KlibOpCode.SysCallVoid or
            KlibOpCode.New or
            KlibOpCode.CallMethod or
            KlibOpCode.CallMethodVoid or
            KlibOpCode.AddVar or
            KlibOpCode.IncrementVar or
            KlibOpCode.CallFunction or
            KlibOpCode.CallFunctionVoid => 2,
            KlibOpCode.PushTrue or
            KlibOpCode.PushFalse or
            KlibOpCode.PushNull or
            KlibOpCode.Pop or
            KlibOpCode.Dup or
            KlibOpCode.Add or
            KlibOpCode.Sub or
            KlibOpCode.Mul or
            KlibOpCode.Div or
            KlibOpCode.Neg or
            KlibOpCode.Eq or
            KlibOpCode.Neq or
            KlibOpCode.Lt or
            KlibOpCode.Le or
            KlibOpCode.Gt or
            KlibOpCode.Ge or
            KlibOpCode.And or
            KlibOpCode.Or or
            KlibOpCode.Not or
            KlibOpCode.End or
            KlibOpCode.ArrayGet or
            KlibOpCode.ArraySet or
            KlibOpCode.NumberArrayGet or
            KlibOpCode.NumberArraySet or
            KlibOpCode.ArrayNewFilled or
            KlibOpCode.ReturnValue or
            KlibOpCode.ReturnVoid or
            KlibOpCode.Dispose => 0,
            _ => throw new InvalidDataException($"Unsupported Klib opcode '{opCode}'."),
        };
    }

    private static bool ReadBool(BinaryReader reader)
    {
        return reader.ReadInt32() switch
        {
            0 => false,
            1 => true,
            var value => throw new InvalidDataException($"Klib bool value '{value}' is invalid."),
        };
    }

    private static string ReadString(BinaryReader reader)
    {
        var byteLength = reader.ReadInt32();
        if (byteLength < 0)
        {
            throw new InvalidDataException("Klib string length must not be negative.");
        }

        var remaining = reader.BaseStream.Length - reader.BaseStream.Position;
        if (byteLength > remaining)
        {
            throw new InvalidDataException("Klib string length exceeds the remaining section data.");
        }

        return Encoding.UTF8.GetString(reader.ReadBytes(byteLength));
    }

    private static bool IsValidSection(long streamLength, SectionHeader section)
    {
        if (section.Offset < 0 || section.Size < 0)
        {
            return false;
        }

        return section.Offset <= streamLength && section.Size <= streamLength - section.Offset;
    }

    private static KlibModuleLoadResult InvalidKlib(string path, string message)
    {
        return KlibModuleLoadResult.Failure(
            RuntimeFailureKind.Startup,
            RuntimeDiagnostic.Error(
                "KESR2102",
                $"{message} Path: {path}",
                RuntimeFailureKind.Startup));
    }

    private readonly struct SectionHeader
    {
        public SectionHeader(int type, int offset, int size)
        {
            Type = type;
            Offset = offset;
            Size = size;
        }

        public int Type { get; }

        public int Offset { get; }

        public int Size { get; }
    }
}

public sealed record KlibModuleLoadResult(
    KlibDocument? Document,
    IReadOnlyList<RuntimeDiagnostic> Diagnostics,
    RuntimeFailureKind FailureKind)
{
    public bool Succeeded => Document is not null && FailureKind == RuntimeFailureKind.None;

    public static KlibModuleLoadResult Success(KlibDocument document)
    {
        return new KlibModuleLoadResult(document, Array.Empty<RuntimeDiagnostic>(), RuntimeFailureKind.None);
    }

    public static KlibModuleLoadResult Failure(RuntimeFailureKind failureKind, params RuntimeDiagnostic[] diagnostics)
    {
        return new KlibModuleLoadResult(null, diagnostics, failureKind);
    }
}
}
