using KoromoEventScript.Cli.Commands.Build;
using KoromoEventScript.Cli.Commands.Clean;
using KoromoEventScript.Cli.Commands.Correct;
using KoromoEventScript.Cli.Commands.Init;
using KoromoEventScript.Cli.Commands.Loc;
using KoromoEventScript.Cli.Commands.Publish;
using KoromoEventScript.Cli.Commands.Run;
using KoromoEventScript.Cli.Diagnostics;
using KoromoEventScript.Cli.ProjectSystem;

namespace KoromoEventScript.Cli.Commands;

public sealed class CliApplication
{
    private const string CliVersion = "0.1.0";

    private readonly BuildCheckOnlyCommand buildCheckOnlyCommand;
    private readonly BuildCommand buildCommand;
    private readonly CleanService cleanService;
    private readonly CorrectCommand correctCommand;
    private readonly InitCommand initCommand;
    private readonly LocCommand locCommand;
    private readonly WindowsPublishCommand publishCommand;
    private readonly RunCommand runCommand;
    private readonly DiagnosticSink diagnosticSink;

    public CliApplication()
        : this(new BuildCheckOnlyCommand(), new BuildCommand(), new CleanService(), new CorrectCommand(), new InitCommand(), new LocCommand(), new WindowsPublishCommand(), new RunCommand(), new DiagnosticSink())
    {
    }

    public CliApplication(
        BuildCheckOnlyCommand buildCheckOnlyCommand,
        BuildCommand buildCommand,
        CorrectCommand correctCommand,
        InitCommand initCommand,
        LocCommand locCommand,
        DiagnosticSink diagnosticSink)
        : this(buildCheckOnlyCommand, buildCommand, new CleanService(), correctCommand, initCommand, locCommand, new WindowsPublishCommand(), new RunCommand(), diagnosticSink)
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
        : this(buildCheckOnlyCommand, buildCommand, new CleanService(), correctCommand, initCommand, locCommand, publishCommand, runCommand, diagnosticSink)
    {
    }

    public CliApplication(
        BuildCheckOnlyCommand buildCheckOnlyCommand,
        BuildCommand buildCommand,
        CleanService cleanService,
        CorrectCommand correctCommand,
        InitCommand initCommand,
        LocCommand locCommand,
        WindowsPublishCommand publishCommand,
        RunCommand runCommand,
        DiagnosticSink diagnosticSink)
    {
        this.buildCheckOnlyCommand = buildCheckOnlyCommand;
        this.buildCommand = buildCommand;
        this.cleanService = cleanService;
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
        if (parseResult.StandardOutput is not null)
        {
            output.WriteLine(parseResult.StandardOutput);
            return (int)CliExitCode.Success;
        }

        if (parseResult.Diagnostics.Count > 0 || (parseResult.BuildOptions is null && parseResult.CleanOptions is null && parseResult.InitOptions is null && parseResult.CorrectOptions is null && parseResult.LocOptions is null && parseResult.PublishOptions is null && parseResult.RunOptions is null))
        {
            diagnosticSink.Write(parseResult.Diagnostics, parseResult.OutputFormat, error);
            return (int)CliExitCode.CommandLineError;
        }

        if (parseResult.CleanOptions is not null)
        {
            return RunClean(parseResult.CleanOptions, output, error, currentDirectory);
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

    private int RunClean(CleanCommandOptions options, TextWriter output, TextWriter error, string currentDirectory)
    {
        var rootResult = new ProjectRootResolver().Resolve(options.ProjectDirectory, currentDirectory);
        if (!rootResult.Succeeded)
        {
            diagnosticSink.Write([rootResult.Diagnostic!], options.OutputFormat, error);
            return (int)CliExitCode.FileOrDirectoryError;
        }

        var configResult = new ProjectConfigLoader().Load(rootResult.ProjectRoot!);
        if (!configResult.Succeeded)
        {
            diagnosticSink.Write([configResult.Diagnostic!], options.OutputFormat, error);
            return (int)CliExitCode.FileOrDirectoryError;
        }

        var cleanResult = cleanService.Execute(configResult.Config!, options);
        diagnosticSink.Write(cleanResult.Diagnostics, options.OutputFormat, error);
        if (cleanResult.ExitCode == CliExitCode.Success && options.DryRun)
        {
            foreach (var path in cleanResult.DeletedPaths)
            {
                output.WriteLine(path);
            }
        }

        return (int)cleanResult.ExitCode;
    }

    private static CommandParseResult Parse(IReadOnlyList<string> args)
    {
        if (args.Count == 0)
        {
            return CommandParseResult.Failure(
                DiagnosticOutputFormat.Text,
                CommandLineDiagnostic("Unsupported command. Only 'build', 'clean', 'correct', 'init', 'loc', 'publish', and 'run' are supported."));
        }

        if (args.Count == 1 && IsVersionOption(args[0]))
        {
            return CommandParseResult.Output($"kes {CliVersion}");
        }

        if (args.Count == 1 && IsHelpOption(args[0]))
        {
            return CommandParseResult.Output(TopLevelHelpText());
        }

        if (HasCommandHelp(args))
        {
            return args[0] switch
            {
                "build" => CommandParseResult.Output(BuildHelpText()),
                "clean" => CommandParseResult.Output(CleanHelpText()),
                "correct" => CommandParseResult.Output(CorrectHelpText()),
                "init" => CommandParseResult.Output(InitHelpText()),
                "loc" => CommandParseResult.Output(LocHelpText()),
                "publish" => CommandParseResult.Output(PublishHelpText()),
                "run" => CommandParseResult.Output(RunHelpText()),
                _ => CommandParseResult.Failure(
                    DiagnosticOutputFormat.Text,
                    CommandLineDiagnostic("Unsupported command. Only 'build', 'clean', 'correct', 'init', 'loc', 'publish', and 'run' are supported.")),
            };
        }

        return args[0] switch
        {
            "build" => ParseBuild(args),
            "clean" => ParseClean(args),
            "correct" => ParseCorrect(args),
            "init" => ParseInit(args),
            "loc" => ParseLoc(args),
            "publish" => ParsePublish(args),
            "run" => ParseRun(args),
            _ => CommandParseResult.Failure(
                DiagnosticOutputFormat.Text,
                CommandLineDiagnostic("Unsupported command. Only 'build', 'clean', 'correct', 'init', 'loc', 'publish', and 'run' are supported.")),
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

                case "--verbose":
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
            Locale: locale));
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

                case "--verbose":
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

                case "--verbose":
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

                case "--verbose":
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

                case "--verbose":
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

                case "--verbose":
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

    private static CommandParseResult ParseClean(IReadOnlyList<string> args)
    {
        string? positionalProject = null;
        string? target = null;
        var includeDist = false;
        var dryRun = false;
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
                    if (!IsBuildTarget(target))
                    {
                        diagnostics.Add(CommandLineDiagnostic($"Unsupported --target value '{target}'. Expected 'windows', 'unity', or 'unreal'."));
                    }
                    else
                    {
                        target = target.ToLowerInvariant();
                    }

                    break;

                case "--dist":
                    includeDist = true;
                    break;

                case "--dry-run":
                    dryRun = true;
                    break;

                case "--verbose":
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

        return CommandParseResult.CleanSuccess(new CleanCommandOptions(
            positionalProject,
            outputFormat,
            target,
            includeDist,
            dryRun));
    }

    private static bool IsHelpOption(string arg)
    {
        return string.Equals(arg, "-h", StringComparison.Ordinal) ||
            string.Equals(arg, "--help", StringComparison.Ordinal);
    }

    private static bool IsVersionOption(string arg)
    {
        return string.Equals(arg, "-v", StringComparison.Ordinal) ||
            string.Equals(arg, "--version", StringComparison.Ordinal);
    }

    private static bool IsBuildTarget(string target)
    {
        return string.Equals(target, "windows", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(target, "unity", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(target, "unreal", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasCommandHelp(IReadOnlyList<string> args)
    {
        for (var index = 1; index < args.Count; index++)
        {
            if (args[index] == "--")
            {
                return false;
            }

            if (IsHelpOption(args[index]))
            {
                return true;
            }
        }

        return false;
    }

    private static string TopLevelHelpText()
    {
        return """
KoromoEventScript CLI Tool

Usage:
  kes -v|--version
  kes -h|--help
  kes <COMMAND> [-h|--help] [command-options] [arguments]

Commands:
  init      Create a new KES project
  correct   Complete localization tags and rewrite scripts
  loc       Generate a localization dictionary CSV template
  build     Validate and compile project scripts
  clean     Remove build artifacts and temporary files
  run       Run a project with the standalone runtime
  publish   Generate distributable artifacts

Common Options:
  -h, --help      Show help and exit
  -v, --version   Show version and exit
  --verbose       Enable verbose logs

Examples:
  kes --version
  kes build --help
  kes run . --debug
""";
    }

    private static string InitHelpText()
    {
        return """
Create a new KES project.

Usage:
  kes init [PROJECT_DIR] [options]

Options:
  --name <NAME>                Set project name
  --template <basic|empty>     Select project template
  --force                      Allow overwriting existing files
  --no-sample                  Do not generate sample .kc/.kel files
  --verbose                    Enable verbose logs
  -h, --help                   Show help and exit

Examples:
  kes init MyGame --name "MyGame"
  kes init . --template empty
""";
    }

    private static string CorrectHelpText()
    {
        return """
Complete localization tags and rewrite scripts.

Usage:
  kes correct [PROJECT_DIR] [options]

Options:
  --entry <PATH_TO_EVENT_LIST>   Set entry .kel file
  --check-only                   Preview changes without rewriting files
  --verbose                      Enable verbose logs
  -h, --help                     Show help and exit

Examples:
  kes correct
  kes correct --entry events/main.kel
  kes correct --check-only
""";
    }

    private static string LocHelpText()
    {
        return """
Generate a localization dictionary CSV template.

Usage:
  kes loc [PROJECT_DIR] [options]

Options:
  --locale <LOCALE_LIST>        Output locales, separated by commas
  --out <PATH_TO_CSV>           Output CSV path
  --verbose                     Enable verbose logs
  -h, --help                    Show help and exit

Examples:
  kes loc
  kes loc --locale jp,en,fr
  kes loc --out translations/messages.csv
""";
    }

    private static string BuildHelpText()
    {
        return """
Validate and compile project scripts.

Usage:
  kes build [PROJECT_DIR] [options]

Options:
  --target <windows|unity|unreal>   Set output target
  --entry <PATH_TO_EVENT_LIST>      Set entry .kel file
  --out-dir <DIR>                   Set build output directory
  --loc <LOCALE>                    Build localized output
  --warnings-as-errors              Treat warnings as errors
  --txt-il                          Also emit .klibtxt
  --check-only                      Validate without writing artifacts
  --verbose                         Enable verbose logs
  -h, --help                        Show help and exit

Examples:
  kes build
  kes build --target windows --warnings-as-errors
  kes build --entry events/main.kel
  kes build --loc en
  kes build --txt-il
""";
    }

    private static string RunHelpText()
    {
        return """
Run a project with the standalone runtime.

Usage:
  kes run [PROJECT_DIR] [options] [-- runtime-arguments]

Options:
  --target <windows>   Select standalone runtime
  --build              Build before running
  --no-build           Use existing build artifacts
  --debug              Enable runtime debug information
  --locale <LOCALE>    Set runtime locale
  --start <TAG>        Start from label or tag
  --fullscreen         Start fullscreen
  --width <NUMBER>     Set window width
  --height <NUMBER>    Set window height
  --verbose            Enable verbose logs
  -h, --help           Show help and exit

Examples:
  kes run
  kes run . --debug
  kes run testdata/projects/full-command-sample --start "#se_sample_0002" -- --profile
""";
    }

    private static string CleanHelpText()
    {
        return """
Remove build artifacts and temporary files.

Usage:
  kes clean [PROJECT_DIR] [options]

Options:
  --target <windows|unity|unreal>   Remove artifacts for one target
  --dist                            Also remove dist output
  --dry-run                         Print targets without deleting
  --verbose                         Enable verbose logs
  -h, --help                        Show help and exit

Examples:
  kes clean
  kes clean --target windows
  kes clean --dist --dry-run
""";
    }

    private static string PublishHelpText()
    {
        return """
Generate distributable artifacts.

Usage:
  kes publish [PROJECT_DIR] [options]

Options:
  --target <windows|unity|unreal>      Set publish target
  --configuration <debug|release>      Set publish configuration
  --out-dir <DIR>                      Set publish output directory
  --archive <none|zip>                 Set archive format
  --include-source                     Include .kc/.kel source files
  --locale <LOCALE>                    Set publish locale
  --clean                              Clean before publishing
  --verbose                            Enable verbose logs
  -h, --help                           Show help and exit

Examples:
  kes publish
  kes publish --target windows --configuration release
  kes publish --out-dir releases --archive zip
""";
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
        CleanCommandOptions? CleanOptions,
        CorrectCommandOptions? CorrectOptions,
        InitCommandOptions? InitOptions,
        LocCommandOptions? LocOptions,
        PublishCommandOptions? PublishOptions,
        RunCommandOptions? RunOptions,
        IReadOnlyList<Diagnostic> Diagnostics,
        DiagnosticOutputFormat OutputFormat,
        string? StandardOutput = null)
    {
        public static CommandParseResult Output(string standardOutput)
        {
            return new CommandParseResult(null, null, null, null, null, null, null, [], DiagnosticOutputFormat.Text, standardOutput);
        }

        public static CommandParseResult BuildSuccess(BuildCommandOptions options)
        {
            return new CommandParseResult(options, null, null, null, null, null, null, [], options.OutputFormat);
        }

        public static CommandParseResult CleanSuccess(CleanCommandOptions options)
        {
            return new CommandParseResult(null, options, null, null, null, null, null, [], options.OutputFormat);
        }

        public static CommandParseResult CorrectSuccess(CorrectCommandOptions options)
        {
            return new CommandParseResult(null, null, options, null, null, null, null, [], options.OutputFormat);
        }

        public static CommandParseResult InitSuccess(InitCommandOptions options)
        {
            return new CommandParseResult(null, null, null, options, null, null, null, [], options.OutputFormat);
        }

        public static CommandParseResult LocSuccess(LocCommandOptions options)
        {
            return new CommandParseResult(null, null, null, null, options, null, null, [], options.OutputFormat);
        }

        public static CommandParseResult PublishSuccess(PublishCommandOptions options)
        {
            return new CommandParseResult(null, null, null, null, null, options, null, [], options.OutputFormat);
        }

        public static CommandParseResult RunSuccess(RunCommandOptions options)
        {
            return new CommandParseResult(null, null, null, null, null, null, options, [], options.OutputFormat);
        }

        public static CommandParseResult Failure(DiagnosticOutputFormat format, params Diagnostic[] diagnostics)
        {
            return Failure(format, (IReadOnlyList<Diagnostic>)diagnostics);
        }

        public static CommandParseResult Failure(DiagnosticOutputFormat format, IReadOnlyList<Diagnostic> diagnostics)
        {
            return new CommandParseResult(null, null, null, null, null, null, null, diagnostics, format);
        }
    }
}
