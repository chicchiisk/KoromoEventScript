using KoromoEventScript.Cli.Diagnostics;

namespace KoromoEventScript.Cli.Build;

public sealed record SourceParseResult<T>(
    T? Syntax,
    Diagnostic? Diagnostic,
    SourceParseStatus Status);
