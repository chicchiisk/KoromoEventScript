using System.IO;
using System.Text;
using KoromoEventScript.Runtime.Core.Klib;
using NUnit.Framework;
using UnityEditor;

namespace KoromoEventScript.Unity.Editor.Tests
{

public sealed class KesImporterTests
{
    private const string TestRoot = "Assets/__KesImporterTests";

    [SetUp]
    public void SetUp()
    {
        AssetDatabase.DeleteAsset(TestRoot);
        Directory.CreateDirectory(TestRoot + "/events");
    }

    [TearDown]
    public void TearDown()
    {
        AssetDatabase.DeleteAsset(TestRoot);
    }

    [Test]
    public void ImportKson_ProducesBuildAssetWithKlibReference()
    {
        var klibPath = TestRoot + "/events/chapter001.klib";
        var klibData = BuildMinimalKlib();
        File.WriteAllBytes(klibPath, klibData);
        AssetDatabase.ImportAsset(klibPath, ImportAssetOptions.ForceSynchronousImport);

        var manifestPath = TestRoot + "/manifest.kson";
        File.WriteAllText(
            manifestPath,
            "{\n" +
            "  \"schemaVersion\": \"1.0\",\n" +
            "  \"gameId\": \"import-test\",\n" +
            "  \"defaultLocale\": \"ja-JP\",\n" +
            "  \"target\": \"unity\",\n" +
            "  \"scripts\": [{\"scriptId\":\"events/chapter001\",\"locale\":\"ja-JP\",\"klibPath\":\"events/chapter001.klib\"}],\n" +
            "  \"localizations\": [],\n" +
            "  \"build\": {\"buildId\":\"unity-import-test-1\"}\n" +
            "}");
        AssetDatabase.ImportAsset(manifestPath, ImportAssetOptions.ForceSynchronousImport);

        var buildAsset = AssetDatabase.LoadAssetAtPath<KesBuildAsset>(manifestPath);

        Assert.That(buildAsset, Is.Not.Null);
        Assert.That(buildAsset.SchemaVersion, Is.EqualTo("1.0"));
        Assert.That(buildAsset.GameId, Is.EqualTo("import-test"));
        Assert.That(buildAsset.BuildId, Is.EqualTo("unity-import-test-1"));
        Assert.That(buildAsset.Scripts, Has.Count.EqualTo(1));
        Assert.That(buildAsset.Scripts[0].ScriptId, Is.EqualTo("events/chapter001"));
        Assert.That(buildAsset.Scripts[0].Klib.Data.Length, Is.EqualTo(klibData.Length));
        var loadResult = new KlibModuleLoader().Load(buildAsset.Scripts[0].Klib.Data, klibPath);
        Assert.That(loadResult.Succeeded, Is.True);
        Assert.That(loadResult.Document.Module.ScriptId, Is.EqualTo("events/chapter001"));
    }

    internal static byte[] BuildMinimalKlib()
    {
        var sections = new[]
        {
            CreateSection(0x0001, writer =>
            {
                WriteString(writer, "events/chapter001");
                WriteString(writer, "events/chapter001");
                WriteString(writer, "events/chapter001.kc");
                writer.Write(0);
            }),
            CreateSection(0x0002, writer => writer.Write(0)),
            CreateSection(0x0003, writer => writer.Write(0)),
            CreateSection(0x0005, writer =>
            {
                writer.Write(1);
                writer.Write((byte)KlibOpCode.End);
            }),
            CreateSection(0x0006, writer => writer.Write(0)),
            CreateSection(0x0007, writer =>
            {
                writer.Write(0);
                writer.Write(0);
                writer.Write(0);
            }),
        };

        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream, Encoding.UTF8))
        {
            writer.Write(Encoding.ASCII.GetBytes("KLIB"));
            writer.Write(1);
            writer.Write(0);
            writer.Write(0);
            writer.Write(0);
            writer.Write(sections.Length);

            var offset = 24 + (sections.Length * 12);
            foreach (var section in sections)
            {
                writer.Write(section.Type);
                writer.Write(offset);
                writer.Write(section.Data.Length);
                offset += section.Data.Length;
            }

            foreach (var section in sections)
            {
                writer.Write(section.Data);
            }

            return stream.ToArray();
        }
    }

    private static KlibSection CreateSection(int type, System.Action<BinaryWriter> write)
    {
        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream, Encoding.UTF8))
        {
            write(writer);
            return new KlibSection(type, stream.ToArray());
        }
    }

    private static void WriteString(BinaryWriter writer, string value)
    {
        var data = Encoding.UTF8.GetBytes(value);
        writer.Write(data.Length);
        writer.Write(data);
    }

    private sealed class KlibSection
    {
        public KlibSection(int type, byte[] data)
        {
            Type = type;
            Data = data;
        }

        public int Type { get; }

        public byte[] Data { get; }
    }
}
}
