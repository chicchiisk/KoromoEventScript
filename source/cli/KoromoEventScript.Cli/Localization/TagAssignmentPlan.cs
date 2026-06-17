namespace KoromoEventScript.Cli.Localization;

public sealed record TagAssignmentPlan(IReadOnlyList<TagAssignmentCandidate> Candidates)
{
    public bool HasChanges => Candidates.Count > 0;
}
