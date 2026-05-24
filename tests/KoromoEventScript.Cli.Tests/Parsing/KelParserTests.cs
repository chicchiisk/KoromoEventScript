using KoromoEventScript.Cli.Parsing;

namespace KoromoEventScript.Cli.Tests.Parsing;

public class KelParserTests
{
    [Test]
    public void Parse_BuildsSyntaxTreeForValidKelDocument()
    {
        var source = ReadTestDataFile("kel", "valid", "main.kel");

        var syntax = KelParser.Parse(source);
        var entry = AssertProperty(syntax.Root, "entry");
        var chapter = AssertProperty(syntax.Root, "chapter001_intro");
        var chapterObject = AssertSingleValue<KelObjectValueSyntax>(chapter).Object;
        var trigger = AssertProperty(chapterObject, "trigger");
        var triggerObject = AssertSingleValue<KelObjectValueSyntax>(trigger).Object;

        Assert.Multiple(() =>
        {
            Assert.That(syntax.Root.Properties.Select(static property => property.Key),
                Is.EqualTo(new[] { "entry", "chapter001_intro" }));
            Assert.That(AssertSingleValue<KelIdentifierValueSyntax>(entry).Value, Is.EqualTo("chapter001_intro"));
            Assert.That(AssertSingleValue<KelIdentifierValueSyntax>(AssertProperty(chapterObject, "type")).Value, Is.EqualTo("story"));
            Assert.That(AssertSingleValue<KelStringValueSyntax>(AssertProperty(chapterObject, "chapter")).Value, Is.EqualTo("events/chapter001.kc"));
            Assert.That(AssertSingleValue<KelNumberValueSyntax>(AssertProperty(chapterObject, "priority")).Value, Is.EqualTo("100"));
            Assert.That(AssertSingleValue<KelNumberValueSyntax>(AssertProperty(chapterObject, "weight")).Value, Is.EqualTo("0.75"));
            Assert.That(AssertSingleValue<KelBooleanValueSyntax>(AssertProperty(triggerObject, "flag.123")).Value, Is.True);
        });
    }

    [Test]
    public void Parse_AggregatesDuplicateKeysAsMultipleValues()
    {
        const string source = """
option = {
    text = option.a
}

option = {
    text = option.b
}
""";

        var syntax = KelParser.Parse(source);
        var option = AssertProperty(syntax.Root, "option");
        var firstObject = AssertValueAt<KelObjectValueSyntax>(option, 0).Object;
        var secondObject = AssertValueAt<KelObjectValueSyntax>(option, 1).Object;

        Assert.Multiple(() =>
        {
            Assert.That(option.Values, Has.Count.EqualTo(2));
            Assert.That(AssertSingleValue<KelIdentifierValueSyntax>(AssertProperty(firstObject, "text")).Value, Is.EqualTo("option.a"));
            Assert.That(AssertSingleValue<KelIdentifierValueSyntax>(AssertProperty(secondObject, "text")).Value, Is.EqualTo("option.b"));
        });
    }

    [Test]
    public void Parse_BuildsNestedObjectsRecursively()
    {
        const string source = """
trigger = {
    flag.123 = true
    or = {
        flag.234 = true
        flag.235 = true
        or = {
            flag.555 = false
            flag.556 = true
        }
    }
}
""";

        var syntax = KelParser.Parse(source);
        var trigger = AssertProperty(syntax.Root, "trigger");
        var triggerObject = AssertSingleValue<KelObjectValueSyntax>(trigger).Object;
        var outerOr = AssertProperty(triggerObject, "or");
        var outerOrObject = AssertSingleValue<KelObjectValueSyntax>(outerOr).Object;
        var innerOr = AssertProperty(outerOrObject, "or");
        var innerOrObject = AssertSingleValue<KelObjectValueSyntax>(innerOr).Object;

        Assert.Multiple(() =>
        {
            Assert.That(AssertSingleValue<KelBooleanValueSyntax>(AssertProperty(triggerObject, "flag.123")).Value, Is.True);
            Assert.That(AssertSingleValue<KelBooleanValueSyntax>(AssertProperty(outerOrObject, "flag.234")).Value, Is.True);
            Assert.That(AssertSingleValue<KelBooleanValueSyntax>(AssertProperty(outerOrObject, "flag.235")).Value, Is.True);
            Assert.That(AssertSingleValue<KelBooleanValueSyntax>(AssertProperty(innerOrObject, "flag.555")).Value, Is.False);
            Assert.That(AssertSingleValue<KelBooleanValueSyntax>(AssertProperty(innerOrObject, "flag.556")).Value, Is.True);
        });
    }

    [Test]
    public void Parse_AllowsKeywordLikeNamesWithoutTreatingThemAsReserved()
    {
        const string source = """
if = if
true.value = false.value
enabled = true
""";

        var syntax = KelParser.Parse(source);

        Assert.Multiple(() =>
        {
            Assert.That(AssertSingleValue<KelIdentifierValueSyntax>(AssertProperty(syntax.Root, "if")).Value, Is.EqualTo("if"));
            Assert.That(AssertSingleValue<KelIdentifierValueSyntax>(AssertProperty(syntax.Root, "true.value")).Value, Is.EqualTo("false.value"));
            Assert.That(AssertSingleValue<KelBooleanValueSyntax>(AssertProperty(syntax.Root, "enabled")).Value, Is.True);
        });
    }

    [Test]
    public void Parse_ReportsMissingValueAfterEquals()
    {
        var source = ReadTestDataFile("kel", "invalid", "missing-script.kel");

        var exception = Assert.Throws<ParserException>(() => KelParser.Parse(source));

        Assert.Multiple(() =>
        {
            Assert.That(exception!.Diagnostic.Code, Is.EqualTo("KES2001"));
            Assert.That(exception.Diagnostic.Line, Is.EqualTo(1));
            Assert.That(exception.Diagnostic.Column, Is.EqualTo(8));
        });
    }

    private static KelPropertySyntax AssertProperty(KelObjectSyntax syntax, string key)
    {
        return syntax.Properties.Single(property => property.Key == key);
    }

    private static T AssertSingleValue<T>(KelPropertySyntax property)
        where T : KelValueSyntax
    {
        Assert.That(property.Values, Has.Count.EqualTo(1));
        Assert.That(property.Values[0], Is.TypeOf<T>());
        return (T)property.Values[0];
    }

    private static T AssertValueAt<T>(KelPropertySyntax property, int index)
        where T : KelValueSyntax
    {
        Assert.That(property.Values[index], Is.TypeOf<T>());
        return (T)property.Values[index];
    }

    private static string ReadTestDataFile(params string[] relativePathSegments)
    {
        var path = Path.Combine(GetRepositoryRoot(), "testdata", Path.Combine(relativePathSegments));
        return File.ReadAllText(path);
    }

    private static string GetRepositoryRoot()
    {
        return Path.GetFullPath(Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "..",
            "..",
            "..",
            "..",
            ".."));
    }
}