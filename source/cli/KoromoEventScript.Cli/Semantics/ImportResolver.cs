using KoromoEventScript.Cli.Build;
using KoromoEventScript.Cli.Commands;
using KoromoEventScript.Cli.Diagnostics;
using KoromoEventScript.Cli.Parsing;

namespace KoromoEventScript.Cli.Semantics;

public sealed class ImportResolver
{
    private readonly Func<ModuleFileEntry, SourceParseResult<ScriptSyntax>> parseImport;

    public ImportResolver()
        : this(entry => new SourceFileParser().ParseKe(entry.FullPath, entry.ProjectRelativePath))
    {
    }

    public ImportResolver(Func<ModuleFileEntry, SourceParseResult<ScriptSyntax>> parseImport)
    {
        ArgumentNullException.ThrowIfNull(parseImport);
        this.parseImport = parseImport;
    }

    public ImportResolutionResult ResolveImports(
        ModuleFileIndex moduleIndex,
        IReadOnlyList<ScriptDocument> roots)
    {
        ArgumentNullException.ThrowIfNull(moduleIndex);
        ArgumentNullException.ThrowIfNull(roots);

        var state = new ResolutionState(moduleIndex, parseImport);
        foreach (var root in roots)
        {
            state.AddRoot(root);
        }

        foreach (var root in roots)
        {
            state.Visit(root);
        }

        if (state.Diagnostics.Count > 0)
        {
            return ImportResolutionResult.Failure(state.GetExitCode(), state.Diagnostics);
        }

        return ImportResolutionResult.Success(new ImportGraph(state.OrderedDocuments, state.DirectImports));
    }

    private sealed class ResolutionState
    {
        private readonly ModuleFileIndex moduleIndex;
        private readonly Func<ModuleFileEntry, SourceParseResult<ScriptSyntax>> parseImport;
        private readonly Dictionary<string, ScriptDocument> documentsByModule = new(StringComparer.Ordinal);
        private readonly HashSet<string> orderedModules = new(StringComparer.Ordinal);
        private readonly HashSet<string> completedModules = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> activeIndexes = new(StringComparer.Ordinal);
        private readonly List<string> activePath = [];
        private CliExitCode exitCode = CliExitCode.Success;

        public ResolutionState(
            ModuleFileIndex moduleIndex,
            Func<ModuleFileEntry, SourceParseResult<ScriptSyntax>> parseImport)
        {
            this.moduleIndex = moduleIndex;
            this.parseImport = parseImport;
        }

        public List<ScriptDocument> OrderedDocuments { get; } = [];

        public Dictionary<string, IReadOnlyList<string>> DirectImports { get; } = new(StringComparer.Ordinal);

        public List<Diagnostic> Diagnostics { get; } = [];

        public void AddRoot(ScriptDocument root)
        {
            ArgumentNullException.ThrowIfNull(root);
            AddDocument(root);
        }

        public void Visit(ScriptDocument document)
        {
            if (completedModules.Contains(document.ModuleName))
            {
                return;
            }

            if (activeIndexes.ContainsKey(document.ModuleName))
            {
                return;
            }

            activeIndexes[document.ModuleName] = activePath.Count;
            activePath.Add(document.ModuleName);

            var imports = new List<string>();
            foreach (var importStatement in document.Syntax.Statements.OfType<ImportStatementSyntax>())
            {
                imports.Add(importStatement.ModuleName);
                ResolveImport(document, importStatement.ModuleName);
            }

            DirectImports[document.ModuleName] = imports;
            activeIndexes.Remove(document.ModuleName);
            activePath.RemoveAt(activePath.Count - 1);
            completedModules.Add(document.ModuleName);
        }

        public CliExitCode GetExitCode()
        {
            return exitCode == CliExitCode.Success
                ? CliExitCode.CompileError
                : exitCode;
        }

        private void ResolveImport(ScriptDocument importer, string moduleName)
        {
            if (activeIndexes.TryGetValue(moduleName, out var cycleStart))
            {
                AddCompileDiagnostic(CycleDiagnostic(importer, activePath.Skip(cycleStart).Concat([moduleName])));
                return;
            }

            if (completedModules.Contains(moduleName))
            {
                return;
            }

            if (documentsByModule.TryGetValue(moduleName, out var parsedDocument))
            {
                Visit(parsedDocument);
                return;
            }

            var match = moduleIndex.FindModule(moduleName);
            switch (match.Kind)
            {
                case ModuleFileMatchKind.Missing:
                    AddFileDiagnostic(MissingDiagnostic(importer, moduleName));
                    return;

                case ModuleFileMatchKind.Ambiguous:
                    AddCompileDiagnostic(AmbiguousDiagnostic(importer, moduleName, match.Candidates));
                    return;

                case ModuleFileMatchKind.Found:
                    ResolveFoundImport(match.File!);
                    return;

                default:
                    throw new InvalidOperationException($"Unsupported module match kind: {match.Kind}");
            }
        }

        private void ResolveFoundImport(ModuleFileEntry entry)
        {
            var parseResult = parseImport(entry);
            switch (parseResult.Status)
            {
                case SourceParseStatus.Success:
                    var document = new ScriptDocument(entry.ProjectRelativePath, entry.ModuleName, parseResult.Syntax!);
                    AddDocument(document);
                    Visit(document);
                    return;

                case SourceParseStatus.FileError:
                    AddFileDiagnostic(parseResult.Diagnostic!);
                    return;

                case SourceParseStatus.SyntaxError:
                    AddSyntaxDiagnostic(parseResult.Diagnostic!);
                    return;

                default:
                    throw new InvalidOperationException($"Unsupported source parse status: {parseResult.Status}");
            }
        }

        private void AddDocument(ScriptDocument document)
        {
            if (!documentsByModule.ContainsKey(document.ModuleName))
            {
                documentsByModule[document.ModuleName] = document;
            }

            if (orderedModules.Add(document.ModuleName))
            {
                OrderedDocuments.Add(document);
            }
        }

        private void AddFileDiagnostic(Diagnostic diagnostic)
        {
            Diagnostics.Add(diagnostic);
            SetExitCode(CliExitCode.FileOrDirectoryError);
        }

        private void AddSyntaxDiagnostic(Diagnostic diagnostic)
        {
            Diagnostics.Add(diagnostic);
            SetExitCode(CliExitCode.SyntaxError);
        }

        private void AddCompileDiagnostic(Diagnostic diagnostic)
        {
            Diagnostics.Add(diagnostic);
            SetExitCode(CliExitCode.CompileError);
        }

        private void SetExitCode(CliExitCode candidate)
        {
            if (exitCode == CliExitCode.Success || (int)candidate < (int)exitCode)
            {
                exitCode = candidate;
            }
        }

        private static Diagnostic MissingDiagnostic(ScriptDocument importer, string moduleName)
        {
            return new Diagnostic(
                DiagnosticLevel.Error,
                "KES9005",
                importer.ProjectRelativePath,
                1,
                1,
                $"Imported module '{moduleName}' was not found in the project events path.");
        }

        private static Diagnostic AmbiguousDiagnostic(
            ScriptDocument importer,
            string moduleName,
            IReadOnlyList<ModuleFileEntry> candidates)
        {
            var paths = string.Join(", ", candidates.Select(static candidate => candidate.ProjectRelativePath));
            return new Diagnostic(
                DiagnosticLevel.Error,
                "KES2007",
                importer.ProjectRelativePath,
                1,
                1,
                $"Imported module '{moduleName}' is ambiguous. Candidates: {paths}.");
        }

        private static Diagnostic CycleDiagnostic(ScriptDocument importer, IEnumerable<string> cycleModules)
        {
            var cyclePath = string.Join(" -> ", cycleModules);
            return new Diagnostic(
                DiagnosticLevel.Error,
                "KES2008",
                importer.ProjectRelativePath,
                1,
                1,
                $"Import cycle detected: {cyclePath}.");
        }
    }
}
