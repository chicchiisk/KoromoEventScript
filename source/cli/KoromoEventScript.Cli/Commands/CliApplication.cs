using KoromoEventScript.Cli.Commands.Build;
using KoromoEventScript.Cli.Commands.Correct;
using KoromoEventScript.Cli.Commands.Init;
using KoromoEventScript.Cli.Commands.Loc;
using KoromoEventScript.Cli.Commands.Publish;
using KoromoEventScript.Cli.Commands.Run;
using KoromoEventScript.Cli.Diagnostics;

namespace KoromoEventScript.Cli.Commands;

public sealed class CliApplication
{
    private readonly BuildCheckOnlyCommand buildCheckOnlyCommand;
    private readonly BuildCommand buildCommand;
    private readonly CorrectCommand correctCommand;
    private readonly InitCommand initCommand;
    private readonly LocCommand locCommand;
    private readonly WindowsPublishCommand publishCommand;
    private readonly RunCommand runCommand;
    private readonly DiagnosticSink diagnosticSink;

    public CliApplication()
        : this(new BuildCheckOnlyCommand(), new BuildCommand(), new CorrectCommand(), new InitCommand(), new LocCommand(), new WindowsPublishCommand(), new RunCommand(), new DiagnosticSink())
    {
    }

    public CliApplication(
        BuildCheckOnlyCommand buildCheckOnlyCommand,
        BuildCommand buildCommand,
        CorrectCommand correctCommand,
        InitCommand initCommand,
        LocCommand locCommand,
        DiagnosticSink diagnosticSink)
        : this(buildCheckOnlyCommand, buildCommand, correctCommand, initCommand, locCommand, new WindowsPublishCommand(), new RunCommand(), diagnosticSink)
    {
    }

    public CliApplication(
        BuildCheckOnlyCommand buildCheckOnlyCommand,
        BuildCommand buildCommand,
        CorrectCommand correctCommand,
        InitCommand initCommand,
        LocCommand locCommand,
        WindowsPublishCommand publishCommand,
        RunCommand runCommand,
        DiagnosticSink diagnosticSink)
    {
        this.buildCheckOnlyCommand = buildCheckOnlyCommand;
        this.buildCommand = buildCommand;
        this.correctCommand = correctCommand;
        this.initCommand = initCommand;
        this.locCommand = locCommand;
        this.publishCommand = publishCommand;
        this.runCommand = runCommand;
        this.diagnosticSink = diagnosticSink;
    }

    public int Run(IReadOnlyList<string> args, TextWriter output, TextWriter error, string currentDirectory)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(currentDirectory);

        var parseResult = Parse(args);
        if (parseResult.Diagnostics.Count > 0 || (parseResult.BuildOptions is null && parseResult.InitOptions is null && parseResult.CorrectOptions is null && parseResult.LocOptions is null && parseResult.PublishOptions is null && parseResult.RunOptions is null))
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

        if (parseResult.PublishOptions is not null)
        {
            var publishResult = publishCommand.Execute(parseResult.PublishOptions, currentDirectory);
            diagnosticSink.Write(publishResult.Diagnostics, parseResult.OutputFormat, error);
            return (int)publishResult.ExitCode;
        }

        if (parseResult.RunOptions is not null)
        {
            var runResult = runCommand.Execute(parseResult.RunOptions, currentDirectory);
            diagnosticSink.Write(runResult.Diagnostics, parseResult.OutputFormat, error);
            return runResult.ExitCode;
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
                CommandLineDiagnostic("Unsupported command. Only 'build', 'correct', 'init', 'loc', 'publish', and 'run' are supported."));
        }

        return args[0] switch
        {
            "build" => ParseBuild(args),
            "correct" => ParseCorrect(args),
            "init" => ParseInit(args),
            "loc" => ParseLoc(args),
            "publish" => ParsePublish(args),
            "run" => ParseRun(args),
            _ => CommandParseResult.Failure(
                DiagnosticOutputFormat.Text,
                CommandLineDiagnostic("Unsupported command. Only 'build', 'correct', 'init', 'loc', 'publish', and 'run' are supported.")),
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

    private static CommandParseResult ParseRun(IReadOnlyList<string> args)
    {
        string? positionalProject = null;
        string? locale = null;
        string? start = null;
        int? width = null;
        int? height = null;
        var target = "windows";
        var build = false;
        var noBuild = false;
        var fullscreen = false;
        var debug = false;
        var profile = false;
        var runtimeArguments = new List<string>();
        var outputFormat = DiagnosticOutputFormat.Text;
        var diagnostics = new List<Diagnostic>();

        for (var index = 1; index < args.Count; index++)
        {
            var arg = args[index];
            switch (arg)
            {
                case "--":
                    runtimeArguments.AddRange(args.Skip(index + 1));
                    index = args.Count;
                    break;

                case "--target":
                    if (++index >= args.Count)
                    {
                        diagnostics.Add(CommandLineDiagnostic("--target requires a value."));
                        break;
                    }

                    var targetValue = args[index];
                    if (string.Equals(targetValue, "windows", StringComparison.OrdinalIgnoreCase))
                    {
                        target = "windows";
                    }
                    else
                    {
                        diagnostics.Add(CommandLineDiagnostic($"Unsupported --target value '{targetValue}'. Expected 'windows'."));
                    }

                    break;

                case "--build":
                    build = true;
                    break;

                case "--no-build":
                    noBuild = true;
                    break;

                case "--manifest":
                    diagnostics.Add(CommandLineDiagnostic("Unsupported option '--manifest'."));
                    if (index + 1 < args.Count && !args[index + 1].StartsWith("-", StringComparison.Ordinal))
                    {
                        index++;
                    }

                    break;

                case "--locale":
                    if (++index >= args.Count)
                    {
                        diagnostics.Add(CommandLineDiagnostic("--locale requires a value."));
                        break;
                    }

                    locale = args[index];
                    break;

                case "--start":
                    if (++index >= args.Count)
                    {
                        diagnostics.Add(CommandLineDiagnostic("--start requires a value."));
                        break;
                    }

                    start = args[index];
                    break;

                case "--fullscreen":
                    fullscreen = true;
                    break;

                case "--width":
                    if (!TryReadPositiveInt(args, ref index, "--width", diagnostics, out width))
                    {
                        break;
                    }

                    break;

                case "--height":
                    if (!TryReadPositiveInt(args, ref index, "--height", diagnostics, out height))
                    {
                        break;
                    }

                    break;

                case "--debug":
                    debug = true;
                    break;

                case "--profile":
                    profile = true;
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

        if (build && noBuild)
        {
            diagnostics.Add(CommandLineDiagnostic("--build cannot be combined with --no-build."));
        }

        if (diagnostics.Count > 0)
        {
            return CommandParseResult.Failure(outputFormat, diagnostics);
        }

        return CommandParseResult.RunSuccess(new RunCommandOptions(
            ProjectDirectory: positionalProject,
            OutputFormat: outputFormat,
            Target: target,
            BuildMode: build ? RunBuildMode.Always : noBuild ? RunBuildMode.Never : RunBuildMode.IfStale,
            Locale: locale,
            Start: start,
            Fullscreen: fullscreen,
            Width: width,
            Height: height,
            Debug: debug,
            Profile: profile,
            RuntimeArguments: runtimeArguments));
    }

    private static CommandParseResult ParsePublish(IReadOnlyList<string> args)
    {
        string? positionalProject = null;
        string? outputDirectory = null;
        string? locale = null;
        var target = "windows";
        var configuration = "release";
        var archive = "zip";
        var includeSource = false;
        var clean = false;
        var outputFormat = DiagnosticOutputFormat.Text;
        var diagnostics = new List<Diagnostic>();

        for (var index = 1; index < args.Count; index++)
        {
            var arg = args[index];
            switch (arg)
            {
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

                case "--configuration":
                    if (++index >= args.Count)
                    {
                        diagnostics.Add(CommandLineDiagnostic("--configuration requires a value."));
                        break;
                    }

                    configuration = args[index];
                    if (!string.Equals(configuration, "debug", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(configuration, "release", StringComparison.OrdinalIgnoreCase))
                    {
                        diagnostics.Add(CommandLineDiagnostic($"Invalid --configuration value '{configuration}'. Expected 'debug' or 'release'."));
                    }

                    break;

                case "--out-dir":
                    if (++index >= args.Count)
                    {
                        diagnostics.Add(CommandLineDiagnostic("--out-dir requires a value."));
                        break;
                    }

                    outputDirectory = args[index];
                    break;

                case "--archive":
                    if (++index >= args.Count)
                    {
                        diagnostics.Add(CommandLineDiagnostic("--archive requires a value."));
                        break;
                    }

                    archive = args[index];
                    if (!string.Equals(archive, "none", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(archive, "zip", StringComparison.OrdinalIgnoreCase))
                    {
                        diagnostics.Add(CommandLineDiagnostic($"Invalid --archive value '{archive}'. Expected 'none' or 'zip'."));
                    }

                    break;

                case "--include-source":
                    includeSource = true;
                    break;

                case "--locale":
                    if (++index >= args.Count)
                    {
                        diagnostics.Add(CommandLineDiagnostic("--locale requires a value."));
                        break;
                    }

                    locale = args[index];
                    break;

                case "--clean":
                    clean = true;
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

        return CommandParseResult.PublishSuccess(new PublishCommandOptions(
            positionalProject,
            outputFormat,
            target,
            configuration,
            outputDirectory,
            archive,
            includeSource,
            locale,
            clean));
    }

    private static Diagnostic CommandLineDiagnostic(string message)
    {
        return new Diagnostic(DiagnosticLevel.Error, "KES9001", string.Empty, 1, 1, message);
    }

    private static bool TryReadPositiveInt(
        IReadOnlyList<string> args,
        ref int index,
        string optionName,
        List<Diagnostic> diagnostics,
        out int? value)
    {
        if (++index >= args.Count)
        {
            value = null;
            diagnostics.Add(CommandLineDiagnostic($"{optionName} requires a value."));
            return false;
        }

        if (int.TryParse(args[index], out var parsed) && parsed > 0)
        {
            value = parsed;
            return true;
        }

        value = null;
        diagnostics.Add(CommandLineDiagnostic($"{optionName} requires a positive integer."));
        return false;
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
        PublishCommandOptions? PublishOptions,
        RunCommandOptions? RunOptions,
        IReadOnlyList<Diagnostic> Diagnostics,
        DiagnosticOutputFormat OutputFormat)
    {
        public static CommandParseResult BuildSuccess(BuildCommandOptions options)
        {
            return new CommandParseResult(options, null, null, null, null, null, [], options.OutputFormat);
        }

        public static CommandParseResult CorrectSuccess(CorrectCommandOptions options)
        {
            return new CommandParseResult(null, options, null, null, null, null, [], options.OutputFormat);
        }

        public static CommandParseResult InitSuccess(InitCommandOptions options)
        {
            return new CommandParseResult(null, null, options, null, null, null, [], options.OutputFormat);
        }

        public static CommandParseResult LocSuccess(LocCommandOptions options)
        {
            return new CommandParseResult(null, null, null, options, null, null, [], options.OutputFormat);
        }

        public static CommandParseResult PublishSuccess(PublishCommandOptions options)
        {
            return new CommandParseResult(null, null, null, null, options, null, [], options.OutputFormat);
        }

        public static CommandParseResult RunSuccess(RunCommandOptions options)
        {
            return new CommandParseResult(null, null, null, null, null, options, [], options.OutputFormat);
        }

        public static CommandParseResult Failure(DiagnosticOutputFormat format, params Diagnostic[] diagnostics)
        {
            return Failure(format, (IReadOnlyList<Diagnostic>)diagnostics);
        }

        public static CommandParseResult Failure(DiagnosticOutputFormat format, IReadOnlyList<Diagnostic> diagnostics)
        {
            return new CommandParseResult(null, null, null, null, null, null, diagnostics, format);
        }
    }
}
