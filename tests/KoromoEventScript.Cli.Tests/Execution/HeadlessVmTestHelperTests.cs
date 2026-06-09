using KoromoEventScript.Cli.Compilation;
using KoromoEventScript.Cli.Execution;
using KoromoEventScript.Cli.Parsing;

namespace KoromoEventScript.Cli.Tests.Execution;

public class HeadlessVmTestHelperTests
{
    [Test]
    public void CreateSyntheticDocument_PreservesInstructionsAndDebugMappings()
    {
        var instructions = new[]
        {
            new KlibInstruction(0, 0, KlibOpCode.PushNull, [], new SourceLocation(1, 1), KlibMappingKind.Statement),
            new KlibInstruction(1, 1, KlibOpCode.End, [], new SourceLocation(2, 1), KlibMappingKind.Statement),
        };

        var document = HeadlessVmTestHelper.CreateSyntheticDocument(instructions);

        Assert.Multiple(() =>
        {
            Assert.That(document.Instructions.Select(static x => x.OpCode), Is.EqualTo(new[] { KlibOpCode.PushNull, KlibOpCode.End }));
            Assert.That(document.Debug.SourceMappings.Select(static x => x.BytecodeOffset), Is.EqualTo(new[] { 0, 1 }));
            Assert.That(document.Module.ScriptId, Is.EqualTo("events/main"));
        });
    }

    [Test]
    public void CreateReferenceConstant_CreatesTypedReferenceEntry()
    {
        var constants = HeadlessVmTestHelper.CreateConstants(
            HeadlessVmTestHelper.StringConstant("class.player"),
            HeadlessVmTestHelper.ReferenceConstant(KlibConstantKind.ClassRef, "class.player"));

        Assert.Multiple(() =>
        {
            Assert.That(constants[1].Kind, Is.EqualTo(KlibConstantKind.ClassRef));
            Assert.That(constants[1].ReferenceIndex, Is.EqualTo(0));
            Assert.That(constants[0].StringValue, Is.EqualTo("class.player"));
        });
    }
}
