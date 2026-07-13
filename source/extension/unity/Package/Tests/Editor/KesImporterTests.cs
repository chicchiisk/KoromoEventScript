using System.IO;
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
        var klibData = new byte[24];
        klibData[0] = (byte)'K';
        klibData[1] = (byte)'L';
        klibData[2] = (byte)'I';
        klibData[3] = (byte)'B';
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
        Assert.That(buildAsset.Scripts[0].Klib.Data.Length, Is.EqualTo(24));
    }
}
}
