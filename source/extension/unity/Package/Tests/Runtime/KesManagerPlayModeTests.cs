using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System;
using KoromoEventScript.Runtime.Core.Execution;
using KoromoEventScript.Runtime.Core.Klib;
using System.IO;
using System.Text;

namespace KoromoEventScript.Unity.Runtime.Tests
{

public sealed class KesManagerPlayModeTests
{
    [UnityTest]
    public IEnumerator Component_CanEnterPlayMode()
    {
        var gameObject = new GameObject("KesManagerPlayModeTest");
        var manager = gameObject.AddComponent<KesManager>();
        manager.SetPlayOnStart(false);

        yield return null;

        Assert.That(manager.isActiveAndEnabled, Is.True);
        UnityEngine.Object.Destroy(gameObject);
    }

    [UnityTest]
    public IEnumerator InputController_OneFrameProducesAtMostOneTransition()
    {
        var gameObject = new GameObject("KesInputControllerPlayModeTest");
        var controller = gameObject.AddComponent<KesInputController>();
        var target = new FakeInputTarget();
        controller.SetTarget(target);

        controller.ProcessInput(
            new KesInputFrame(advancePressed: true, submitPressed: true, navigateDownPressed: true),
            0.016f);
        yield return null;

        Assert.That(target.ChooseCount, Is.EqualTo(0));
        Assert.That(target.AdvanceCount, Is.EqualTo(0));
        Assert.That(controller.SelectedChoiceIndex, Is.EqualTo(1));
        UnityEngine.Object.Destroy(gameObject);
    }

    [UnityTest]
    public IEnumerator Manager_CompletedEventStartsMatchingNextEvent()
    {
        var gameObject = new GameObject("KesManagerEventTransitionPlayModeTest");
        var firstKlib = ScriptableObject.CreateInstance<KesKlibAsset>();
        var secondKlib = ScriptableObject.CreateInstance<KesKlibAsset>();
        var buildAsset = ScriptableObject.CreateInstance<KesBuildAsset>();
        try
        {
            firstKlib.name = "chapter001";
            firstKlib.SetImportedData(BuildMinimalKlib("events/chapter001"));
            secondKlib.name = "chapter002";
            secondKlib.SetImportedData(BuildMinimalKlib("events/chapter002"));
            buildAsset.SetImportedData(
                "{}",
                "1.0",
                "play-mode-event-test",
                "play-mode-event-test-1",
                "ja-JP",
                new[]
                {
                    new KesScriptAssetReference("events/chapter001", "ja-JP", firstKlib, isEntry: true),
                    new KesScriptAssetReference("events/chapter002", "ja-JP", secondKlib),
                },
                new[]
                {
                    new KesEventAssetReference("chapter001_intro", "story", "events/chapter001.kc", "events/chapter001", true, null),
                    new KesEventAssetReference(
                        "chapter002_intro",
                        "story",
                        "events/chapter002.kc",
                        "events/chapter002",
                        false,
                        new KesTriggerAssetReference(
                            new[] { new KesTriggerConditionAssetReference("from", "chapter001_intro", null, null) },
                            Array.Empty<KesTriggerAssetReference>())),
                });

            var manager = gameObject.AddComponent<KesManager>();
            manager.SetPlayOnStart(false);
            manager.SetBuildAsset(buildAsset);

            Assert.That(manager.Play(), Is.True);
            yield return null;

            Assert.That(manager.CurrentEventId, Is.EqualTo("chapter002_intro"));
            Assert.That(manager.Session.Document.Module.ScriptId, Is.EqualTo("events/chapter002"));
            Assert.That(manager.Continuation.Kind, Is.EqualTo(RuntimeContinuationKind.Completed));
            Assert.That(manager.LastDiagnostics, Is.Empty);
        }
        finally
        {
            UnityEngine.Object.Destroy(gameObject);
            UnityEngine.Object.Destroy(buildAsset);
            UnityEngine.Object.Destroy(secondKlib);
            UnityEngine.Object.Destroy(firstKlib);
        }
    }

    [UnityTest]
    public IEnumerator Manager_SystemWaitDoesNotExecuteFollowingInstructionBeforeHostCompletion()
    {
        var gameObject = new GameObject("KesManagerHostWaitPlayModeTest");
        var klib = ScriptableObject.CreateInstance<KesKlibAsset>();
        var buildAsset = ScriptableObject.CreateInstance<KesBuildAsset>();
        try
        {
            klib.SetImportedData(BuildWaitKlib("events/wait", 0.1));
            buildAsset.SetImportedData(
                "{}",
                "1.0",
                "host-wait-test",
                "host-wait-test-1",
                "ja-JP",
                new[]
                {
                    new KesScriptAssetReference("events/wait", "ja-JP", klib, isEntry: true),
                });

            var manager = gameObject.AddComponent<KesManager>();
            manager.SetPlayOnStart(false);
            manager.SetBuildAsset(buildAsset);

            Assert.That(manager.Play(), Is.True);
            Assert.That(manager.Continuation.Kind, Is.EqualTo(RuntimeContinuationKind.WaitingForHost));
            Assert.That(manager.Session.OperandStack, Is.Empty);

            yield return new WaitForSecondsRealtime(0.15f);

            Assert.That(manager.Continuation.Kind, Is.EqualTo(RuntimeContinuationKind.Completed));
            Assert.That(manager.Session.OperandStack.Count, Is.EqualTo(1));
            Assert.That(manager.Session.OperandStack[0].NumberValue, Is.EqualTo(7));
        }
        finally
        {
            UnityEngine.Object.Destroy(gameObject);
            UnityEngine.Object.Destroy(buildAsset);
            UnityEngine.Object.Destroy(klib);
        }
    }

    private static byte[] BuildMinimalKlib(string scriptId)
    {
        var sections = new[]
        {
            Section(0x0001, writer =>
            {
                WriteString(writer, scriptId);
                WriteString(writer, scriptId);
                WriteString(writer, scriptId + ".kc");
                writer.Write(0);
            }),
            Section(0x0002, writer => writer.Write(0)),
            Section(0x0003, writer => writer.Write(0)),
            Section(0x0005, writer =>
            {
                writer.Write(1);
                writer.Write((byte)KlibOpCode.End);
            }),
            Section(0x0006, writer => writer.Write(0)),
            Section(0x0007, writer =>
            {
                writer.Write(0);
                writer.Write(0);
                writer.Write(0);
            }),
        };

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8);
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

    private static byte[] BuildWaitKlib(string scriptId, double seconds)
    {
        var sections = new[]
        {
            Section(0x0001, writer =>
            {
                WriteString(writer, scriptId);
                WriteString(writer, scriptId);
                WriteString(writer, scriptId + ".kc");
                writer.Write(0);
            }),
            Section(0x0002, writer =>
            {
                writer.Write(2);
                writer.Write((int)KlibConstantKind.String);
                WriteString(writer, "system.wait");
                writer.Write((int)KlibConstantKind.Number);
                writer.Write(seconds);
            }),
            Section(0x0003, writer => writer.Write(0)),
            Section(0x0005, writer =>
            {
                using var bytecode = new MemoryStream();
                using (var bytecodeWriter = new BinaryWriter(bytecode, Encoding.UTF8, leaveOpen: true))
                {
                    bytecodeWriter.Write((byte)KlibOpCode.PushConst);
                    bytecodeWriter.Write(1);
                    bytecodeWriter.Write((byte)KlibOpCode.SysCallVoid);
                    bytecodeWriter.Write(0);
                    bytecodeWriter.Write(1);
                    bytecodeWriter.Write((byte)KlibOpCode.PushInt);
                    bytecodeWriter.Write(7);
                    bytecodeWriter.Write((byte)KlibOpCode.End);
                }

                var bytes = bytecode.ToArray();
                writer.Write(bytes.Length);
                writer.Write(bytes);
            }),
            Section(0x0006, writer => writer.Write(0)),
            Section(0x0007, writer =>
            {
                writer.Write(0);
                writer.Write(0);
                writer.Write(0);
            }),
        };

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8);
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

    private static KlibSection Section(int type, Action<BinaryWriter> write)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8);
        write(writer);
        return new KlibSection(type, stream.ToArray());
    }

    private static void WriteString(BinaryWriter writer, string value)
    {
        var data = Encoding.UTF8.GetBytes(value);
        writer.Write(data.Length);
        writer.Write(data);
    }

    private sealed class FakeInputTarget : IKesInputTarget
    {
        public RuntimeContinuation Continuation { get; } = new(
            RuntimeContinuationKind.WaitingForSelection,
            null,
            new[] { 1, 2 },
            "Select",
            new[]
            {
                new RuntimeSelectionChoice("A", 1),
                new RuntimeSelectionChoice("B", 2),
            });

        public int AdvanceCount { get; private set; }

        public int ChooseCount { get; private set; }

        public bool ContinueAdvance()
        {
            AdvanceCount++;
            return true;
        }

        public bool ChooseSelection(int choiceIndex)
        {
            ChooseCount++;
            return true;
        }
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
