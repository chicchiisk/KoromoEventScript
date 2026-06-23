using System.Diagnostics;

namespace KoromoEventScript.Cli.Commands.Run;

public interface IProcessLauncher
{
    int Launch(ProcessLaunchRequest request);
}

public sealed record ProcessLaunchRequest(
    string FileName,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory);

public sealed class ProcessLauncher : IProcessLauncher
{
    public int Launch(ProcessLaunchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var startInfo = new ProcessStartInfo
        {
            FileName = request.FileName,
            WorkingDirectory = request.WorkingDirectory,
            UseShellExecute = false,
        };

        foreach (var argument in request.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start Windows runtime process.");
        process.WaitForExit();
        return process.ExitCode;
    }
}
