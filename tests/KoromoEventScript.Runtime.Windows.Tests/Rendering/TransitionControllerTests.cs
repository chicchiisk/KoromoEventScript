using KoromoEventScript.Runtime.Core.Diagnostics;
using KoromoEventScript.Runtime.Windows.Rendering;

namespace KoromoEventScript.Runtime.Windows.Tests.Rendering;

public sealed class TransitionControllerTests
{
    [TestCase("fade", SceneTransitionKind.Fade)]
    [TestCase("crossfade", SceneTransitionKind.Crossfade)]
    [TestCase("none", SceneTransitionKind.None)]
    public void Create_WithKnownTransition_ReturnsTransitionState(string mode, SceneTransitionKind expectedKind)
    {
        var result = SceneTransitionController.Create(mode, 0.5d);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Transition?.Kind, Is.EqualTo(expectedKind));
            Assert.That(result.Transition?.DurationSeconds, Is.EqualTo(0.5d));
        });
    }

    [Test]
    public void Create_WithUnknownTransition_ReturnsRuntimeError()
    {
        var result = SceneTransitionController.Create("wipe", 0.25d);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailureKind, Is.EqualTo(RuntimeFailureKind.Runtime));
            Assert.That(result.Diagnostics.Single().FailureKind, Is.EqualTo(RuntimeFailureKind.Runtime));
            Assert.That(result.Diagnostics.Single().Code, Is.EqualTo("KESR3501"));
        });
    }
}
