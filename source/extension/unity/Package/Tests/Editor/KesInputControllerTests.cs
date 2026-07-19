using System;
using KoromoEventScript.Runtime.Core.Execution;
using NUnit.Framework;
using UnityEngine;

namespace KoromoEventScript.Unity.Editor.Tests
{

public sealed class KesInputControllerTests
{
    [Test]
    public void Selection_NavigateAndSubmitInOneFrame_ConsumesOnlyNavigation()
    {
        var fixture = new InputFixture(Selection("A", "B", "C"));
        try
        {
            fixture.Controller.ProcessInput(
                new KesInputFrame(advancePressed: true, submitPressed: true, navigateDownPressed: true),
                0.016f);

            Assert.That(fixture.Controller.SelectedChoiceIndex, Is.EqualTo(1));
            Assert.That(fixture.Target.ChooseCount, Is.EqualTo(0));
            Assert.That(fixture.Target.AdvanceCount, Is.EqualTo(0));

            fixture.Controller.ProcessInput(new KesInputFrame(submitPressed: true), 0.016f);

            Assert.That(fixture.Target.ChooseCount, Is.EqualTo(1));
            Assert.That(fixture.Target.LastChoiceIndex, Is.EqualTo(1));
            Assert.That(fixture.Target.AdvanceCount, Is.EqualTo(0));
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Test]
    public void MenuToggle_ConsumesSameFrameGameplayInput()
    {
        var fixture = new InputFixture(Advance());
        try
        {
            fixture.Controller.ProcessInput(
                new KesInputFrame(advancePressed: true, cancelPressed: true),
                0.016f);

            Assert.That(fixture.Controller.IsMenuOpen, Is.True);
            Assert.That(fixture.Target.AdvanceCount, Is.EqualTo(0));

            fixture.Controller.ProcessInput(
                new KesInputFrame(advancePressed: true, cancelPressed: true),
                0.016f);

            Assert.That(fixture.Controller.IsMenuOpen, Is.False);
            Assert.That(fixture.Target.AdvanceCount, Is.EqualTo(0));

            fixture.Controller.ProcessInput(new KesInputFrame(advancePressed: true), 0.016f);
            Assert.That(fixture.Target.AdvanceCount, Is.EqualTo(1));
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Test]
    public void SelectionWithoutCurrentChoice_SubmitDoesNotBranch()
    {
        var fixture = new InputFixture(Selection());
        try
        {
            fixture.Controller.ProcessInput(new KesInputFrame(submitPressed: true), 0.016f);

            Assert.That(fixture.Controller.SelectedChoiceIndex, Is.EqualTo(-1));
            Assert.That(fixture.Target.ChooseCount, Is.EqualTo(0));
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Test]
    public void SkipAndAuto_AdvanceAtConfiguredIntervalsWithoutDoubleProcessing()
    {
        var fixture = new InputFixture(Advance());
        try
        {
            fixture.Controller.SetIntervals(0.05f, 1f);
            fixture.Controller.ProcessInput(new KesInputFrame(skipHeld: true), 0.02f);
            fixture.Controller.ProcessInput(new KesInputFrame(skipHeld: true), 0.02f);
            Assert.That(fixture.Target.AdvanceCount, Is.EqualTo(0));

            fixture.Controller.ProcessInput(new KesInputFrame(skipHeld: true), 0.02f);
            Assert.That(fixture.Target.AdvanceCount, Is.EqualTo(1));

            fixture.Controller.ProcessInput(new KesInputFrame(toggleAutoPressed: true), 0.5f);
            fixture.Controller.ProcessInput(default, 0.5f);
            Assert.That(fixture.Target.AdvanceCount, Is.EqualTo(1));

            fixture.Controller.ProcessInput(default, 0.5f);
            Assert.That(fixture.Target.AdvanceCount, Is.EqualTo(2));
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Test]
    public void Context_FollowsGameplaySelectionAndMenuState()
    {
        var fixture = new InputFixture(Advance());
        try
        {
            var source = new FakeInputSource();
            fixture.Controller.SetInputSource(source);
            Assert.That(source.Context, Is.EqualTo(KesInputContext.Gameplay));

            fixture.Target.Continuation = Selection("A", "B");
            fixture.Controller.ProcessInput(default, 0f);
            Assert.That(source.Context, Is.EqualTo(KesInputContext.Selection));

            fixture.Controller.ProcessInput(new KesInputFrame(cancelPressed: true), 0f);
            Assert.That(source.Context, Is.EqualTo(KesInputContext.Menu));
        }
        finally
        {
            fixture.Dispose();
        }
    }

    private static RuntimeContinuation Advance()
    {
        return new RuntimeContinuation(
            RuntimeContinuationKind.WaitingForAdvance,
            1,
            Array.Empty<int>(),
            null,
            Array.Empty<RuntimeSelectionChoice>());
    }

    private static RuntimeContinuation Selection(params string[] labels)
    {
        var offsets = new int[labels.Length];
        var choices = new RuntimeSelectionChoice[labels.Length];
        for (var i = 0; i < labels.Length; i++)
        {
            offsets[i] = i;
            choices[i] = new RuntimeSelectionChoice(labels[i], i);
        }

        return new RuntimeContinuation(
            RuntimeContinuationKind.WaitingForSelection,
            null,
            offsets,
            "Select",
            choices);
    }

    private sealed class InputFixture : IDisposable
    {
        private readonly GameObject root;

        public InputFixture(RuntimeContinuation continuation)
        {
            root = new GameObject("KesInputControllerTest");
            Controller = root.AddComponent<KesInputController>();
            Target = new FakeInputTarget { Continuation = continuation };
            Controller.SetTarget(Target);
        }

        public KesInputController Controller { get; }

        public FakeInputTarget Target { get; }

        public void Dispose()
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private sealed class FakeInputTarget : IKesInputTarget
    {
        public RuntimeContinuation Continuation { get; set; }

        public int AdvanceCount { get; private set; }

        public int ChooseCount { get; private set; }

        public int LastChoiceIndex { get; private set; } = -1;

        public bool ContinueAdvance()
        {
            AdvanceCount++;
            return true;
        }

        public bool ChooseSelection(int choiceIndex)
        {
            ChooseCount++;
            LastChoiceIndex = choiceIndex;
            return true;
        }
    }

    private sealed class FakeInputSource : IKesInputSource
    {
        public KesInputContext Context { get; private set; }

        public KesInputFrame ReadFrame()
        {
            return default;
        }

        public void SetContext(KesInputContext context)
        {
            Context = context;
        }
    }
}
}
