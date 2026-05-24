using KoromoEventScript.Cli.Build;
using KoromoEventScript.Cli.Parsing;

namespace KoromoEventScript.Cli.Tests.Build;

public class KelScriptReferenceResolverTests
{
    [Test]
    public void ResolveScriptReferences_FindsNestedChapterReferencesInFirstSeenOrder()
    {
        const string source = """
entry = intro

    intro = {
        chapter = "events/intro.kc"
        route = {
        chapter = route_a.kc
    }
}

duplicate = {
    chapter = "events/intro.kc"
}
""";
        var syntax = KelParser.Parse(source);

        var references = new KelScriptReferenceResolver().ResolveScriptReferences(syntax);

        Assert.That(references, Is.EqualTo(["events/intro.kc", "route_a.kc"]));
    }
}
