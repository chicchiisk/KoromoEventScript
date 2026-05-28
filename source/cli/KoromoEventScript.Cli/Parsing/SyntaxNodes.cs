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

public sealed record BlockSyntax(IReadOnlyList<StatementSyntax> Statements);

public sealed record VarStatementSyntax(
    string Name,
    IReadOnlyList<Token> TypeTokens,
    IReadOnlyList<Token> ValueTokens,
    SourceLocation NameLocation = default) : StatementSyntax;

public sealed record AssignmentStatementSyntax(
    string TargetName,
    IReadOnlyList<Token> ValueTokens,
    SourceLocation TargetLocation = default) : StatementSyntax;

public sealed record ParameterSyntax(
    string Name,
    IReadOnlyList<Token> TypeTokens,
    SourceLocation NameLocation = default);

public sealed record FunctionDeclarationSyntax(
    string Name,
    SourceLocation NameLocation,
    IReadOnlyList<ParameterSyntax> Parameters,
    IReadOnlyList<Token> ReturnTypeTokens,
    BlockSyntax Body) : StatementSyntax;

public sealed record ActorDeclarationSyntax(
    string Name,
    SourceLocation NameLocation,
    BlockSyntax Body) : StatementSyntax;

public sealed record EnumMemberSyntax(
    string Name,
    SourceLocation NameLocation = default);

public sealed record EnumDeclarationSyntax(
    string Name,
    SourceLocation NameLocation,
    IReadOnlyList<EnumMemberSyntax> Members) : StatementSyntax;

public abstract record ClassMemberSyntax;

public sealed record ClassFieldSyntax(
    string? AccessModifier,
    VarStatementSyntax Declaration) : ClassMemberSyntax;

public sealed record ClassMethodSyntax(
    string? AccessModifier,
    FunctionDeclarationSyntax Declaration) : ClassMemberSyntax;

public sealed record ClassDeclarationSyntax(
    string Name,
    SourceLocation NameLocation,
    IReadOnlyList<ClassMemberSyntax> Members) : StatementSyntax;

public sealed record LabelStatementSyntax(
    string Tag,
    SourceLocation TagLocation = default) : StatementSyntax;

public sealed record JumpStatementSyntax(
    string Tag,
    SourceLocation TagLocation = default) : StatementSyntax;

public sealed record CommandStatementSyntax(
    string Name,
    IReadOnlyList<Token> Arguments,
    SourceLocation NameLocation = default) : StatementSyntax;

public abstract record LessBlockItemSyntax;

public sealed record LessStatementSyntax(
    string Name,
    IReadOnlyList<Token> SharedArguments,
    IReadOnlyList<LessBlockItemSyntax> Items,
    SourceLocation NameLocation = default) : StatementSyntax;

public sealed record LessCommandItemSyntax(IReadOnlyList<Token> Arguments) : LessBlockItemSyntax;

public sealed record LessNestedStatementSyntax(LessStatementSyntax Statement) : LessBlockItemSyntax;

public sealed record SayStatementSyntax(
    string Speaker,
    string? Tag,
    IReadOnlyList<TextLineSyntax> Lines,
    SourceLocation? TagLocation = null,
    SourceLocation SpeakerLocation = default) : StatementSyntax;

public sealed record NarStatementSyntax(
    string? Tag,
    IReadOnlyList<TextLineSyntax> Lines,
    SourceLocation? TagLocation = null) : StatementSyntax;

public sealed record TextLineSyntax(string Text, bool IsExpressionLine);

public sealed record SelectStatementSyntax(IReadOnlyList<CaseClauseSyntax> Cases) : StatementSyntax;

public sealed record CaseClauseSyntax(
    string Text,
    string Tag,
    SourceLocation TagLocation = default);

public sealed record IfStatementSyntax(
    IReadOnlyList<Token> ConditionTokens,
    BlockSyntax Body,
    IReadOnlyList<ElseIfClauseSyntax> ElseIfClauses,
    BlockSyntax? ElseBody,
    SourceLocation IfLocation = default) : StatementSyntax;

public sealed record ElseIfClauseSyntax(
    IReadOnlyList<Token> ConditionTokens,
    BlockSyntax Body,
    SourceLocation ElseIfLocation = default);

public sealed record WhileStatementSyntax(
    IReadOnlyList<Token> ConditionTokens,
    BlockSyntax Body,
    SourceLocation WhileLocation = default) : StatementSyntax;

public sealed record ForStatementSyntax(
    string VariableName,
    IReadOnlyList<Token> IterableTokens,
    BlockSyntax Body,
    SourceLocation VariableLocation = default,
    SourceLocation ForLocation = default) : StatementSyntax;
