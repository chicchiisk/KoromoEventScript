using KoromoEventScript.Runtime.Core.Diagnostics;
using KoromoEventScript.Runtime.Core.Execution;
using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

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
    public void NewComponent_ExecutionSourceLoggingIsDisabled()
    {
        var gameObject = new GameObject("KesManagerTraceDefaultTest");

        try
        {
            var manager = gameObject.AddComponent<KesManager>();
            Assert.That(manager.LogExecutionSource, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(gameObject);
        }
    }

    [Test]
    public void Play_ExecutionSourceLoggingEnabled_LogsMappedFileLineAndInstruction()
    {
        var gameObject = new GameObject("KesManagerTraceTest");
        var klibAsset = ScriptableObject.CreateInstance<KesKlibAsset>();
        var buildAsset = ScriptableObject.CreateInstance<KesBuildAsset>();

        try
        {
            klibAsset.name = "chapter001";
            klibAsset.SetImportedData(KesImporterTests.BuildMinimalKlib(
                "events/chapter001",
                sourceLine: 42,
                sourceColumn: 7));
            buildAsset.SetImportedData(
                "{}",
                "1.0",
                "manager-trace-test",
                "manager-trace-test-1",
                "ja-JP",
                new[]
                {
                    new KesScriptAssetReference("events/chapter001", "ja-JP", klibAsset),
                });

            var manager = gameObject.AddComponent<KesManager>();
            manager.SetBuildAsset(buildAsset);
            manager.SetLogExecutionSource(true);
            LogAssert.Expect(
                LogType.Log,
                "[KES TRACE] events/chapter001.kc:42:7 [End @bytecode:0]");

            Assert.That(manager.Play(), Is.True);
            Assert.That(manager.Continuation.Kind, Is.EqualTo(RuntimeContinuationKind.Completed));
        }
        finally
        {
            Object.DestroyImmediate(gameObject);
            Object.DestroyImmediate(buildAsset);
            Object.DestroyImmediate(klibAsset);
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

    [Test]
    public void Play_CompletedEntryEventStartsFirstMatchingNextEvent()
    {
        var gameObject = new GameObject("KesManagerEventTransitionTest");
        var firstKlib = ScriptableObject.CreateInstance<KesKlibAsset>();
        var secondKlib = ScriptableObject.CreateInstance<KesKlibAsset>();
        var ignoredKlib = ScriptableObject.CreateInstance<KesKlibAsset>();
        var buildAsset = ScriptableObject.CreateInstance<KesBuildAsset>();

        try
        {
            firstKlib.name = "chapter001";
            firstKlib.SetImportedData(KesImporterTests.BuildMinimalKlib("events/chapter001"));
            secondKlib.name = "chapter002";
            secondKlib.SetImportedData(KesImporterTests.BuildMinimalKlib("events/chapter002"));
            ignoredKlib.name = "chapter003";
            ignoredKlib.SetImportedData(KesImporterTests.BuildMinimalKlib("events/chapter003"));
            buildAsset.SetImportedData(
                "{}",
                "1.0",
                "manager-event-test",
                "manager-event-test-1",
                "ja-JP",
                new[]
                {
                    new KesScriptAssetReference("events/chapter001", "ja-JP", firstKlib, isEntry: true),
                    new KesScriptAssetReference("events/chapter002", "ja-JP", secondKlib),
                    new KesScriptAssetReference("events/chapter003", "ja-JP", ignoredKlib),
                },
                new[]
                {
                    Event("chapter001_intro", "events/chapter001", true),
                    Event("chapter002_intro", "events/chapter002", false, From("chapter001_intro")),
                    Event("chapter003_intro", "events/chapter003", false, From("chapter001_intro")),
                });

            var manager = gameObject.AddComponent<KesManager>();
            manager.SetBuildAsset(buildAsset);

            var started = manager.Play();

            Assert.That(started, Is.True);
            Assert.That(manager.CurrentEventId, Is.EqualTo("chapter002_intro"));
            Assert.That(manager.Session.Document.Module.ScriptId, Is.EqualTo("events/chapter002"));
            Assert.That(manager.Continuation.Kind, Is.EqualTo(RuntimeContinuationKind.Completed));
            Assert.That(manager.LastDiagnostics, Is.Empty);
        }
        finally
        {
            Object.DestroyImmediate(gameObject);
            Object.DestroyImmediate(buildAsset);
            Object.DestroyImmediate(ignoredKlib);
            Object.DestroyImmediate(secondKlib);
            Object.DestroyImmediate(firstKlib);
        }
    }

    [Test]
    public void Play_NoMatchingNextEventCompletesCurrentEventNormally()
    {
        var gameObject = new GameObject("KesManagerNoEventTransitionTest");
        var klib = ScriptableObject.CreateInstance<KesKlibAsset>();
        var buildAsset = ScriptableObject.CreateInstance<KesBuildAsset>();

        try
        {
            klib.name = "chapter001";
            klib.SetImportedData(KesImporterTests.BuildMinimalKlib("events/chapter001"));
            buildAsset.SetImportedData(
                "{}",
                "1.0",
                "manager-event-end-test",
                "manager-event-end-test-1",
                "ja-JP",
                new[] { new KesScriptAssetReference("events/chapter001", "ja-JP", klib, isEntry: true) },
                new[] { Event("chapter001_intro", "events/chapter001", true) });

            var manager = gameObject.AddComponent<KesManager>();
            manager.SetBuildAsset(buildAsset);

            Assert.That(manager.Play(), Is.True);
            Assert.That(manager.CurrentEventId, Is.EqualTo("chapter001_intro"));
            Assert.That(manager.Continuation.Kind, Is.EqualTo(RuntimeContinuationKind.Completed));
            Assert.That(manager.LastDiagnostics, Is.Empty);
        }
        finally
        {
            Object.DestroyImmediate(gameObject);
            Object.DestroyImmediate(buildAsset);
            Object.DestroyImmediate(klib);
        }
    }

    [Test]
    public void Play_SynchronousEventLoopStopsWithDiagnostic()
    {
        var gameObject = new GameObject("KesManagerEventLoopTest");
        var klib = ScriptableObject.CreateInstance<KesKlibAsset>();
        var buildAsset = ScriptableObject.CreateInstance<KesBuildAsset>();

        try
        {
            klib.name = "chapter001";
            klib.SetImportedData(KesImporterTests.BuildMinimalKlib("events/chapter001"));
            buildAsset.SetImportedData(
                "{}",
                "1.0",
                "manager-event-loop-test",
                "manager-event-loop-test-1",
                "ja-JP",
                new[] { new KesScriptAssetReference("events/chapter001", "ja-JP", klib, isEntry: true) },
                new[] { Event("chapter001_intro", "events/chapter001", true, From("chapter001_intro")) });

            var manager = gameObject.AddComponent<KesManager>();
            manager.SetBuildAsset(buildAsset);
            LogAssert.Expect(
                LogType.Error,
                "KESU2009: Event transitions exceeded the synchronous transition limit before reaching an input wait.");

            Assert.That(manager.Play(), Is.False);
            Assert.That(manager.LastDiagnostics, Has.Count.EqualTo(1));
            Assert.That(manager.LastDiagnostics[0].Code, Is.EqualTo("KESU2009"));
        }
        finally
        {
            Object.DestroyImmediate(gameObject);
            Object.DestroyImmediate(buildAsset);
            Object.DestroyImmediate(klib);
        }
    }

    private static KesEventAssetReference Event(
        string eventId,
        string scriptId,
        bool isEntry,
        KesTriggerAssetReference trigger = null)
    {
        return new KesEventAssetReference(eventId, "story", scriptId + ".kc", scriptId, isEntry, trigger);
    }

    private static KesTriggerAssetReference From(string eventId)
    {
        return new KesTriggerAssetReference(
            new[]
            {
                new KesTriggerConditionAssetReference("from", eventId, null, null),
            },
            Array.Empty<KesTriggerAssetReference>());
    }
}
}
