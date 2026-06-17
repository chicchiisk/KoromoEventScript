namespace KoromoEventScript.Cli.Localization;

public sealed class CorrectPreviewFormatter
{
    public string Format(TagAssignmentPlan plan)
    {
        if (!plan.HasChanges)
        {
            return "No tag updates required.";
        }

        return string.Join(
            Environment.NewLine,
            plan.Candidates.Select(candidate =>
                $"{candidate.ProjectRelativePath}:{candidate.Line}:{candidate.Column} {candidate.Kind} -> {candidate.Tag}"));
    }
}
