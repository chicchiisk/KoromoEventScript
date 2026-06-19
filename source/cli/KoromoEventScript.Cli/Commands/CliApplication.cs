using KoromoEventScript.Cli.Commands.Build;
using KoromoEventScript.Cli.Commands.Correct;
using KoromoEventScript.Cli.Commands.Init;
using KoromoEventScript.Cli.Commands.Loc;
using KoromoEventScript.Cli.Diagnostics;

namespace KoromoEventScript.Cli.Commands;

public sealed class CliApplication
{
    private readonly BuildCheckOnlyCommand buildCheckOnlyCommand;
    private readonly BuildCommand buildCommand;
    private readonly CorrectCommand correctCommand;
    private readonly InitCommand initCommand;
    private readonly LocCommand locCommand;
    private readonly DiagnosticSink diagnosticSink;

    public CliApplication()
        : this(new BuildCheckOnlyCommand(), new BuildCommand(), new CorrectCommand(), new InitCommand(), new LocCommand(), new DiagnosticSink())
    {
    }

    public CliApplication(
        BuildCheckOnlyCommand buildCheckOnlyCommand,
        BuildCommand buildCommand,
        CorrectCommand correctCommand,
        InitCommand initCommand,
        LocCommand locCommand,
        DiagnosticSink diagnosticSink)
    {
        this.buildCheckOnlyCommand = buildCheckOnlyCommand;
        this.buildCommand = buildCommand;
        this.correctCommand = correctCommand;
        this.initCommand = initCommand;
        this.locCommand = locCommand;
        this.diagnosticSink = diagnosticSink;
    }

    public int Run(IReadOnlyList<string> args, TextWriter output, TextWriter error, string currentDirectory)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(currentDirectory);

        var parseResult = Parse(args);
        if (parseResult.Diagnostics.Count > 0 || (parseResult.BuildOptions is null && parseResult.InitOptions is null && parseResult.CorrectOptions is null && parseResult.LocOptions is null))
        {
            diagnosticSink.Write(parseResult.Diagnostics, parseResult.OutputFormat, error);
            return (int)CliExitCode.CommandLineError;
        }

        if (parseResult.CorrectOptions is not null)
        {
            var correctResult = correctCommand.Execute(parseResult.CorrectOptions, currentDirectory);
            diagnosticSink.Write(correctResult.Diagnostics, parseResult.OutputFormat, error);
            if (!string.IsNullOrWhiteSpace(correctResult.StandardOutput))
            {
                output.WriteLine(correctResult.StandardOutput);
            }

            return (int)correctResult.ExitCode;
        }

        if (parseResult.InitOptions is not null)
        {
            var initResult = initCommand.Execute(parseResult.InitOptions, currentDirectory);
            diagnosticSink.Write(initResult.Diagnostics, parseResult.OutputFormat, error);
            if (!string.IsNullOrWhiteSpace(initResult.SuccessMessage))
            {
                output.WriteLine(initResult.SuccessMessage);
            }

            return (int)initResult.ExitCode;
        }

        if (parseResult.LocOptions is not null)
        {
            var locResult = locCommand.Execute(parseResult.LocOptions, currentDirectory);
            diagnosticSink.Write(locResult.Diagnostics, parseResult.OutputFormat, error);
            if (!string.IsNullOrWhiteSpace(locResult.SuccessMessage))
            {
                output.WriteLine(locResult.SuccessMessage);
            }

            return (int)locResult.ExitCode;
        }

        if (parseResult.BuildOptions!.CheckOnly)
        {
            var checkOnlyResult = buildCheckOnlyCommand.Execute(parseResult.BuildOptions, currentDirectory);
            diagnosticSink.Write(checkOnlyResult.Diagnostics, parseResult.OutputFormat, error);
            return (int)checkOnlyResult.ExitCode;
        }

        var result = buildCommand.Execute(parseResult.BuildOptions, currentDirectory);
        diagnosticSink.Write(result.Diagnostics, parseResult.OutputFormat, error);
        return (int)result.ExitCode;
    }

    private static CommandParseResult Parse(IReadOnlyList<string> args)
    {
        if (args.Count == 0)
        {
            return CommandParseResult.Failure(
                DiagnosticOutputFormat.Text,
                CommandLineDiagnostic("Unsupported command. Only 'build', 'correct', 'init', and 'loc' are supported."));
        }

        return args[0] switch
        {
            "build" => ParseBuild(args),
            "correct" => ParseCorrect(args),
            "init" => ParseInit(args),
            "loc" => ParseLoc(args),
            _ => CommandParseResult.Failure(
                DiagnosticOutputFormat.Text,
                CommandLineDiagnostic("Unsupported command. Only 'build', 'correct', 'init', and 'loc' are supported.")),
        };
    }

    private static CommandParseResult ParseBuild(IReadOnlyList<string> args)
    {
        string? positionalProject = null;
        string? entryPath = null;
        string? outputDirectory = null;
        string? locale = null;
        var checkOnly = false;
        var warningsAsErrors = false;
        var emitTextIr = false;
        var noIncremental = false;
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

                case "--entry":
                    if (++index >= args.Count)
                    {
                        diagnostics.Add(CommandLineDiagnostic("--entry requires a value."));
                        break;
                    }

                    entryPath = args[index];
                    break;

                case "--txt-il":
                    emitTextIr = true;
                    break;

                case "--out-dir":
                    if (++index >= args.Count)
                    {
                        diagnostics.Add(CommandLineDiagnostic("--out-dir requires a value."));
                        break;
                    }

                    outputDirectory = args[index];
                    break;

                case "--loc":
                    if (++index >= args.Count)
                    {
                        diagnostics.Add(CommandLineDiagnostic("--loc requires a value."));
                        break;
                    }

                    locale = args[index];
                    if (string.IsNullOrWhiteSpace(locale))
                    {
                        diagnostics.Add(CommandLineDiagnostic("--loc requires a locale tag."));
                    }
                    else if (!locale.All(static character => char.IsAsciiLetterOrDigit(character) || character == '-'))
                    {
                        diagnostics.Add(CommandLineDiagnostic($"Invalid locale tag '{locale}'. Expected only letters, digits, or hyphen."));
                    }
                    break;

                case "--no-incremental":
                    noIncremental = true;
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

        if (emitTextIr && checkOnly)
        {
            diagnostics.Add(CommandLineDiagnostic("--txt-il cannot be combined with --check-only."));
        }

        if (diagnostics.Count > 0)
        {
            return CommandParseResult.Failure(outputFormat, diagnostics);
        }

        return CommandParseResult.BuildSuccess(new BuildCommandOptions(
            positionalProject,
            outputFormat,
            warningsAsErrors,
            entryPath,
            CheckOnly: checkOnly,
            EmitTextIr: emitTextIr,
            Target: target,
            OutputDirectory: outputDirectory,
            Locale: locale,
            NoIncremental: noIncremental));
    }

    private static CommandParseResult ParseCorrect(IReadOnlyList<string> args)
    {
        string? positionalProject = null;
        string? entryPath = null;
        var checkOnly = false;
        var outputFormat = DiagnosticOutputFormat.Text;
        var diagnostics = new List<Diagnostic>();

        for (var index = 1; index < args.Count; index++)
        {
            var arg = args[index];
            switch (arg)
            {
                case "--entry":
                    if (++index >= args.Count)
                    {
                        diagnostics.Add(CommandLineDiagnostic("--entry requires a value."));
                        break;
                    }

                    entryPath = args[index];
                    break;

                case "--check-only":
                    checkOnly = true;
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

        if (diagnostics.Count > 0)
        {
            return CommandParseResult.Failure(outputFormat, diagnostics);
        }

        return CommandParseResult.CorrectSuccess(new CorrectCommandOptions(
            positionalProject,
            entryPath,
            checkOnly,
            outputFormat));
    }

    private static CommandParseResult ParseInit(IReadOnlyList<string> args)
    {
        string? positionalProject = null;
        string? projectName = null;
        var template = InitTemplate.Basic;
        var force = false;
        var noSample = false;
        var outputFormat = DiagnosticOutputFormat.Text;
        var diagnostics = new List<Diagnostic>();

        for (var index = 1; index < args.Count; index++)
        {
            var arg = args[index];
            switch (arg)
            {
                case "--name":
                    if (++index >= args.Count)
                    {
                        diagnostics.Add(CommandLineDiagnostic("--name requires a value."));
                        break;
                    }

                    projectName = args[index];
                    break;

                case "--template":
                    if (++index >= args.Count)
                    {
                        diagnostics.Add(CommandLineDiagnostic("--template requires a value."));
                        break;
                    }

                    var templateValue = args[index];
                    if (string.Equals(templateValue, "basic", StringComparison.OrdinalIgnoreCase))
                    {
                        template = InitTemplate.Basic;
                    }
                    else if (string.Equals(templateValue, "empty", StringComparison.OrdinalIgnoreCase))
                    {
                        template = InitTemplate.Empty;
                    }
                    else
                    {
                        diagnostics.Add(CommandLineDiagnostic($"Invalid --template value '{templateValue}'. Expected 'basic' or 'empty'."));
                    }

                    break;

                case "--force":
                    force = true;
                    break;

                case "--no-sample":
                    noSample = true;
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

        if (diagnostics.Count > 0)
        {
            return CommandParseResult.Failure(outputFormat, diagnostics);
        }

        return CommandParseResult.InitSuccess(new InitCommandOptions(
            positionalProject,
            projectName,
            template,
            force,
            noSample,
            outputFormat));
    }

    private static CommandParseResult ParseLoc(IReadOnlyList<string> args)
    {
        string? positionalProject = null;
        string? outputPath = null;
        var locales = new List<string>();
        var outputFormat = DiagnosticOutputFormat.Text;
        var diagnostics = new List<Diagnostic>();

        for (var index = 1; index < args.Count; index++)
        {
            var arg = args[index];
            switch (arg)
            {
                case "--locale":
                    if (++index >= args.Count)
                    {
                        diagnostics.Add(CommandLineDiagnostic("--locale requires a value."));
                        break;
                    }

                    AddLocales(args[index], locales, diagnostics);
                    break;

                case "--out":
                    if (++index >= args.Count)
                    {
                        diagnostics.Add(CommandLineDiagnostic("--out requires a value."));
                        break;
                    }

                    outputPath = args[index];
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

        if (diagnostics.Count > 0)
        {
            return CommandParseResult.Failure(outputFormat, diagnostics);
        }

        return CommandParseResult.LocSuccess(new LocCommandOptions(
            positionalProject,
            locales,
            outputPath,
            outputFormat));
    }

    private static Diagnostic CommandLineDiagnostic(string message)
    {
        return new Diagnostic(DiagnosticLevel.Error, "KES9001", string.Empty, 1, 1, message);
    }

    private static void AddLocales(string localeList, List<string> locales, List<Diagnostic> diagnostics)
    {
        var values = localeList.Split(',', StringSplitOptions.TrimEntries);
        if (values.Length == 0)
        {
            diagnostics.Add(CommandLineDiagnostic("--locale requires at least one locale tag."));
            return;
        }

        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                diagnostics.Add(CommandLineDiagnostic("Locale tags in --locale must not be empty."));
                continue;
            }

            if (!value.All(static character => char.IsAsciiLetterOrDigit(character) || character == '-'))
            {
                diagnostics.Add(CommandLineDiagnostic($"Invalid locale tag '{value}'. Expected only letters, digits, or hyphen."));
                continue;
            }

            if (!locales.Contains(value, StringComparer.Ordinal))
            {
                locales.Add(value);
            }
        }
    }

    private sealed record CommandParseResult(
        BuildCommandOptions? BuildOptions,
        CorrectCommandOptions? CorrectOptions,
        InitCommandOptions? InitOptions,
        LocCommandOptions? LocOptions,
        IReadOnlyList<Diagnostic> Diagnostics,
        DiagnosticOutputFormat OutputFormat)
    {
        public static CommandParseResult BuildSuccess(BuildCommandOptions options)
        {
            return new CommandParseResult(options, null, null, null, [], options.OutputFormat);
        }

        public static CommandParseResult CorrectSuccess(CorrectCommandOptions options)
        {
            return new CommandParseResult(null, options, null, null, [], options.OutputFormat);
        }

        public static CommandParseResult InitSuccess(InitCommandOptions options)
        {
            return new CommandParseResult(null, null, options, null, [], options.OutputFormat);
        }

        public static CommandParseResult LocSuccess(LocCommandOptions options)
        {
            return new CommandParseResult(null, null, null, options, [], options.OutputFormat);
        }

        public static CommandParseResult Failure(DiagnosticOutputFormat format, params Diagnostic[] diagnostics)
        {
            return Failure(format, (IReadOnlyList<Diagnostic>)diagnostics);
        }

        public static CommandParseResult Failure(DiagnosticOutputFormat format, IReadOnlyList<Diagnostic> diagnostics)
        {
            return new CommandParseResult(null, null, null, null, diagnostics, format);
        }
    }
}
