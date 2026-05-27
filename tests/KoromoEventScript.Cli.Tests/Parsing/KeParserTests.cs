using KoromoEventScript.Cli.Lexing;
using KoromoEventScript.Cli.Parsing;

namespace KoromoEventScript.Cli.Tests.Parsing;

public class KeParserTests
{
    [Test]
    public void Parse_BuildsSyntaxTreeForMinimalSupportedStatements()
    {
        const string source = """
import Common

var hp: number = 10
show Noa 0 face="normal"
cast exp="eye_open":
    Riku
    Amane
say Riku #line_1:
    こんにちは
    @vf Riku "smile"
nar #nar_1:
    地の文です
select:
    case "はい" #yes
    case "いいえ" #no
label #yes
jump #end
""";

        var syntax = KeParser.Parse(source);

        Assert.That(syntax.Statements, Has.Count.EqualTo(9));
        Assert.Multiple(() =>
        {
            Assert.That(syntax.Statements[0], Is.TypeOf<ImportStatementSyntax>());

            var varStatement = (VarStatementSyntax)syntax.Statements[1];
            Assert.That(varStatement.Name, Is.EqualTo("hp"));
            Assert.That(varStatement.NameLocation, Is.EqualTo(new SourceLocation(3, 5)));
            Assert.That(varStatement.TypeTokens.Select(static token => token.Lexeme), Is.EqualTo(["number"]));
            Assert.That(varStatement.ValueTokens.Select(static token => token.Lexeme), Is.EqualTo(["10"]));

            var commandStatement = (CommandStatementSyntax)syntax.Statements[2];
            Assert.That(commandStatement.Name, Is.EqualTo("show"));
            Assert.That(commandStatement.Arguments.Select(static token => token.Lexeme), Is.EqualTo(["Noa", "0", "face", "=", "normal"]));

            var lessStatement = (LessStatementSyntax)syntax.Statements[3];
            Assert.That(lessStatement.Name, Is.EqualTo("cast"));
            Assert.That(lessStatement.SharedArguments.Select(static token => token.Lexeme), Is.EqualTo(["exp", "=", "eye_open"]));
            Assert.That(lessStatement.Items.Cast<LessCommandItemSyntax>().Select(static item => string.Join(' ', item.Arguments.Select(static token => token.Lexeme))),
                Is.EqualTo(["Riku", "Amane"]));

            var sayStatement = (SayStatementSyntax)syntax.Statements[4];
            Assert.That(sayStatement.Speaker, Is.EqualTo("Riku"));
            Assert.That(sayStatement.Tag, Is.EqualTo("#line_1"));
            Assert.That(sayStatement.TagLocation, Is.EqualTo(new SourceLocation(8, 10)));
            Assert.That(sayStatement.Lines.Select(static line => (line.Text, line.IsExpressionLine)),
                Is.EqualTo(new[] { ("こんにちは", false), ("@vf Riku \"smile\"", true) }));

            var narStatement = (NarStatementSyntax)syntax.Statements[5];
            Assert.That(narStatement.Tag, Is.EqualTo("#nar_1"));
            Assert.That(narStatement.TagLocation, Is.EqualTo(new SourceLocation(11, 5)));
            Assert.That(narStatement.Lines.Select(static line => line.Text), Is.EqualTo(["地の文です"]));

            var selectStatement = (SelectStatementSyntax)syntax.Statements[6];
            Assert.That(selectStatement.Cases.Select(static item => (item.Text, item.Tag)),
                Is.EqualTo(new[] { ("はい", "#yes"), ("いいえ", "#no") }));
            Assert.That(selectStatement.Cases.Select(static item => item.TagLocation),
                Is.EqualTo(new[] { new SourceLocation(14, 15), new SourceLocation(15, 16) }));

            var labelStatement = (LabelStatementSyntax)syntax.Statements[7];
            Assert.That(labelStatement.Tag, Is.EqualTo("#yes"));
            Assert.That(labelStatement.TagLocation, Is.EqualTo(new SourceLocation(16, 7)));
            var jumpStatement = (JumpStatementSyntax)syntax.Statements[8];
            Assert.That(jumpStatement.Tag, Is.EqualTo("#end"));
            Assert.That(jumpStatement.TagLocation, Is.EqualTo(new SourceLocation(17, 6)));
        });
    }

    [Test]
    public void Parse_ParsesJumpAfterLabel()
    {
        const string source = """
label #start
jump #end
""";

        var syntax = KeParser.Parse(source);

        Assert.Multiple(() =>
        {
            var labelStatement = (LabelStatementSyntax)syntax.Statements[0];
            Assert.That(labelStatement.Tag, Is.EqualTo("#start"));
            Assert.That(labelStatement.TagLocation, Is.EqualTo(new SourceLocation(1, 7)));
            Assert.That(syntax.Statements[1], Is.EqualTo(new JumpStatementSyntax("#end", new SourceLocation(2, 6))));
        });
    }

    [Test]
    public void Parse_BuildsSyntaxTreeForMajorDefinitions()
    {
        const string source = """
actor Riku:
    var faceName: string = "normal"

fn calc_score(base: number, bonus: number): number:
    var total = base
    score total

class Counter:
    private var value: number = 0
    public fn add(amount: number): number:
        var next = value
        score next

enum Mood:
    normal
    smile
""";

        var syntax = KeParser.Parse(source);

        Assert.That(syntax.Statements, Has.Count.EqualTo(4));
        Assert.Multiple(() =>
        {
            var actor = (ActorDeclarationSyntax)syntax.Statements[0];
            Assert.That(actor.Name, Is.EqualTo("Riku"));
            Assert.That(actor.NameLocation, Is.EqualTo(new SourceLocation(1, 7)));
            Assert.That(actor.Body.Statements.Single(), Is.TypeOf<VarStatementSyntax>());

            var function = (FunctionDeclarationSyntax)syntax.Statements[1];
            Assert.That(function.Name, Is.EqualTo("calc_score"));
            Assert.That(function.NameLocation, Is.EqualTo(new SourceLocation(4, 4)));
            Assert.That(function.Parameters.Select(static parameter => (parameter.Name, parameter.NameLocation)),
                Is.EqualTo(new[] { ("base", new SourceLocation(4, 15)), ("bonus", new SourceLocation(4, 29)) }));
            Assert.That(function.Parameters[0].TypeTokens.Select(static token => token.Lexeme), Is.EqualTo(["number"]));
            Assert.That(function.ReturnTypeTokens.Select(static token => token.Lexeme), Is.EqualTo(["number"]));
            Assert.That(function.Body.Statements.OfType<VarStatementSyntax>().Single().Name, Is.EqualTo("total"));

            var classDeclaration = (ClassDeclarationSyntax)syntax.Statements[2];
            Assert.That(classDeclaration.Name, Is.EqualTo("Counter"));
            Assert.That(classDeclaration.NameLocation, Is.EqualTo(new SourceLocation(8, 7)));
            Assert.That(classDeclaration.Members, Has.Count.EqualTo(2));
            var field = (ClassFieldSyntax)classDeclaration.Members[0];
            Assert.That(field.AccessModifier, Is.EqualTo("private"));
            Assert.That(field.Declaration.Name, Is.EqualTo("value"));
            Assert.That(field.Declaration.NameLocation, Is.EqualTo(new SourceLocation(9, 17)));
            var method = (ClassMethodSyntax)classDeclaration.Members[1];
            Assert.That(method.AccessModifier, Is.EqualTo("public"));
            Assert.That(method.Declaration.Name, Is.EqualTo("add"));
            Assert.That(method.Declaration.NameLocation, Is.EqualTo(new SourceLocation(10, 15)));
            Assert.That(method.Declaration.Parameters.Single().Name, Is.EqualTo("amount"));
            Assert.That(method.Declaration.Body.Statements.OfType<VarStatementSyntax>().Single().Name, Is.EqualTo("next"));

            var enumDeclaration = (EnumDeclarationSyntax)syntax.Statements[3];
            Assert.That(enumDeclaration.Name, Is.EqualTo("Mood"));
            Assert.That(enumDeclaration.NameLocation, Is.EqualTo(new SourceLocation(14, 6)));
            Assert.That(enumDeclaration.Members.Select(static member => (member.Name, member.NameLocation)),
                Is.EqualTo(new[] { ("normal", new SourceLocation(15, 5)), ("smile", new SourceLocation(16, 5)) }));
        });
    }

    [Test]
    public void Parse_ReportsIncompleteMajorDefinitionAsSyntaxDiagnostic()
    {
        const string source = """
fn missingBody()
""";

        var exception = Assert.Throws<ParserException>(() => KeParser.Parse(source));

        Assert.Multiple(() =>
        {
            Assert.That(exception!.Diagnostic.Code, Is.EqualTo("KES2002"));
            Assert.That(exception.Diagnostic.Line, Is.EqualTo(1));
        });
    }

    [Test]
    public void Parse_ReportsMissingColonOnSayStatement()
    {
        const string source = """
say Riku
    こんにちは
""";

        var exception = Assert.Throws<ParserException>(() => KeParser.Parse(source));

        Assert.Multiple(() =>
        {
            Assert.That(exception!.Diagnostic.Code, Is.EqualTo("KES2002"));
            Assert.That(exception.Diagnostic.Line, Is.EqualTo(1));
        });
    }

    [Test]
    public void Parse_ReportsEmptySayBlock()
    {
        const string source = """
say Riku:

jump #end
""";

        var exception = Assert.Throws<ParserException>(() => KeParser.Parse(source));

        Assert.That(exception!.Diagnostic.Code, Is.EqualTo("KES2003"));
    }

    [Test]
    public void Parse_ReportsEmptyNarBlock()
    {
        const string source = """
nar:

jump #end
""";

        var exception = Assert.Throws<ParserException>(() => KeParser.Parse(source));

        Assert.That(exception!.Diagnostic.Code, Is.EqualTo("KES2003"));
    }

    [Test]
    public void Parse_ReportsIndentationMismatchFromLexer()
    {
        const string source = """
select:
    case "はい" #yes
  case "いいえ" #no
""";

        var exception = Assert.Throws<LexerException>(() => KeParser.Parse(source));

        Assert.That(exception!.Diagnostic.Code, Is.EqualTo("KES1005"));
    }

    [Test]
    public void Parse_ReportsCaseOutsideSelectBlock()
    {
        const string source = """
case "はい" #yes
""";

        var exception = Assert.Throws<ParserException>(() => KeParser.Parse(source));

        Assert.That(exception!.Diagnostic.Code, Is.EqualTo("KES2006"));
    }

    [Test]
    public void Parse_ReportsImportAfterOtherStatements()
    {
        const string source = """
show Noa 0
import Common
""";

        var exception = Assert.Throws<ParserException>(() => KeParser.Parse(source));

        Assert.That(exception!.Diagnostic.Code, Is.EqualTo("KES2005"));
    }

    [Test]
    public void Parse_SupportsNestedLessBlocks()
    {
        const string source = """
change_scene:
    bg living_room
    show:
        Kurumi 0 "normal"
        Noa 1 "smile"
""";

        var syntax = KeParser.Parse(source);
        var lessStatement = (LessStatementSyntax)syntax.Statements.Single();

        Assert.Multiple(() =>
        {
            Assert.That(lessStatement.Name, Is.EqualTo("change_scene"));
            Assert.That(lessStatement.Items, Has.Count.EqualTo(2));

            var backgroundItem = (LessCommandItemSyntax)lessStatement.Items[0];
            Assert.That(backgroundItem.Arguments.Select(static token => token.Lexeme), Is.EqualTo(["bg", "living_room"]));

            var nestedItem = (LessNestedStatementSyntax)lessStatement.Items[1];
            Assert.That(nestedItem.Statement.Name, Is.EqualTo("show"));
            Assert.That(nestedItem.Statement.SharedArguments, Is.Empty);
            Assert.That(nestedItem.Statement.Items.Cast<LessCommandItemSyntax>()
                .Select(static item => string.Join(' ', item.Arguments.Select(static token => token.Lexeme))),
                Is.EqualTo(["Kurumi 0 normal", "Noa 1 smile"]));
        });
    }
}
