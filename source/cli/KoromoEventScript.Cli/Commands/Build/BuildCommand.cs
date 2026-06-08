using KoromoEventScript.Cli.Build;
using KoromoEventScript.Cli.Compilation;
using KoromoEventScript.Cli.Diagnostics;

namespace KoromoEventScript.Cli.Commands.Build;

public sealed class BuildCommand
{
    private readonly BuildPreparationService preparationService;
    private readonly KlibCompiler compiler;
    private readonly KlibArtifactWriter artifactWriter;

    public BuildCommand()
        : this(new BuildPreparationService(), new KlibCompiler(), new KlibArtifactWriter())
    {
    }

    public BuildCommand(
        BuildPreparationService preparationService,
        KlibCompiler compiler,
        KlibArtifactWriter artifactWriter)
    {
        this.preparationService = preparationService;
        this.compiler = compiler;
        this.artifactWriter = artifactWriter;
    }

    public BuildCommandResult Execute(BuildCommandOptions options, string currentDirectory)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentDirectory);

        var preparation = preparationService.Prepare(options, currentDirectory);
        if (!preparation.Succeeded)
        {
            return new BuildCommandResult(preparation.ExitCode, preparation.Diagnostics);
        }

        var config = preparation.Config!;
        var semanticResult = preparation.SemanticResult!;
        foreach (var document in semanticResult.ImportGraph?.OrderedDocuments ?? [])
        {
            var compilation = compiler.Compile(config, semanticResult, document);
            if (!compilation.Succeeded)
            {
                return new BuildCommandResult(CliExitCode.CompileError, compilation.Diagnostics);
            }

            var relativeOutput = Path.ChangeExtension(document.ProjectRelativePath, ".klib")!;
            var outputPath = Path.Combine(config.ProjectRoot, config.BuildPath, options.Target, relativeOutput);
            artifactWriter.WriteBinary(outputPath, compilation.Document!);
            if (options.EmitTextIr)
            {
                var textPath = Path.ChangeExtension(outputPath, ".klibtxt")!;
                artifactWriter.WriteText(textPath, compilation.Document!);
            }
        }

        return new BuildCommandResult(CliExitCode.Success, []);
    }
}

public sealed record BuildCommandResult(
    CliExitCode ExitCode,
    IReadOnlyList<Diagnostic> Diagnostics);
