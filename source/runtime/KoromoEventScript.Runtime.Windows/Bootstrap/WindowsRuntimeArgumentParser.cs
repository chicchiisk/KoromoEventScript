using System.Globalization;
using System.Text;
using KoromoEventScript.Runtime.Core.Diagnostics;

namespace KoromoEventScript.Runtime.Windows.Bootstrap;

public static class WindowsRuntimeArgumentParser
{
    public static WindowsRuntimeBootstrapResult Parse(string commandLine, string baseDirectory)
    {
        return Parse(Tokenize(commandLine), baseDirectory);
    }

    public static WindowsRuntimeBootstrapResult Parse(IReadOnlyList<string> args, string baseDirectory)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);

        string? manifestPath = null;
        string? locale = null;
        string? start = null;
        var fullscreen = false;
        int? width = null;
        int? height = null;
        var debug = false;
        var profile = false;

        for (var index = 0; index < args.Count; index++)
        {
            var argument = args[index];
            switch (argument)
            {
                case "--manifest":
                    if (!TryReadValue(args, ref index, argument, out manifestPath, out var manifestFailure))
                    {
                        return manifestFailure;
                    }

                    break;

                case "--locale":
                    if (!TryReadValue(args, ref index, argument, out locale, out var localeFailure))
                    {
                        return localeFailure;
                    }

                    break;

                case "--start":
                    if (!TryReadValue(args, ref index, argument, out start, out var startFailure))
                    {
                        return startFailure;
                    }

                    break;

                case "--fullscreen":
                    fullscreen = true;
                    break;

                case "--width":
                    if (!TryReadPositiveInt(args, ref index, argument, out width, out var widthFailure))
                    {
                        return widthFailure;
                    }

                    break;

                case "--height":
                    if (!TryReadPositiveInt(args, ref index, argument, out height, out var heightFailure))
                    {
                        return heightFailure;
                    }

                    break;

                case "--debug":
                    debug = true;
                    break;

                case "--profile":
                    profile = true;
                    break;

                default:
                    return ArgumentFailure($"Unknown Windows runtime argument: {argument}");
            }
        }

        var resolvedManifest = string.IsNullOrWhiteSpace(manifestPath)
            ? DiscoverDefaultManifest(baseDirectory)
            : ResolveExplicitManifestPath(baseDirectory, manifestPath);
        if (resolvedManifest is null)
        {
            return StartupFailure("Runtime manifest was not found in data/manifest.json or manifest.json.");
        }

        if (!File.Exists(resolvedManifest))
        {
            return StartupFailure($"Runtime manifest was not found: {resolvedManifest}");
        }

        return WindowsRuntimeBootstrapResult.Success(
            new WindowsRuntimeOptions(
                resolvedManifest,
                locale,
                start,
                fullscreen,
                width,
                height,
                debug,
                profile));
    }

    private static IReadOnlyList<string> Tokenize(string commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return [];
        }

        var tokens = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;
        for (var index = 0; index < commandLine.Length; index++)
        {
            var character = commandLine[index];
            if (character == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (char.IsWhiteSpace(character) && !inQuotes)
            {
                AddCurrentToken(tokens, current);
                continue;
            }

            current.Append(character);
        }

        AddCurrentToken(tokens, current);
        return tokens;
    }

    private static void AddCurrentToken(List<string> tokens, StringBuilder current)
    {
        if (current.Length == 0)
        {
            return;
        }

        tokens.Add(current.ToString());
        current.Clear();
    }

    private static bool TryReadValue(
        IReadOnlyList<string> args,
        ref int index,
        string optionName,
        out string? value,
        out WindowsRuntimeBootstrapResult failure)
    {
        if (index + 1 >= args.Count || args[index + 1].StartsWith("--", StringComparison.Ordinal))
        {
            value = null;
            failure = ArgumentFailure($"Windows runtime argument '{optionName}' requires a value.");
            return false;
        }

        index++;
        value = args[index];
        failure = null!;
        return true;
    }

    private static bool TryReadPositiveInt(
        IReadOnlyList<string> args,
        ref int index,
        string optionName,
        out int? value,
        out WindowsRuntimeBootstrapResult failure)
    {
        if (!TryReadValue(args, ref index, optionName, out var text, out failure))
        {
            value = null;
            return false;
        }

        if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) || parsed <= 0)
        {
            value = null;
            failure = ArgumentFailure($"Windows runtime argument '{optionName}' requires a positive integer.");
            return false;
        }

        value = parsed;
        return true;
    }

    private static string? DiscoverDefaultManifest(string baseDirectory)
    {
        var normalizedBase = Path.GetFullPath(baseDirectory);
        foreach (var candidate in new[] { Path.Combine(normalizedBase, "data", "manifest.json"), Path.Combine(normalizedBase, "manifest.json") })
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string ResolveExplicitManifestPath(string baseDirectory, string path)
    {
        if (Path.IsPathRooted(path))
        {
            return Path.GetFullPath(path);
        }

        var currentDirectoryPath = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, path));
        if (File.Exists(currentDirectoryPath))
        {
            return currentDirectoryPath;
        }

        return Path.GetFullPath(Path.Combine(baseDirectory, path));
    }

    private static WindowsRuntimeBootstrapResult ArgumentFailure(string message)
    {
        return WindowsRuntimeBootstrapResult.Failure(
            RuntimeFailureKind.Argument,
            RuntimeDiagnostic.Error("KESR9001", message, RuntimeFailureKind.Argument));
    }

    private static WindowsRuntimeBootstrapResult StartupFailure(string message)
    {
        return WindowsRuntimeBootstrapResult.Failure(
            RuntimeFailureKind.Startup,
            RuntimeDiagnostic.Error("KESR9002", message, RuntimeFailureKind.Startup));
    }
}
