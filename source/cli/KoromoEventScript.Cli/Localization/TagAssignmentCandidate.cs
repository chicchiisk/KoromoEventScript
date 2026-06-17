using KoromoEventScript.Cli.Parsing;

namespace KoromoEventScript.Cli.Localization;

public enum TagAssignmentKind
{
    Say,
    Nar,
    Select,
}

public sealed record TagAssignmentCandidate(
    string ProjectRelativePath,
    TagAssignmentKind Kind,
    int Line,
    int Column,
    int InsertionColumn,
    string Tag);
