using KoromoEventScript.Cli.Diagnostics;

namespace KoromoEventScript.Cli.Semantics;

public sealed class WarningAnalyzer
{
    public WarningAnalysisResult Analyze(IReadOnlyList<ScriptDocument> documents)
    {
        ArgumentNullException.ThrowIfNull(documents);

        var diagnostics = documents
            .Where(static document => document.Syntax.Statements.Count == 0)
            .Select(static document => new Diagnostic(
                DiagnosticLevel.Warning,
                "KES4001",
                document.ProjectRelativePath,
                1,
                1,
                "Empty script document."))
            .ToArray();

        return new WarningAnalysisResult(diagnostics);
    }
}
