using KoromoEventScript.Cli.Build;
using KoromoEventScript.Cli.Diagnostics;
using KoromoEventScript.Cli.ProjectSystem;

namespace KoromoEventScript.Cli.Commands.Build;

public sealed class BuildCheckOnlyCommand
{
    private readonly ProjectRootResolver _projectRootResolver;
    private readonly ProjectConfigLoader _projectConfigLoader;
    private readonly SourceFileParser _sourceFileParser;
    private readonly KelScriptReferenceResolver _scriptReferenceResolver;

    public BuildCheckOnlyCommand()
        : this(new ProjectRootResolver(), new ProjectConfigLoader(), new SourceFileParser(), new KelScriptReferenceResolver())
    {
    }

    public BuildCheckOnlyCommand(
        ProjectRootResolver projectRootResolver,
        ProjectConfigLoader projectConfigLoader,
        SourceFileParser sourceFileParser,
        KelScriptReferenceResolver scriptReferenceResolver)
    {
        _projectRootResolver = projectRootResolver;
        _projectConfigLoader = projectConfigLoader;
        _sourceFileParser = sourceFileParser;
        _scriptReferenceResolver = scriptReferenceResolver;
    }

    public BuildCheckOnlyResult Execute(BuildCommandOptions options, string currentDirectory)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentDirectory);

        var diagnostics = new List<Diagnostic>();
        var rootResult = _projectRootResolver.Resolve(options.ProjectDirectory, currentDirectory);
        if (!rootResult.Succeeded)
        {
            diagnostics.Add(rootResult.Diagnostic!);
            return new BuildCheckOnlyResult(CliExitCode.FileOrDirectoryError, diagnostics);
        }

        var configResult = _projectConfigLoader.Load(rootResult.ProjectRoot!);
        if (!configResult.Succeeded)
        {
            diagnostics.Add(configResult.Diagnostic!);
            return new BuildCheckOnlyResult(CliExitCode.FileOrDirectoryError, diagnostics);
        }

        var config = configResult.Config!;
        var entryAbsolutePath = ResolveProjectPath(config.ProjectRoot, config.EntryPath);
        var entryDisplayPath = NormalizeDisplayPath(config.EntryPath);
        var kelResult = _sourceFileParser.ParseKel(entryAbsolutePath, entryDisplayPath);
        if (kelResult.Status != SourceParseStatus.Success)
        {
            diagnostics.Add(kelResult.Diagnostic!);
            return new BuildCheckOnlyResult(MapParseStatus(kelResult.Status), diagnostics);
        }

        foreach (var scriptReference in _scriptReferenceResolver.ResolveScriptReferences(kelResult.Syntax!))
        {
            var scriptAbsolutePath = ResolveProjectPath(config.ProjectRoot, scriptReference);
            var scriptDisplayPath = NormalizeDisplayPath(scriptReference);
            var scriptResult = _sourceFileParser.ParseKe(scriptAbsolutePath, scriptDisplayPath);
            if (scriptResult.Status == SourceParseStatus.Success)
            {
                continue;
            }

            diagnostics.Add(scriptResult.Diagnostic!);
            if (scriptResult.Status == SourceParseStatus.FileError)
            {
                return new BuildCheckOnlyResult(CliExitCode.FileOrDirectoryError, diagnostics);
            }
        }

        return diagnostics.Count == 0
            ? new BuildCheckOnlyResult(CliExitCode.Success, diagnostics)
            : new BuildCheckOnlyResult(CliExitCode.SyntaxError, diagnostics);
    }

    private static CliExitCode MapParseStatus(SourceParseStatus status)
    {
        return status switch
        {
            SourceParseStatus.Success => CliExitCode.Success,
            SourceParseStatus.FileError => CliExitCode.FileOrDirectoryError,
            SourceParseStatus.SyntaxError => CliExitCode.SyntaxError,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
        };
    }

    private static string ResolveProjectPath(string projectRoot, string projectRelativePath)
    {
        return Path.GetFullPath(Path.Combine(projectRoot, projectRelativePath));
    }

    private static string NormalizeDisplayPath(string path)
    {
        return path.Replace('\\', '/');
    }
}
