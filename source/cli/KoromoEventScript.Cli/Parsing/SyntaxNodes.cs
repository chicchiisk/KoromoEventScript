using KoromoEventScript.Cli.Lexing;

namespace KoromoEventScript.Cli.Parsing;

public sealed record ScriptSyntax(IReadOnlyList<StatementSyntax> Statements);

public readonly record struct SourceLocation(int Line, int Column);

public sealed record KelDocumentSyntax(KelObjectSyntax Root);

public sealed record KelObjectSyntax(IReadOnlyList<KelPropertySyntax> Properties);

public sealed record KelPropertySyntax(
    string Key,
    IReadOnlyList<KelValueSyntax> Values);

public abstract record KelValueSyntax;

public sealed record KelObjectValueSyntax(KelObjectSyntax Object) : KelValueSyntax;

public sealed record KelStringValueSyntax(string Value) : KelValueSyntax;

public sealed record KelIdentifierValueSyntax(string Value) : KelValueSyntax;

public sealed record KelNumberValueSyntax(string Value) : KelValueSyntax;

public sealed record KelBooleanValueSyntax(bool Value) : KelValueSyntax;

public abstract record StatementSyntax;

public sealed record ImportStatementSyntax(string ModuleName) : StatementSyntax;

public sealed record VarStatementSyntax(
    string Name,
    IReadOnlyList<Token> TypeTokens,
    IReadOnlyList<Token> ValueTokens,
    SourceLocation NameLocation = default) : StatementSyntax;

public sealed record LabelStatementSyntax(
    string Tag,
    SourceLocation TagLocation = default) : StatementSyntax;

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
    IReadOnlyList<TextLineSyntax> Lines,
    SourceLocation? TagLocation = null) : StatementSyntax;

public sealed record NarStatementSyntax(
    string? Tag,
    IReadOnlyList<TextLineSyntax> Lines,
    SourceLocation? TagLocation = null) : StatementSyntax;

public sealed record TextLineSyntax(string Text, bool IsExpressionLine);

public sealed record SelectStatementSyntax(IReadOnlyList<CaseClauseSyntax> Cases) : StatementSyntax;

public sealed record CaseClauseSyntax(string Text, string Tag);
