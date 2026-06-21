using KoromoEventScript.Cli.Build;
using KoromoEventScript.Cli.Commands;
using KoromoEventScript.Cli.Commands.Build;
using KoromoEventScript.Cli.Compilation;
using KoromoEventScript.Cli.Diagnostics;
using KoromoEventScript.Cli.Execution;
using SourceLocation = KoromoEventScript.Runtime.Core.Klib.KlibSourceLocation;

namespace KoromoEventScript.Cli.Tests.Execution;

internal static class HeadlessVmTestHelper
{
    public static KlibConstant StringConstant(string value)
    {
        return new KlibConstant(KlibConstantKind.String, StringValue: value);
    }

    public static KlibConstant NumberConstant(double value)
    {
        return new KlibConstant(KlibConstantKind.Number, NumberValue: value);
    }

    public static KlibConstant BoolConstant(bool value)
    {
        return new KlibConstant(KlibConstantKind.Bool, BoolValue: value);
    }

    public static KlibConstant NullConstant()
    {
        return new KlibConstant(KlibConstantKind.Null);
    }

    public static KlibConstant ReferenceConstant(KlibConstantKind kind, string value)
    {
        if (kind is KlibConstantKind.String or KlibConstantKind.Number or KlibConstantKind.Bool or KlibConstantKind.Null)
        {
            throw new ArgumentException($"Reference constant kind '{kind}' is not supported.", nameof(kind));
        }

        return new KlibConstant(kind, StringValue: value);
    }

    public static IReadOnlyList<KlibConstant> CreateConstants(params KlibConstant[] constants)
    {
        var result = constants.ToList();
        for (var index = 0; index < result.Count; index++)
        {
            var constant = result[index];
            if (constant.Kind is not (KlibConstantKind.String or KlibConstantKind.Number or KlibConstantKind.Bool or KlibConstantKind.Null))
            {
                var targetIndex = result.FindIndex(existing => existing.Kind == KlibConstantKind.String && existing.StringValue == constant.StringValue);
                if (targetIndex < 0)
                {
                    throw new InvalidOperationException($"String constant '{constant.StringValue}' was not found for reference kind '{constant.Kind}'.");
                }

                result[index] = constant with
                {
                    StringValue = null,
                    ReferenceIndex = targetIndex
                };
            }
        }

        return result;
    }

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
        return CreateSyntheticDocument(
            [
                new KlibInstruction(0, 0, KlibOpCode.Jump, [4], new SourceLocation(1, 1), KlibMappingKind.Statement),
                new KlibInstruction(1, 5, KlibOpCode.End, [], new SourceLocation(2, 1), KlibMappingKind.Statement),
            ],
            [StringConstant("#missing")]);
    }

    public static KlibDocument CreateSyntheticDocument(
        IReadOnlyList<KlibInstruction> instructions,
        IReadOnlyList<KlibConstant>? constants = null,
        IReadOnlyList<KlibVariable>? variables = null,
        IReadOnlyList<KlibLabel>? labels = null)
    {
        constants ??= [];
        variables ??= [];
        labels ??= [];

        return new KlibDocument(
            new KlibVersion(1, 0, 0),
            new KlibModuleInfo("events/main", "module.main", "events/main.kc", "#start"),
            [],
            constants,
            variables,
            instructions,
            labels,
            new KlibDebugInfo(
                null,
                null,
                instructions.Select(static instruction => new KlibSourceMapping(
                    instruction.Offset,
                    0,
                    instruction.Source?.Line ?? instruction.Index + 1,
                    instruction.Source?.Column ?? 1,
                    instruction.Source?.Line ?? instruction.Index + 1,
                    instruction.Source?.Column ?? 1,
                    instruction.MappingKind)).ToArray()));
    }

    public static HeadlessVmSession CreateSession(KlibDocument document)
    {
        var session = new HeadlessVmSession();
        session.Start(document);
        return session;
    }
}
