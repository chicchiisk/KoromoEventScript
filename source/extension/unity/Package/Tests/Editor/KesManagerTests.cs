using KoromoEventScript.Runtime.Core.Diagnostics;
using KoromoEventScript.Runtime.Core.Execution;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

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
            Assert.That(manager.ActiveLocale, Is.EqualTo("ja-JP"));
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

    [Test]
    public void Play_ExistingLocaleVariantUsesExactMatch()
    {
        var gameObject = new GameObject("KesManagerLocaleVariantTest");
        var defaultKlibAsset = ScriptableObject.CreateInstance<KesKlibAsset>();
        var variantKlibAsset = ScriptableObject.CreateInstance<KesKlibAsset>();
        var buildAsset = ScriptableObject.CreateInstance<KesBuildAsset>();

        try
        {
            defaultKlibAsset.name = "default";
            defaultKlibAsset.SetImportedData(KesImporterTests.BuildMinimalKlib("events/default"));
            variantKlibAsset.name = "english";
            variantKlibAsset.SetImportedData(KesImporterTests.BuildMinimalKlib("events/english"));
            buildAsset.SetImportedData(
                "{}",
                "1.0",
                "manager-locale-test",
                "manager-locale-test-1",
                "ja-JP",
                new[]
                {
                    new KesScriptAssetReference("events/default", "ja-JP", defaultKlibAsset),
                    new KesScriptAssetReference("events/english", "en-US", variantKlibAsset),
                });

            var manager = gameObject.AddComponent<KesManager>();
            manager.SetBuildAsset(buildAsset);
            manager.SetLocale("en-US");

            var started = manager.Play();

            Assert.That(started, Is.True);
            Assert.That(manager.Session.Document.Module.ScriptId, Is.EqualTo("events/english"));
            Assert.That(manager.ActiveLocale, Is.EqualTo("en-US"));
            Assert.That(manager.LastDiagnostics, Is.Empty);
        }
        finally
        {
            Object.DestroyImmediate(gameObject);
            Object.DestroyImmediate(buildAsset);
            Object.DestroyImmediate(variantKlibAsset);
            Object.DestroyImmediate(defaultKlibAsset);
        }
    }

    [Test]
    public void Play_MissingLocaleFallsBackToDefaultAndReportsWarning()
    {
        var gameObject = new GameObject("KesManagerLocaleFallbackTest");
        var klibAsset = ScriptableObject.CreateInstance<KesKlibAsset>();
        var buildAsset = ScriptableObject.CreateInstance<KesBuildAsset>();

        try
        {
            klibAsset.name = "chapter001";
            klibAsset.SetImportedData(KesImporterTests.BuildMinimalKlib());
            buildAsset.SetImportedData(
                "{}",
                "1.0",
                "manager-fallback-test",
                "manager-fallback-test-1",
                "ja-JP",
                new[]
                {
                    new KesScriptAssetReference("events/chapter001", "ja-JP", klibAsset),
                });

            var manager = gameObject.AddComponent<KesManager>();
            manager.SetBuildAsset(buildAsset);
            manager.SetLocale("fr-FR");
            LogAssert.Expect(
                LogType.Warning,
                "KESU2005: Locale 'fr-FR' was not found. Falling back to default locale 'ja-JP'.");

            var started = manager.Play();

            Assert.That(started, Is.True);
            Assert.That(manager.Session.Document.Module.ScriptId, Is.EqualTo("events/chapter001"));
            Assert.That(manager.ActiveLocale, Is.EqualTo("ja-JP"));
            Assert.That(manager.LastDiagnostics, Has.Count.EqualTo(1));
            Assert.That(manager.LastDiagnostics[0].Code, Is.EqualTo("KESU2005"));
            Assert.That(manager.LastDiagnostics[0].Severity, Is.EqualTo(RuntimeDiagnosticSeverity.Warning));
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
