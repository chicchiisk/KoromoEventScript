#nullable enable

using System.Collections.Generic;

namespace KoromoEventScript.Runtime.Core.Klib
{

public enum KlibConstantKind
{
    String = 1,
    Number = 2,
    Bool = 3,
    Null = 4,
    ActorRef = 5,
    AssetRef = 6,
    LocaleKey = 7,
    ClassRef = 8,
    FieldRef = 9,
    MethodRef = 10,
}

public enum KlibVariableType
{
    Unknown = 0,
    Number = 1,
    Bool = 2,
    String = 3,
    Actor = 4,
    AssetRef = 5,
    LocaleKey = 6,
    Array = 7,
    ClassInstance = 8,
}

public enum KlibScopeKind
{
    Global = 1,
    Script = 2,
    Chapter = 3,
    Block = 4,
    Local = 5,
}

public enum KlibMappingKind
{
    Statement = 0,
    TextBody = 1,
    SelectCase = 2,
    Expression = 3,
    Synthetic = 4,
}

public enum KlibOpCode : byte
{
    PushConst = 0x01,
    PushTrue = 0x02,
    PushFalse = 0x03,
    PushNull = 0x04,
    PushInt = 0x05,
    Pop = 0x06,
    Dup = 0x07,
    LoadVar = 0x10,
    StoreVar = 0x11,
    DefVar = 0x12,
    Add = 0x20,
    Sub = 0x21,
    Mul = 0x22,
    Div = 0x23,
    Neg = 0x24,
    Eq = 0x30,
    Neq = 0x31,
    Lt = 0x32,
    Le = 0x33,
    Gt = 0x34,
    Ge = 0x35,
    And = 0x38,
    Or = 0x39,
    Not = 0x3A,
    Jump = 0x40,
    JumpFalse = 0x41,
    Label = 0x42,
    Select = 0x43,
    End = 0x4F,
    Call = 0x50,
    CallVoid = 0x51,
    SysCall = 0x52,
    SysCallVoid = 0x53,
    ArrayNew = 0x54,
    ArrayGet = 0x55,
    ArraySet = 0x56,
    New = 0x57,
    GetField = 0x58,
    SetField = 0x59,
    CallMethod = 0x5A,
    CallMethodVoid = 0x5B,
    Dispose = 0x5C,
    AddVar = 0x5D,
    IncrementVar = 0x5E,
    NumberArrayGet = 0x5F,
    NumberArraySet = 0x60,
    ArrayNewFilled = 0x61,
}

public readonly struct KlibSourceLocation
{
    public KlibSourceLocation(int line, int column)
    {
        Line = line;
        Column = column;
    }

    public int Line { get; }

    public int Column { get; }
}

public sealed record KlibVersion(int Major, int Minor, int Patch);

public sealed record KlibModuleInfo(
    string ScriptId,
    string ModuleId,
    string SourcePath,
    string? EntryLabel);

public sealed record KlibImport(
    string ModuleId,
    string ScriptId,
    string SourcePath,
    string? EntryLabel);

public sealed record KlibConstant(
    KlibConstantKind Kind,
    string? StringValue = null,
    double? NumberValue = null,
    bool? BoolValue = null,
    int? ReferenceIndex = null);

public sealed record KlibVariable(
    int StableIdIndex,
    int NameIndex,
    KlibVariableType Type,
    KlibScopeKind ScopeKind,
    int ScopeId,
    int? InitialValueIndex);

public sealed record KlibSelectCase(
    int TextIndex,
    int Offset);

public sealed record KlibInstruction(
    int Index,
    int Offset,
    KlibOpCode OpCode,
    IReadOnlyList<int> Operands,
    KlibSourceLocation? Source,
    KlibMappingKind MappingKind,
    IReadOnlyList<KlibSelectCase>? SelectCases = null);

public sealed record KlibLabel(
    int NameIndex,
    int Offset,
    int Flags);

public sealed record KlibSourceMapping(
    int BytecodeOffset,
    int FileIndex,
    int Line,
    int Column,
    int EndLine,
    int EndColumn,
    KlibMappingKind Kind);

public sealed record KlibDebugInfo(
    int? ModuleDisplayNameIndex,
    int? FileDisplayNameIndex,
    IReadOnlyList<KlibSourceMapping> SourceMappings);

public sealed record KlibDocument(
    KlibVersion Version,
    KlibModuleInfo Module,
    IReadOnlyList<KlibImport> Imports,
    IReadOnlyList<KlibConstant> Constants,
    IReadOnlyList<KlibVariable> Variables,
    IReadOnlyList<KlibInstruction> Instructions,
    IReadOnlyList<KlibLabel> Labels,
    KlibDebugInfo Debug);
}
