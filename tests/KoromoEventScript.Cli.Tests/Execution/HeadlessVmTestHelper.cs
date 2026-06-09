using KoromoEventScript.Cli.Build;
using KoromoEventScript.Cli.Commands;
using KoromoEventScript.Cli.Commands.Build;
using KoromoEventScript.Cli.Compilation;
using KoromoEventScript.Cli.Diagnostics;
using KoromoEventScript.Cli.Execution;

namespace KoromoEventScript.Cli.Tests.Execution;

internal static class HeadlessVmTestHelper
{
    public static (TemporaryProject Fixture, KlibDocument Document) CreateScenarioDocument(string keSource, string kelSource)
    {
        var fixture = TemporaryProject.Create();
        fixture.WriteConfig(entry: "events/main.kel");
        fixture.WriteFile("events/main.kel", kelSource);
        fixture.WriteFile("events/main.kc", keSource);

        var preparation = new BuildPreparationService().Prepare(
            new BuildCommandOptions(fixture.Root, DiagnosticOutputFormat.Text, EmitTextIr: true),
            TestContext.CurrentContext.WorkDirectory);

        Assert.That(preparation.Succeeded, Is.True, "Build preparation failed.");
        Assert.That(preparation.SemanticResult?.ImportGraph?.OrderedDocuments, Has.Count.EqualTo(1), "Expected single script document.");

        var document = preparation.SemanticResult!.ImportGraph!.OrderedDocuments[0];
        var compilation = new KlibCompiler().Compile(preparation.Config!, preparation.SemanticResult, document);

        Assert.That(compilation.Succeeded, Is.True, "Compilation failed.");
        return (fixture, compilation.Document!);
    }

    public static KlibDocument CreateInvalidJumpDocument()
    {
        return new KlibDocument(
            new KlibVersion(1, 0, 0),
            new KlibModuleInfo("events/main", "module.main", "events/main.kc", "#start"),
            [],
            [
                new KlibConstant(KlibConstantKind.String, StringValue: "#missing")
            ],
            [],
            [
                new KlibInstruction(0, 0, KlibOpCode.Jump, [4], new KoromoEventScript.Cli.Parsing.SourceLocation(1, 1), KlibMappingKind.Statement),
                new KlibInstruction(1, 5, KlibOpCode.End, [], new KoromoEventScript.Cli.Parsing.SourceLocation(2, 1), KlibMappingKind.Statement)
            ],
            [],
            new KlibDebugInfo(
                null,
                null,
                [
                    new KlibSourceMapping(0, 0, 1, 1, 1, 1, KlibMappingKind.Statement)
                ]));
    }

    public static HeadlessVmSession CreateSession(KlibDocument document)
    {
        var session = new HeadlessVmSession();
        session.Start(document);
        return session;
    }
}
