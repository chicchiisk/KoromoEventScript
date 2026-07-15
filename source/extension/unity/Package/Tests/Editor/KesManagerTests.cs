using KoromoEventScript.Runtime.Core.Execution;
using NUnit.Framework;
using UnityEngine;

namespace KoromoEventScript.Unity.Editor.Tests
{

public sealed class KesManagerTests
{
    [Test]
    public void NewComponent_PlayOnStartIsEnabled()
    {
        var gameObject = new GameObject("KesManagerTest");

        try
        {
            var manager = gameObject.AddComponent<KesManager>();
            Assert.That(manager.PlayOnStart, Is.True);
        }
        finally
        {
            Object.DestroyImmediate(gameObject);
        }
    }

    [Test]
    public void Play_LoadsDefaultLocaleScriptAndCompletesVm()
    {
        var gameObject = new GameObject("KesManagerExecutionTest");
        var klibAsset = ScriptableObject.CreateInstance<KesKlibAsset>();
        var buildAsset = ScriptableObject.CreateInstance<KesBuildAsset>();

        try
        {
            klibAsset.name = "chapter001";
            klibAsset.SetImportedData(KesImporterTests.BuildMinimalKlib());
            buildAsset.SetImportedData(
                "{}",
                "1.0",
                "manager-test",
                "manager-test-1",
                "ja-JP",
                new[]
                {
                    new KesScriptAssetReference("events/chapter001", "ja-JP", klibAsset),
                });

            var manager = gameObject.AddComponent<KesManager>();
            manager.SetBuildAsset(buildAsset);

            var started = manager.Play();

            Assert.That(started, Is.True);
            Assert.That(manager.Session, Is.Not.Null);
            Assert.That(manager.Session.Document.Module.ScriptId, Is.EqualTo("events/chapter001"));
            Assert.That(manager.Continuation.Kind, Is.EqualTo(RuntimeContinuationKind.Completed));
            Assert.That(manager.LastDiagnostics, Is.Empty);
        }
        finally
        {
            Object.DestroyImmediate(gameObject);
            Object.DestroyImmediate(buildAsset);
            Object.DestroyImmediate(klibAsset);
        }
    }
}
}
