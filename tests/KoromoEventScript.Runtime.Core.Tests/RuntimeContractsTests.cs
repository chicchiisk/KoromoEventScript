using KoromoEventScript.Runtime.Core.Diagnostics;
using KoromoEventScript.Runtime.Core.Effects;

namespace KoromoEventScript.Runtime.Core.Tests;

public sealed class RuntimeContractsTests
{
    [Test]
    public void RuntimeEffectBatch_CollectsEffectsAndDiagnosticsForHostObservation()
    {
        var batch = new RuntimeEffectBatch(
            [
                new RuntimeEffect(RuntimeEffectKind.Scene, "scene.background", new Dictionary<string, string?>
                {
                    ["asset"] = "bg.school",
                }),
                RuntimeEffect.Wait(RuntimeWaitKind.Click),
            ],
            [
                RuntimeDiagnostic.Warning(
                    "KESW9001",
                    "voice asset is missing",
                    new RuntimeSourceLocation("chapter001", 12, "events/chapter001.kc", 8, 5)),
            ]);

        Assert.Multiple(() =>
        {
            Assert.That(batch.Effects.Select(static effect => effect.Kind), Is.EqualTo(new[] { RuntimeEffectKind.Scene, RuntimeEffectKind.Wait }));
            Assert.That(batch.Diagnostics.Single().Severity, Is.EqualTo(RuntimeDiagnosticSeverity.Warning));
            Assert.That(batch.Diagnostics.Single().Location?.ScriptId, Is.EqualTo("chapter001"));
        });
    }

    [Test]
    public void RuntimeEffectSink_AllowsFakeHostToObserveVmOrSyscallOutput()
    {
        var host = new FakeRuntimeEffectSink();
        var diagnostic = RuntimeDiagnostic.Error("KESR5001", "unknown opcode", RuntimeFailureKind.Runtime);
        var batch = new RuntimeEffectBatch([RuntimeEffect.Diagnostic(diagnostic)], [diagnostic]);

        host.Publish(batch);

        Assert.Multiple(() =>
        {
            Assert.That(host.Published.Single().Effects.Single().Name, Is.EqualTo("KESR5001"));
            Assert.That(host.Published.Single().Diagnostics.Single().FailureKind, Is.EqualTo(RuntimeFailureKind.Runtime));
        });
    }

    [Test]
    public void RuntimeExitCodeMapper_MapsRuntimeFailuresToCliCompatibleExitCodes()
    {
        Assert.Multiple(() =>
        {
            Assert.That((int)RuntimeExitCodeMapper.Map(RuntimeFailureKind.None), Is.EqualTo(0));
            Assert.That((int)RuntimeExitCodeMapper.Map(RuntimeFailureKind.Argument), Is.EqualTo(2));
            Assert.That((int)RuntimeExitCodeMapper.Map(RuntimeFailureKind.Runtime), Is.EqualTo(5));
            Assert.That((int)RuntimeExitCodeMapper.Map(RuntimeFailureKind.Io), Is.EqualTo(6));
            Assert.That((int)RuntimeExitCodeMapper.Map(RuntimeFailureKind.Startup), Is.EqualTo(7));
        });
    }

    private sealed class FakeRuntimeEffectSink : IRuntimeEffectSink
    {
        private readonly List<RuntimeEffectBatch> published = [];

        public IReadOnlyList<RuntimeEffectBatch> Published => published;

        public void Publish(RuntimeEffectBatch batch)
        {
            published.Add(batch);
        }
    }
}
