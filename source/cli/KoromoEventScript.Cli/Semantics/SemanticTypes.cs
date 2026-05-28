using KoromoEventScript.Cli.Commands;
using KoromoEventScript.Cli.Diagnostics;

namespace KoromoEventScript.Cli.Semantics;

public enum KesTypeKind
{
    Number,
    Bool,
    String,
    Actor,
    Array,
    Null,
    Void,
    Unknown,
    Unsupported,
}

public sealed record KesType
{
    private KesType(KesTypeKind kind, KesType? elementType = null)
    {
        if (kind == KesTypeKind.Array)
        {
            ArgumentNullException.ThrowIfNull(elementType);
        }
        else if (elementType is not null)
        {
            throw new ArgumentException("Only array types can have an element type.", nameof(elementType));
        }

        Kind = kind;
        ElementType = elementType;
    }

    public static KesType Number { get; } = new(KesTypeKind.Number);

    public static KesType Bool { get; } = new(KesTypeKind.Bool);

    public static KesType String { get; } = new(KesTypeKind.String);

    public static KesType Actor { get; } = new(KesTypeKind.Actor);

    public static KesType Null { get; } = new(KesTypeKind.Null);

    public static KesType Void { get; } = new(KesTypeKind.Void);

    public static KesType Unknown { get; } = new(KesTypeKind.Unknown);

    public static KesType Unsupported { get; } = new(KesTypeKind.Unsupported);

    public KesTypeKind Kind { get; }

    public KesType? ElementType { get; }

    public static KesType Array(KesType elementType)
    {
        ArgumentNullException.ThrowIfNull(elementType);
        return new KesType(KesTypeKind.Array, elementType);
    }

    public bool IsReferenceType => Kind is KesTypeKind.String or KesTypeKind.Actor or KesTypeKind.Array;

    public bool IsAssignableFrom(KesType actual)
    {
        ArgumentNullException.ThrowIfNull(actual);

        if (Kind == KesTypeKind.Unknown || actual.Kind == KesTypeKind.Unknown)
        {
            return true;
        }

        if (actual.Kind == KesTypeKind.Null)
        {
            return IsReferenceType;
        }

        if (Kind != actual.Kind)
        {
            return false;
        }

        return Kind != KesTypeKind.Array || ElementType!.IsAssignableFrom(actual.ElementType!);
    }

    public override string ToString()
    {
        return Kind switch
        {
            KesTypeKind.Number => "number",
            KesTypeKind.Bool => "bool",
            KesTypeKind.String => "string",
            KesTypeKind.Actor => "Actor",
            KesTypeKind.Array => $"{ElementType}[]",
            KesTypeKind.Null => "null",
            KesTypeKind.Void => "void",
            KesTypeKind.Unknown => "<unknown>",
            _ => "<unsupported>",
        };
    }
}

public sealed record TypeCheckingResult
{
    private TypeCheckingResult(CliExitCode exitCode, IReadOnlyList<Diagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        ExitCode = exitCode;
        Diagnostics = diagnostics.ToArray();
    }

    public CliExitCode ExitCode { get; }

    public IReadOnlyList<Diagnostic> Diagnostics { get; }

    public bool Succeeded => ExitCode == CliExitCode.Success;

    public static TypeCheckingResult Success()
    {
        return new TypeCheckingResult(CliExitCode.Success, []);
    }

    public static TypeCheckingResult Failure(IReadOnlyList<Diagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        if (diagnostics.Count == 0)
        {
            throw new ArgumentException("Failure results must include diagnostics.", nameof(diagnostics));
        }

        return new TypeCheckingResult(CliExitCode.CompileError, diagnostics);
    }
}
