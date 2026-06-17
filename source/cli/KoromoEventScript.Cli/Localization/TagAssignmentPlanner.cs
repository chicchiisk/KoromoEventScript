using KoromoEventScript.Cli.ProjectSystem;
using KoromoEventScript.Cli.Semantics;
using KoromoEventScript.Cli.Parsing;

namespace KoromoEventScript.Cli.Localization;

public sealed class TagAssignmentPlanner
{
    public TagAssignmentPlan BuildPlan(ProjectConfig config, IReadOnlyList<ScriptDocument> orderedDocuments)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(orderedDocuments);

        var candidates = new List<TagAssignmentCandidate>();
        foreach (var document in orderedDocuments)
        {
            var normalizedFileName = AutoTagPattern.NormalizeScriptFileName(document.ProjectRelativePath);
            var reservedNumbers = CollectReservedNumbers(document.Syntax, normalizedFileName);
            var nextNumber = 1;

            foreach (var statement in document.Syntax.Statements)
            {
                VisitStatement(statement, document.ProjectRelativePath);
            }

            void VisitStatement(StatementSyntax statement, string projectRelativePath)
            {
                switch (statement)
                {
                    case SayStatementSyntax say when string.IsNullOrWhiteSpace(say.Tag):
                        candidates.Add(new TagAssignmentCandidate(
                            projectRelativePath,
                            TagAssignmentKind.Say,
                            say.SpeakerLocation.Line,
                            say.SpeakerLocation.Column,
                            FindSayInsertionColumn(say),
                            Allocate(TagAssignmentKind.Say)));
                        break;

                    case NarStatementSyntax nar when string.IsNullOrWhiteSpace(nar.Tag):
                        candidates.Add(new TagAssignmentCandidate(
                            projectRelativePath,
                            TagAssignmentKind.Nar,
                            nar.KeywordLocation.Line,
                            nar.KeywordLocation.Column,
                            4,
                            Allocate(TagAssignmentKind.Nar)));
                        break;

                    case SelectStatementSyntax select when string.IsNullOrWhiteSpace(select.Tag):
                        candidates.Add(new TagAssignmentCandidate(
                            projectRelativePath,
                            TagAssignmentKind.Select,
                            select.KeywordLocation.Line,
                            select.KeywordLocation.Column,
                            7,
                            Allocate(TagAssignmentKind.Select)));
                        break;

                    case IfStatementSyntax ifStatement:
                        VisitBlock(ifStatement.Body, projectRelativePath);
                        foreach (var elseIfClause in ifStatement.ElseIfClauses)
                        {
                            VisitBlock(elseIfClause.Body, projectRelativePath);
                        }

                        if (ifStatement.ElseBody is not null)
                        {
                            VisitBlock(ifStatement.ElseBody, projectRelativePath);
                        }

                        break;

                    case WhileStatementSyntax whileStatement:
                        VisitBlock(whileStatement.Body, projectRelativePath);
                        break;

                    case ForStatementSyntax forStatement:
                        VisitBlock(forStatement.Body, projectRelativePath);
                        break;

                    case FunctionDeclarationSyntax function:
                        VisitBlock(function.Body, projectRelativePath);
                        break;

                    case ActorDeclarationSyntax actor:
                        VisitBlock(actor.Body, projectRelativePath);
                        break;

                    case ClassDeclarationSyntax classDeclaration:
                        foreach (var member in classDeclaration.Members.OfType<ClassMethodSyntax>())
                        {
                            VisitBlock(member.Declaration.Body, projectRelativePath);
                        }

                        break;
                }
            }

            void VisitBlock(BlockSyntax block, string projectRelativePath)
            {
                foreach (var child in block.Statements)
                {
                    VisitStatement(child, projectRelativePath);
                }
            }

            string Allocate(TagAssignmentKind kind)
            {
                while (!reservedNumbers.Add(nextNumber))
                {
                    nextNumber++;
                }

                var assigned = nextNumber++;
                return AutoTagPattern.CreateTag(AutoTagPattern.GetPrefix(kind), normalizedFileName, assigned);
            }
        }

        return new TagAssignmentPlan(candidates);
    }

    private static HashSet<int> CollectReservedNumbers(ScriptSyntax syntax, string normalizedFileName)
    {
        var reserved = new HashSet<int>();
        foreach (var tag in EnumerateTags(syntax))
        {
            if (AutoTagPattern.TryParseNumber(tag, normalizedFileName, out var number))
            {
                reserved.Add(number);
            }
        }

        return reserved;
    }

    private static IEnumerable<string> EnumerateTags(ScriptSyntax syntax)
    {
        foreach (var statement in syntax.Statements)
        {
            foreach (var tag in EnumerateTags(statement))
            {
                yield return tag;
            }
        }
    }

    private static IEnumerable<string> EnumerateTags(StatementSyntax statement)
    {
        switch (statement)
        {
            case SayStatementSyntax { Tag: { Length: > 0 } tag }:
                yield return tag;
                yield break;
            case NarStatementSyntax { Tag: { Length: > 0 } tag }:
                yield return tag;
                yield break;
            case SelectStatementSyntax { Tag: { Length: > 0 } tag }:
                yield return tag;
                yield break;
            case IfStatementSyntax ifStatement:
                foreach (var child in ifStatement.Body.Statements.SelectMany(EnumerateTags))
                {
                    yield return child;
                }

                foreach (var child in ifStatement.ElseIfClauses.SelectMany(static clause => clause.Body.Statements).SelectMany(EnumerateTags))
                {
                    yield return child;
                }

                if (ifStatement.ElseBody is not null)
                {
                    foreach (var child in ifStatement.ElseBody.Statements.SelectMany(EnumerateTags))
                    {
                        yield return child;
                    }
                }

                yield break;
            case WhileStatementSyntax whileStatement:
                foreach (var child in whileStatement.Body.Statements.SelectMany(EnumerateTags))
                {
                    yield return child;
                }

                yield break;
            case ForStatementSyntax forStatement:
                foreach (var child in forStatement.Body.Statements.SelectMany(EnumerateTags))
                {
                    yield return child;
                }

                yield break;
            case FunctionDeclarationSyntax function:
                foreach (var child in function.Body.Statements.SelectMany(EnumerateTags))
                {
                    yield return child;
                }

                yield break;
            case ActorDeclarationSyntax actor:
                foreach (var child in actor.Body.Statements.SelectMany(EnumerateTags))
                {
                    yield return child;
                }

                yield break;
            case ClassDeclarationSyntax classDeclaration:
                foreach (var method in classDeclaration.Members.OfType<ClassMethodSyntax>())
                {
                    foreach (var child in method.Declaration.Body.Statements.SelectMany(EnumerateTags))
                    {
                        yield return child;
                    }
                }

                yield break;
        }
    }

    private static int FindSayInsertionColumn(SayStatementSyntax say)
    {
        var speakerWidth = say.Speaker.Length + 4;
        return speakerWidth;
    }
}
