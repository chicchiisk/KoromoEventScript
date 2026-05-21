using KoromoEventScript.Cli.Lexing;

namespace KoromoEventScript.Cli.Parsing;

public sealed record ScriptSyntax(IReadOnlyList<StatementSyntax> Statements);

public abstract record StatementSyntax;

public sealed record ImportStatementSyntax(string ModuleName) : StatementSyntax;

public sealed record VarStatementSyntax(
    string Name,
    IReadOnlyList<Token> TypeTokens,
    IReadOnlyList<Token> ValueTokens) : StatementSyntax;

public sealed record LabelStatementSyntax(string Tag) : StatementSyntax;

public sealed record JumpStatementSyntax(string Tag) : StatementSyntax;

public sealed record CommandStatementSyntax(
    string Name,
    IReadOnlyList<Token> Arguments) : StatementSyntax;

public abstract record LessBlockItemSyntax;

public sealed record LessStatementSyntax(
    string Name,
    IReadOnlyList<Token> SharedArguments,
    IReadOnlyList<LessBlockItemSyntax> Items) : StatementSyntax;

public sealed record LessCommandItemSyntax(IReadOnlyList<Token> Arguments) : LessBlockItemSyntax;

public sealed record LessNestedStatementSyntax(LessStatementSyntax Statement) : LessBlockItemSyntax;

public sealed record SayStatementSyntax(
    string Speaker,
    string? Tag,
    IReadOnlyList<TextLineSyntax> Lines) : StatementSyntax;

public sealed record NarStatementSyntax(
    string? Tag,
    IReadOnlyList<TextLineSyntax> Lines) : StatementSyntax;

public sealed record TextLineSyntax(string Text, bool IsExpressionLine);

public sealed record SelectStatementSyntax(IReadOnlyList<CaseClauseSyntax> Cases) : StatementSyntax;

public sealed record CaseClauseSyntax(string Text, string Tag);