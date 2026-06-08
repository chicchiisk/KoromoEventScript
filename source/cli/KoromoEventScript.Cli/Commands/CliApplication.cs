using KoromoEventScript.Cli.Commands.Build;
using KoromoEventScript.Cli.Diagnostics;

namespace KoromoEventScript.Cli.Commands;

public sealed class CliApplication
{
    private readonly BuildCheckOnlyCommand buildCheckOnlyCommand;
    private readonly BuildCommand buildCommand;
    private readonly DiagnosticSink diagnosticSink;

    public CliApplication()
        : this(new BuildCheckOnlyCommand(), new BuildCommand(), new DiagnosticSink())
    {
    }

    public CliApplication(BuildCheckOnlyCommand buildCheckOnlyCommand, BuildCommand buildCommand, DiagnosticSink diagnosticSink)
    {
        this.buildCheckOnlyCommand = buildCheckOnlyCommand;
        this.buildCommand = buildCommand;
        this.diagnosticSink = diagnosticSink;
    }

    public int Run(IReadOnlyList<string> args, TextWriter output, TextWriter error, string currentDirectory)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(currentDirectory);

        var parseResult = Parse(args);
        if (parseResult.Diagnostics.Count > 0 || parseResult.Options is null)
        {
            diagnosticSink.Write(parseResult.Diagnostics, parseResult.OutputFormat, error);
            return (int)CliExitCode.CommandLineError;
        }

        if (parseResult.Options.CheckOnly)
        {
            var checkOnlyResult = buildCheckOnlyCommand.Execute(parseResult.Options, currentDirectory);
            diagnosticSink.Write(checkOnlyResult.Diagnostics, parseResult.OutputFormat, error);
            return (int)checkOnlyResult.ExitCode;
        }

        var result = buildCommand.Execute(parseResult.Options, currentDirectory);
        diagnosticSink.Write(result.Diagnostics, parseResult.OutputFormat, error);
        return (int)result.ExitCode;
    }

    private static BuildCommandParseResult Parse(IReadOnlyList<string> args)
    {
        if (args.Count == 0 || !string.Equals(args[0], "build", StringComparison.Ordinal))
        {
            return BuildCommandParseResult.Failure(
                DiagnosticOutputFormat.Text,
                CommandLineDiagnostic("Unsupported command. Only 'build --check-only' is supported."));
        }

        string? positionalProject = null;
        string? optionProject = null;
        var checkOnly = false;
        var warningsAsErrors = false;
        var emitTextIr = false;
        var target = "windows";
        var outputFormat = DiagnosticOutputFormat.Text;
        var diagnostics = new List<Diagnostic>();

        for (var index = 1; index < args.Count; index++)
        {
            var arg = args[index];
            switch (arg)
            {
                case "--check-only":
                    checkOnly = true;
                    break;

                case "--warnings-as-errors":
                    warningsAsErrors = true;
                    break;

                case "--txt-il":
                    emitTextIr = true;
                    break;

                case "--project":
                    if (++index >= args.Count)
                    {
                        diagnostics.Add(CommandLineDiagnostic("--project requires a value."));
                        break;
                    }

                    optionProject = args[index];
                    break;

                case "--log-format":
                    if (++index >= args.Count)
                    {
                        diagnostics.Add(CommandLineDiagnostic("--log-format requires a value."));
                        break;
                    }

                    var format = args[index];
                    if (string.Equals(format, "text", StringComparison.OrdinalIgnoreCase))
                    {
                        outputFormat = DiagnosticOutputFormat.Text;
                    }
                    else if (string.Equals(format, "json", StringComparison.OrdinalIgnoreCase))
                    {
                        outputFormat = DiagnosticOutputFormat.JsonLines;
                    }
                    else
                    {
                        diagnostics.Add(CommandLineDiagnostic($"Invalid --log-format value '{format}'. Expected 'text' or 'json'."));
                    }

                    break;

                case "--target":
                    if (++index >= args.Count)
                    {
                        diagnostics.Add(CommandLineDiagnostic("--target requires a value."));
                        break;
                    }

                    target = args[index];
                    if (!string.Equals(target, "windows", StringComparison.OrdinalIgnoreCase))
                    {
                        diagnostics.Add(CommandLineDiagnostic($"Unsupported --target value '{target}'. Expected 'windows'."));
                    }

                    break;

                default:
                    if (arg.StartsWith("-", StringComparison.Ordinal))
                    {
                        diagnostics.Add(CommandLineDiagnostic($"Unsupported option '{arg}'."));
                    }
                    else if (positionalProject is null)
                    {
                        positionalProject = arg;
                    }
                    else
                    {
                        diagnostics.Add(CommandLineDiagnostic($"Unexpected argument '{arg}'."));
                    }

                    break;
            }
        }

        if (positionalProject is not null && optionProject is not null)
        {
            diagnostics.Add(CommandLineDiagnostic("Specify the project directory either as PROJECT_DIR or --project, not both."));
        }

        if (diagnostics.Count > 0)
        {
            return BuildCommandParseResult.Failure(outputFormat, diagnostics);
        }

        return BuildCommandParseResult.Success(new BuildCommandOptions(
            optionProject ?? positionalProject,
            outputFormat,
            warningsAsErrors,
            CheckOnly: checkOnly,
            EmitTextIr: emitTextIr,
            Target: target));
    }

    private static Diagnostic CommandLineDiagnostic(string message)
    {
        return new Diagnostic(DiagnosticLevel.Error, "KES9001", string.Empty, 1, 1, message);
    }

    private sealed record BuildCommandParseResult(
        BuildCommandOptions? Options,
        IReadOnlyList<Diagnostic> Diagnostics,
        DiagnosticOutputFormat OutputFormat)
    {
        public static BuildCommandParseResult Success(BuildCommandOptions options)
        {
            return new BuildCommandParseResult(options, [], options.OutputFormat);
        }

        public static BuildCommandParseResult Failure(DiagnosticOutputFormat format, params Diagnostic[] diagnostics)
        {
            return Failure(format, (IReadOnlyList<Diagnostic>)diagnostics);
        }

        public static BuildCommandParseResult Failure(DiagnosticOutputFormat format, IReadOnlyList<Diagnostic> diagnostics)
        {
            return new BuildCommandParseResult(null, diagnostics, format);
        }
    }
}
