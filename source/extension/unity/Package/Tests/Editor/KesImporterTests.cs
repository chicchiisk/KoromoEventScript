using System;
using System.IO;
using System.Text;
using KoromoEventScript.Runtime.Core.Klib;
using KoromoEventScript.Runtime.Core.Execution;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using System.Text.RegularExpressions;

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
            "  \"scripts\": [{\"scriptId\":\"events/chapter001\",\"locale\":\"ja-JP\",\"klibPath\":\"events/chapter001.klib\",\"isEntry\":true,\"startLabel\":\"\"}],\n" +
            "  \"events\": [{\"eventId\":\"chapter001_intro\",\"type\":\"story\",\"chapter\":\"events/chapter001.kc\",\"scriptId\":\"events/chapter001\",\"isEntry\":true,\"trigger\":{\"conditions\":[{\"kind\":\"from\",\"from\":\"prologue\",\"param\":null,\"value\":null},{\"kind\":\"is\",\"from\":null,\"param\":\"route\",\"value\":{\"kind\":\"string\",\"text\":\"chapter001_intro\"}}],\"or\":[]}}],\n" +
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
        Assert.That(buildAsset.Scripts[0].IsEntry, Is.True);
        Assert.That(buildAsset.Scripts[0].Klib.Data.Length, Is.EqualTo(klibData.Length));
        Assert.That(buildAsset.Events, Has.Count.EqualTo(1));
        Assert.That(buildAsset.Events[0].EventId, Is.EqualTo("chapter001_intro"));
        Assert.That(buildAsset.Events[0].ScriptId, Is.EqualTo("events/chapter001"));
        Assert.That(buildAsset.Events[0].IsEntry, Is.True);
        var parameters = new RuntimeGameParameterStore();
        parameters.Set("route", RuntimeValue.String("chapter001_intro"));
        Assert.That(
            new KoromoEventScript.Runtime.Core.Manifests.RuntimeTriggerEvaluator(parameters)
                .IsMatch(buildAsset.Events[0].Trigger, "prologue"),
            Is.True);
        var loadResult = new KlibModuleLoader().Load(buildAsset.Scripts[0].Klib.Data, klibPath);
        Assert.That(loadResult.Succeeded, Is.True);
        Assert.That(loadResult.Document.Module.ScriptId, Is.EqualTo("events/chapter001"));
    }

    [TestCase(false, "KESU1001")]
    [TestCase(true, "KESU1001")]
    public void ImportKlib_CorruptOrUnsupportedVersion_DoesNotCreateAsset(
        bool unsupportedVersion,
        string diagnosticCode)
    {
        var klibPath = TestRoot + "/events/invalid.klib";
        var data = unsupportedVersion ? BuildMinimalKlib() : Encoding.UTF8.GetBytes("broken");
        if (unsupportedVersion)
        {
            BitConverter.GetBytes(99).CopyTo(data, 4);
        }

        File.WriteAllBytes(klibPath, data);
        ExpectImportFailure(diagnosticCode);
        AssetDatabase.ImportAsset(klibPath, ImportAssetOptions.ForceSynchronousImport);

        Assert.That(AssetDatabase.LoadAssetAtPath<KesKlibAsset>(klibPath), Is.Null);
    }

    [Test]
    public void ImportKson_MissingKlib_DoesNotCreateBuildAsset()
    {
        var manifestPath = TestRoot + "/missing.kson";
        File.WriteAllText(manifestPath, BuildManifest("events/chapter001", "events/missing.klib"));

        ExpectImportFailure("KESU1108");
        AssetDatabase.ImportAsset(manifestPath, ImportAssetOptions.ForceSynchronousImport);

        Assert.That(AssetDatabase.LoadAssetAtPath<KesBuildAsset>(manifestPath), Is.Null);
    }

    [Test]
    public void ImportKson_ScriptIdMismatch_DoesNotCreateBuildAsset()
    {
        var klibPath = TestRoot + "/events/chapter001.klib";
        File.WriteAllBytes(klibPath, BuildMinimalKlib("events/actual"));
        AssetDatabase.ImportAsset(klibPath, ImportAssetOptions.ForceSynchronousImport);

        var manifestPath = TestRoot + "/mismatch.kson";
        File.WriteAllText(manifestPath, BuildManifest("events/expected", "events/chapter001.klib"));

        ExpectImportFailure("KESU1112");
        AssetDatabase.ImportAsset(manifestPath, ImportAssetOptions.ForceSynchronousImport);

        Assert.That(AssetDatabase.LoadAssetAtPath<KesBuildAsset>(manifestPath), Is.Null);
    }

    private static string BuildManifest(string scriptId, string klibPath)
    {
        return
            "{\n" +
            "  \"schemaVersion\": \"1.0\",\n" +
            "  \"gameId\": \"import-test\",\n" +
            "  \"defaultLocale\": \"ja-JP\",\n" +
            "  \"target\": \"unity\",\n" +
            "  \"scripts\": [{\"scriptId\":\"" + scriptId + "\",\"locale\":\"ja-JP\",\"klibPath\":\"" + klibPath + "\",\"isEntry\":true,\"startLabel\":\"\"}],\n" +
            "  \"events\": [],\n" +
            "  \"localizations\": [],\n" +
            "  \"build\": {\"buildId\":\"unity-import-test-1\"}\n" +
            "}";
    }

    private static void ExpectImportFailure(string diagnosticCode)
    {
        var diagnosticPattern = new Regex(diagnosticCode, RegexOptions.Singleline);
        LogAssert.Expect(LogType.Exception, diagnosticPattern);
        LogAssert.Expect(LogType.Error, diagnosticPattern);
    }

    internal static byte[] BuildMinimalKlib(
        string scriptId = "events/chapter001",
        int? sourceLine = null,
        int sourceColumn = 1)
    {
        var sections = new[]
        {
            CreateSection(0x0001, writer =>
            {
                WriteString(writer, scriptId);
                WriteString(writer, scriptId);
                WriteString(writer, scriptId + ".kc");
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
                writer.Write(sourceLine.HasValue ? 1 : 0);
                if (sourceLine.HasValue)
                {
                    writer.Write(0);
                    writer.Write(0);
                    writer.Write(sourceLine.Value);
                    writer.Write(sourceColumn);
                    writer.Write(sourceLine.Value);
                    writer.Write(sourceColumn);
                    writer.Write((int)KlibMappingKind.Statement);
                }
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
