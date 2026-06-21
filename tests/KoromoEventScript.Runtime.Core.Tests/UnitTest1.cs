using KoromoEventScript.Runtime.Core;

using KoromoEventScript.Runtime.Core.Klib;

namespace KoromoEventScript.Runtime.Core.Tests;

public sealed class RuntimeCoreAssemblyTests
{
    [Test]
    public void RuntimeCoreAssemblyIsReferenceable()
    {
        Assert.That(typeof(RuntimeCoreAssemblyMarker).Assembly.GetName().Name, Is.EqualTo("KoromoEventScript.Runtime.Core"));
    }
    [Test]
    public void KlibDocumentCanBeConstructedFromRuntimeCoreOnly()
    {
        var document = new KlibDocument(
            new KlibVersion(1, 0, 0),
            new KlibModuleInfo("chapter001", "chapter001", "events/chapter001.kc", EntryLabel: null),
            [],
            [new KlibConstant(KlibConstantKind.String, StringValue: "message")],
            [],
            [
                new KlibInstruction(
                    0,
                    0,
                    KlibOpCode.PushConst,
                    [0],
                    new KlibSourceLocation(12, 8),
                    KlibMappingKind.Statement),
            ],
            [],
            new KlibDebugInfo(null, null, []));

        Assert.Multiple(() =>
        {
            Assert.That(document.Module.ScriptId, Is.EqualTo("chapter001"));
            Assert.That(document.Instructions.Single().Source, Is.EqualTo(new KlibSourceLocation(12, 8)));
        });
    }
}
