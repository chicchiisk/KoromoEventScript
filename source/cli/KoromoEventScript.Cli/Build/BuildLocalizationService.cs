using KoromoEventScript.Cli.Commands;
using KoromoEventScript.Cli.Diagnostics;
using KoromoEventScript.Cli.Localization;
using KoromoEventScript.Cli.Parsing;
using KoromoEventScript.Cli.ProjectSystem;
using KoromoEventScript.Cli.Semantics;

namespace KoromoEventScript.Cli.Build;

public sealed record LocalizedBuildProjection(
    CliExitCode ExitCode,
    IReadOnlyList<Diagnostic> Diagnostics,
    IReadOnlyList<ScriptDocument> Documents)
{
    public bool Succeeded => ExitCode == CliExitCode.Success;
}

public sealed class BuildLocalizationService
{
    private const string DefaultDictionaryFileName = "localization.csv";

    private readonly LocalizationDictionaryCsvRepository repository;

    public BuildLocalizationService()
        : this(new LocalizationDictionaryCsvRepository())
    {
    }

    public BuildLocalizationService(LocalizationDictionaryCsvRepository repository)
    {
        this.repository = repository;
    }

    public LocalizedBuildProjection Resolve(ProjectConfig config, IReadOnlyList<ScriptDocument> documents, string localeTag)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(documents);
        ArgumentException.ThrowIfNullOrWhiteSpace(localeTag);

        var path = Path.Combine(config.ProjectRoot, DefaultDictionaryFileName);
        var loadResult = repository.Load(path);
        if (!loadResult.Succeeded)
        {
            var exitCode = loadResult.Diagnostics.Any(static diagnostic => diagnostic.Code == "KES9004")
                ? CliExitCode.FileOrDirectoryError
                : CliExitCode.CompileError;
            return new LocalizedBuildProjection(exitCode, loadResult.Diagnostics, []);
        }

        if (!loadResult.Exists || loadResult.Document is null)
        {
            return new LocalizedBuildProjection(
                CliExitCode.FileOrDirectoryError,
                [new Diagnostic(DiagnosticLevel.Error, "KES9004", "localization.csv", 1, 1, "Localization dictionary was not found.")],
                []);
        }

        if (!loadResult.Document.LocaleColumns.Contains(localeTag, StringComparer.Ordinal))
        {
            return new LocalizedBuildProjection(
                CliExitCode.CompileError,
                [new Diagnostic(DiagnosticLevel.Error, "KES9006", "localization.csv", 1, 1, $"Localization dictionary does not contain locale column '{localeTag}'.")],
                []);
        }

        var entries = loadResult.Document.Entries.ToDictionary(static entry => entry.Tag, StringComparer.Ordinal);
        var diagnostics = new List<Diagnostic>();
        var localizedDocuments = documents
            .Select(document => LocalizeDocument(document, entries, localeTag, diagnostics))
            .ToArray();

        return diagnostics.Count > 0
            ? new LocalizedBuildProjection(CliExitCode.CompileError, diagnostics, [])
            : new LocalizedBuildProjection(CliExitCode.Success, [], localizedDocuments);
    }

    private static ScriptDocument LocalizeDocument(
        ScriptDocument document,
        IReadOnlyDictionary<string, LocalizationDictionaryEntry> entries,
        string localeTag,
        List<Diagnostic> diagnostics)
    {
        var statements = document.Syntax.Statements
            .Select(statement => LocalizeStatement(statement, document.ProjectRelativePath, entries, localeTag, diagnostics))
            .ToArray();

        return new ScriptDocument(document.ProjectRelativePath, document.ModuleName, new ScriptSyntax(statements));
    }

    private static StatementSyntax LocalizeStatement(
        StatementSyntax statement,
        string projectRelativePath,
        IReadOnlyDictionary<string, LocalizationDictionaryEntry> entries,
        string localeTag,
        List<Diagnostic> diagnostics)
    {
        return statement switch
        {
            SayStatementSyntax say => LocalizeSay(say, projectRelativePath, entries, localeTag, diagnostics),
            NarStatementSyntax nar => LocalizeNar(nar, projectRelativePath, entries, localeTag, diagnostics),
            SelectStatementSyntax select => LocalizeSelect(select, projectRelativePath, entries, localeTag, diagnostics),
            IfStatementSyntax ifStatement => ifStatement with
            {
                Body = LocalizeBlock(ifStatement.Body, projectRelativePath, entries, localeTag, diagnostics),
                ElseIfClauses = ifStatement.ElseIfClauses.Select(clause => clause with
                {
                    Body = LocalizeBlock(clause.Body, projectRelativePath, entries, localeTag, diagnostics),
                }).ToArray(),
                ElseBody = ifStatement.ElseBody is null
                    ? null
                    : LocalizeBlock(ifStatement.ElseBody, projectRelativePath, entries, localeTag, diagnostics),
            },
            WhileStatementSyntax whileStatement => whileStatement with
            {
                Body = LocalizeBlock(whileStatement.Body, projectRelativePath, entries, localeTag, diagnostics),
            },
            ForStatementSyntax forStatement => forStatement with
            {
                Body = LocalizeBlock(forStatement.Body, projectRelativePath, entries, localeTag, diagnostics),
            },
            FunctionDeclarationSyntax function => function with
            {
                Body = LocalizeBlock(function.Body, projectRelativePath, entries, localeTag, diagnostics),
            },
            ActorDeclarationSyntax actor => actor with
            {
                Body = LocalizeBlock(actor.Body, projectRelativePath, entries, localeTag, diagnostics),
            },
            ClassDeclarationSyntax classDeclaration => classDeclaration with
            {
                Members = classDeclaration.Members.Select(member => member switch
                {
                    ClassMethodSyntax method => method with
                    {
                        Declaration = method.Declaration with
                        {
                            Body = LocalizeBlock(method.Declaration.Body, projectRelativePath, entries, localeTag, diagnostics),
                        },
                    },
                    _ => member,
                }).ToArray(),
            },
            _ => statement,
        };
    }

    private static BlockSyntax LocalizeBlock(
        BlockSyntax block,
        string projectRelativePath,
        IReadOnlyDictionary<string, LocalizationDictionaryEntry> entries,
        string localeTag,
        List<Diagnostic> diagnostics)
    {
        return new BlockSyntax(block.Statements
            .Select(statement => LocalizeStatement(statement, projectRelativePath, entries, localeTag, diagnostics))
            .ToArray());
    }

    private static SayStatementSyntax LocalizeSay(
        SayStatementSyntax say,
        string projectRelativePath,
        IReadOnlyDictionary<string, LocalizationDictionaryEntry> entries,
        string localeTag,
        List<Diagnostic> diagnostics)
    {
        var tag = NormalizeTag(say.Tag);
        if (tag is null)
        {
            return say;
        }

        if (!entries.TryGetValue(tag, out var entry))
        {
            diagnostics.Add(MissingTagDiagnostic(projectRelativePath, say.SpeakerLocation, tag));
            return say;
        }

        return say with { Lines = LocalizeLines(say.Lines, entry.Translations[localeTag]) };
    }

    private static NarStatementSyntax LocalizeNar(
        NarStatementSyntax nar,
        string projectRelativePath,
        IReadOnlyDictionary<string, LocalizationDictionaryEntry> entries,
        string localeTag,
        List<Diagnostic> diagnostics)
    {
        var tag = NormalizeTag(nar.Tag);
        if (tag is null)
        {
            return nar;
        }

        if (!entries.TryGetValue(tag, out var entry))
        {
            diagnostics.Add(MissingTagDiagnostic(projectRelativePath, nar.TagLocation ?? nar.KeywordLocation, tag));
            return nar;
        }

        return nar with { Lines = LocalizeLines(nar.Lines, entry.Translations[localeTag]) };
    }

    private static SelectStatementSyntax LocalizeSelect(
        SelectStatementSyntax select,
        string projectRelativePath,
        IReadOnlyDictionary<string, LocalizationDictionaryEntry> entries,
        string localeTag,
        List<Diagnostic> diagnostics)
    {
        var selectTag = NormalizeTag(select.Tag);
        if (selectTag is null)
        {
            return select;
        }

        var cases = new List<CaseClauseSyntax>(select.Cases.Count);
        for (var index = 0; index < select.Cases.Count; index++)
        {
            var tag = $"{selectTag}_c{index:00}";
            if (!entries.TryGetValue(tag, out var entry))
            {
                diagnostics.Add(MissingTagDiagnostic(projectRelativePath, select.Cases[index].TagLocation, tag));
                cases.Add(select.Cases[index]);
                continue;
            }

            cases.Add(select.Cases[index] with { Text = entry.Translations[localeTag] });
        }

        return select with { Cases = cases };
    }

    private static IReadOnlyList<TextLineSyntax> LocalizeLines(IReadOnlyList<TextLineSyntax> originalLines, string localizedText)
    {
        var localizedLines = localizedText.Split('\n');
        var results = new TextLineSyntax[localizedLines.Length];
        for (var index = 0; index < localizedLines.Length; index++)
        {
            var isExpressionLine = index < originalLines.Count && originalLines[index].IsExpressionLine;
            results[index] = new TextLineSyntax(localizedLines[index], isExpressionLine);
        }

        return results;
    }

    private static Diagnostic MissingTagDiagnostic(string projectRelativePath, SourceLocation location, string tag)
    {
        return new Diagnostic(
            DiagnosticLevel.Error,
            "KES9006",
            projectRelativePath,
            location.Line,
            location.Column,
            $"Localization dictionary does not contain tag '{tag}'.");
    }

    private static string? NormalizeTag(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return null;
        }

        return tag.StartsWith('#') ? tag[1..] : tag;
    }
}
