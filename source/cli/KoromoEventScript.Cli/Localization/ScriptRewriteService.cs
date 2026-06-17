using System.Text;
using KoromoEventScript.Cli.Commands;
using KoromoEventScript.Cli.Diagnostics;
using KoromoEventScript.Cli.ProjectSystem;

namespace KoromoEventScript.Cli.Localization;

public sealed class ScriptRewriteService
{
    public ScriptRewriteResult Apply(ProjectConfig config, TagAssignmentPlan plan)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(plan);

        var changedFiles = new List<string>();
        foreach (var group in plan.Candidates.GroupBy(static candidate => candidate.ProjectRelativePath, StringComparer.Ordinal))
        {
            var absolutePath = Path.Combine(config.ProjectRoot, group.Key.Replace('/', Path.DirectorySeparatorChar));
            string source;
            try
            {
                source = File.ReadAllText(absolutePath);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return new ScriptRewriteResult(
                    CliExitCode.FileOrDirectoryError,
                    [new Diagnostic(DiagnosticLevel.Error, "KES9004", group.Key, 1, 1, $"Could not read input file: {exception.Message}")],
                    []);
            }

            var normalizedSource = source.Replace("\r\n", "\n");
            var lineStarts = ComputeLineStarts(normalizedSource);
            var edits = group
                .Select(candidate => new SourceTextEdit(
                    GetInsertionIndex(candidate, normalizedSource, lineStarts),
                    $" {candidate.Tag}"))
                .OrderByDescending(static edit => edit.Index)
                .ToArray();

            var builder = new StringBuilder(normalizedSource);
            foreach (var edit in edits)
            {
                builder.Insert(edit.Index, edit.Text);
            }

            try
            {
                File.WriteAllText(absolutePath, builder.ToString().Replace("\n", Environment.NewLine));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return new ScriptRewriteResult(
                    CliExitCode.FileOrDirectoryError,
                    [new Diagnostic(DiagnosticLevel.Error, "KES9004", group.Key, 1, 1, $"Could not write source file: {exception.Message}")],
                    changedFiles);
            }

            changedFiles.Add(group.Key);
        }

        return new ScriptRewriteResult(CliExitCode.Success, [], changedFiles);
    }

    private static int[] ComputeLineStarts(string source)
    {
        var starts = new List<int> { 0 };
        for (var index = 0; index < source.Length; index++)
        {
            if (source[index] == '\n')
            {
                starts.Add(index + 1);
            }
        }

        return starts.ToArray();
    }

    private static int GetInsertionIndex(TagAssignmentCandidate candidate, string source, int[] lineStarts)
    {
        var lineIndex = candidate.Line - 1;
        var start = lineStarts[lineIndex];
        var end = lineIndex + 1 < lineStarts.Length ? lineStarts[lineIndex + 1] - 1 : source.Length;
        var line = source[start..end];
        var colonIndex = line.LastIndexOf(':');
        if (colonIndex >= 0)
        {
            return start + colonIndex;
        }

        return start + line.Length;
    }
}
