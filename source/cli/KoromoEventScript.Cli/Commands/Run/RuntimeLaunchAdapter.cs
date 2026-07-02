using System.Globalization;
using System.Text;

namespace KoromoEventScript.Cli.Commands.Run;

public sealed class RuntimeLaunchAdapter
{
    public ProcessLaunchRequest Create(
        string runtimeCommandPath,
        string manifestPath,
        RunCommandOptions options,
        string currentDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runtimeCommandPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentDirectory);

        var runtimeArguments = BuildRuntimeArguments(options, manifestPath);
        if (string.Equals(Path.GetExtension(runtimeCommandPath), ".csproj", StringComparison.OrdinalIgnoreCase))
        {
            return new ProcessLaunchRequest(
                "dotnet",
                [
                    "run",
                    "--project",
                    runtimeCommandPath,
                    "--no-launch-profile",
                    "--",
                    "--args",
                    SerializeRuntimeArguments(runtimeArguments),
                ],
                Path.GetDirectoryName(runtimeCommandPath)!);
        }

        return new ProcessLaunchRequest(
            runtimeCommandPath,
            runtimeArguments,
            GetRuntimeWorkingDirectory(runtimeCommandPath, currentDirectory));
    }

    public static IReadOnlyList<string> BuildRuntimeArguments(RunCommandOptions options, string manifestPath)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);

        var arguments = new List<string>
        {
            "--manifest",
            manifestPath,
        };

        AddValue(arguments, "--locale", options.Locale);
        AddValue(arguments, "--start", options.Start);
        if (options.Fullscreen)
        {
            arguments.Add("--fullscreen");
        }

        AddValue(arguments, "--width", options.Width?.ToString(CultureInfo.InvariantCulture));
        AddValue(arguments, "--height", options.Height?.ToString(CultureInfo.InvariantCulture));
        if (options.Debug)
        {
            arguments.Add("--debug");
        }

        if (options.Profile)
        {
            arguments.Add("--profile");
        }

        arguments.AddRange(options.RuntimeArguments ?? []);
        return arguments;
    }

    public static string SerializeRuntimeArguments(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        return string.Join(" ", arguments.Select(QuoteArgument));
    }

    private static void AddValue(List<string> arguments, string name, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        arguments.Add(name);
        arguments.Add(value);
    }

    private static string QuoteArgument(string argument)
    {
        var builder = new StringBuilder(argument.Length + 2);
        builder.Append('"');
        var backslashCount = 0;
        foreach (var character in argument)
        {
            if (character == '\\')
            {
                backslashCount++;
                continue;
            }

            if (character == '"')
            {
                builder.Append('\\', (backslashCount * 2) + 1);
                builder.Append('"');
                backslashCount = 0;
                continue;
            }

            if (backslashCount > 0)
            {
                builder.Append('\\', backslashCount);
                backslashCount = 0;
            }

            builder.Append(character);
        }

        if (backslashCount > 0)
        {
            builder.Append('\\', backslashCount * 2);
        }

        builder.Append('"');
        return builder.ToString();
    }

    private static string GetRuntimeWorkingDirectory(string runtimeCommandPath, string currentDirectory)
    {
        var runtimeDirectory = Path.GetDirectoryName(runtimeCommandPath);
        return string.IsNullOrEmpty(runtimeDirectory) ? currentDirectory : runtimeDirectory;
    }
}
