using KoromoEventScript.Cli.Parsing;
using KoromoEventScript.Cli.Semantics;

namespace KoromoEventScript.Cli.Localization;

public sealed record LocalizationSourceEntry(
    string Tag,
    string Speaker,
    string Original);

public sealed class LocalizationTextExtractor
{
    public IReadOnlyList<LocalizationSourceEntry> Extract(
        IReadOnlyList<ScriptDocument> orderedDocuments,
        TagAssignmentPlan tagPlan)
    {
        ArgumentNullException.ThrowIfNull(orderedDocuments);
        ArgumentNullException.ThrowIfNull(tagPlan);

        var planLookup = tagPlan.Candidates
            .GroupBy(static candidate => candidate.ProjectRelativePath, StringComparer.Ordinal)
            .ToDictionary(
                static group => group.Key,
                static group => group.ToDictionary(static candidate => (candidate.Line, candidate.Kind), static candidate => candidate.Tag),
                StringComparer.Ordinal);

        var results = new List<LocalizationSourceEntry>();
        foreach (var document in orderedDocuments)
        {
            var tagsByLocation = planLookup.TryGetValue(document.ProjectRelativePath, out var candidates)
                ? candidates
                : new Dictionary<(int Line, TagAssignmentKind Kind), string>();

            foreach (var statement in document.Syntax.Statements)
            {
                VisitStatement(statement, tagsByLocation, results);
            }
        }

        return results;
    }

    private static void VisitStatement(
        StatementSyntax statement,
        IReadOnlyDictionary<(int Line, TagAssignmentKind Kind), string> tagsByLocation,
        List<LocalizationSourceEntry> results)
    {
        switch (statement)
        {
            case SayStatementSyntax say:
                var sayTag = ResolveTag(say.Tag, say.SpeakerLocation.Line, TagAssignmentKind.Say, tagsByLocation);
                if (!string.IsNullOrWhiteSpace(sayTag))
                {
                    results.Add(new LocalizationSourceEntry(
                        NormalizeTag(sayTag!),
                        say.Speaker,
                        JoinLines(say.Lines)));
                }

                break;

            case NarStatementSyntax nar:
                var narTag = ResolveTag(nar.Tag, nar.KeywordLocation.Line, TagAssignmentKind.Nar, tagsByLocation);
                if (!string.IsNullOrWhiteSpace(narTag))
                {
                    results.Add(new LocalizationSourceEntry(
                        NormalizeTag(narTag!),
                        string.Empty,
                        JoinLines(nar.Lines)));
                }

                break;

            case SelectStatementSyntax select:
                var selectTag = ResolveTag(select.Tag, select.KeywordLocation.Line, TagAssignmentKind.Select, tagsByLocation);
                if (string.IsNullOrWhiteSpace(selectTag))
                {
                    break;
                }

                var normalizedSelectTag = NormalizeTag(selectTag!);
                for (var index = 0; index < select.Cases.Count; index++)
                {
                    var @case = select.Cases[index];
                    results.Add(new LocalizationSourceEntry(
                        $"{normalizedSelectTag}_c{index:00}",
                        string.Empty,
                        @case.Text));
                }

                break;

            case IfStatementSyntax ifStatement:
                VisitBlock(ifStatement.Body, tagsByLocation, results);
                foreach (var elseIfClause in ifStatement.ElseIfClauses)
                {
                    VisitBlock(elseIfClause.Body, tagsByLocation, results);
                }

                if (ifStatement.ElseBody is not null)
                {
                    VisitBlock(ifStatement.ElseBody, tagsByLocation, results);
                }

                break;

            case WhileStatementSyntax whileStatement:
                VisitBlock(whileStatement.Body, tagsByLocation, results);
                break;

            case ForStatementSyntax forStatement:
                VisitBlock(forStatement.Body, tagsByLocation, results);
                break;

            case FunctionDeclarationSyntax function:
                VisitBlock(function.Body, tagsByLocation, results);
                break;

            case ActorDeclarationSyntax actor:
                VisitBlock(actor.Body, tagsByLocation, results);
                break;

            case ClassDeclarationSyntax classDeclaration:
                foreach (var method in classDeclaration.Members.OfType<ClassMethodSyntax>())
                {
                    VisitBlock(method.Declaration.Body, tagsByLocation, results);
                }

                break;
        }
    }

    private static void VisitBlock(
        BlockSyntax block,
        IReadOnlyDictionary<(int Line, TagAssignmentKind Kind), string> tagsByLocation,
        List<LocalizationSourceEntry> results)
    {
        foreach (var statement in block.Statements)
        {
            VisitStatement(statement, tagsByLocation, results);
        }
    }

    private static string? ResolveTag(
        string? existingTag,
        int line,
        TagAssignmentKind kind,
        IReadOnlyDictionary<(int Line, TagAssignmentKind Kind), string> tagsByLocation)
    {
        if (!string.IsNullOrWhiteSpace(existingTag))
        {
            return existingTag;
        }

        return tagsByLocation.TryGetValue((line, kind), out var plannedTag)
            ? plannedTag
            : null;
    }

    private static string JoinLines(IReadOnlyList<TextLineSyntax> lines)
    {
        return string.Join('\n', lines.Select(static line => line.Text));
    }

    private static string NormalizeTag(string tag)
    {
        return tag.StartsWith('#') ? tag[1..] : tag;
    }
}
