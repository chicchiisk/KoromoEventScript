using KoromoEventScript.Runtime.Core.Execution;
using KoromoEventScript.Runtime.Core.Klib;
using KoromoEventScript.Runtime.Windows.Diagnostics;

namespace KoromoEventScript.Runtime.Windows.Tests.Diagnostics;

public sealed class RuntimeProfileTests
{
    [Test]
    public void Resolve_WithSourceMapping_ReturnsFileLineColumn()
    {
        var document = CreateDocument(withSourceMapping: true);
        var resolver = new RuntimeSourceMappingResolver();

        var location = resolver.Resolve(document, new RuntimeExecutionPosition("chapter001", 20, null));

        Assert.That(location, Is.EqualTo("events/chapter001.kc:8:5"));
    }

    [Test]
    public void Resolve_WithoutSourceMapping_ReturnsScriptInstructionFallback()
    {
        var document = CreateDocument(withSourceMapping: false);
        var resolver = new RuntimeSourceMappingResolver();

        var location = resolver.Resolve(document, new RuntimeExecutionPosition("chapter001", 20, null));

        Assert.That(location, Is.EqualTo("chapter001#20"));
    }

    [Test]
    public void Record_AddsProfileMeasurementsForDrawVmAndAssetLoad()
    {
        var collector = new RuntimeProfileCollector();
        var position = new RuntimeExecutionPosition("chapter001", 20, null);

        collector.Record(RuntimeProfileArea.Draw, TimeSpan.FromMilliseconds(4.25d), position);
        collector.Record(RuntimeProfileArea.Vm, TimeSpan.FromMilliseconds(6.5d), position);
        collector.Record(RuntimeProfileArea.AssetLoad, TimeSpan.FromMilliseconds(2d), position, "bgm.daily");

        Assert.That(
            collector.Snapshot().Measurements.Select(static measurement => (measurement.Area, measurement.Elapsed.TotalMilliseconds)),
            Is.EqualTo([
                (RuntimeProfileArea.Draw, 4.25d),
                (RuntimeProfileArea.Vm, 6.5d),
                (RuntimeProfileArea.AssetLoad, 2d),
            ]));
    }

    [Test]
    public void Format_IncludesTimingAndSourceMappingFallback()
    {
        var collector = new RuntimeProfileCollector();
        collector.Record(RuntimeProfileArea.Draw, TimeSpan.FromMilliseconds(4.25d), new RuntimeExecutionPosition("chapter001", 20, null));
        collector.Record(RuntimeProfileArea.Vm, TimeSpan.FromMilliseconds(6.5d), new RuntimeExecutionPosition("chapter001", 99, null));
        collector.Record(RuntimeProfileArea.AssetLoad, TimeSpan.FromMilliseconds(2d), new RuntimeExecutionPosition("chapter001", 20, null), "bgm.daily");
        var formatter = new RuntimeProfileLogFormatter(new RuntimeSourceMappingResolver());

        var lines = formatter.Format(collector.Snapshot(), CreateDocument(withSourceMapping: true));

        Assert.Multiple(() =>
        {
            Assert.That(lines, Has.Some.EqualTo("PROFILE draw 4.25ms events/chapter001.kc:8:5"));
            Assert.That(lines, Has.Some.EqualTo("PROFILE vm 6.50ms chapter001#99"));
            Assert.That(lines, Has.Some.EqualTo("PROFILE assetLoad 2.00ms events/chapter001.kc:8:5 asset=bgm.daily"));
        });
    }

    private static KlibDocument CreateDocument(bool withSourceMapping)
    {
        return new KlibDocument(
            new KlibVersion(1, 0, 0),
            new KlibModuleInfo("chapter001", "chapter001", "events/chapter001.kc", null),
            [],
            [
                new KlibConstant(KlibConstantKind.String, StringValue: "events/chapter001.kc"),
            ],
            [],
            [
                new KlibInstruction(20, 4, KlibOpCode.PushNull, [], null, KlibMappingKind.Synthetic),
                new KlibInstruction(99, 8, KlibOpCode.PushNull, [], null, KlibMappingKind.Synthetic),
            ],
            [],
            new KlibDebugInfo(
                ModuleDisplayNameIndex: null,
                FileDisplayNameIndex: null,
                SourceMappings: withSourceMapping
                    ? [new KlibSourceMapping(4, FileIndex: 0, Line: 8, Column: 5, EndLine: 8, EndColumn: 12, KlibMappingKind.Statement)]
                    : []));
    }
}
