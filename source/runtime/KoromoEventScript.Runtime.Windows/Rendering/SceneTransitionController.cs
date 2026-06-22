using KoromoEventScript.Runtime.Core.Diagnostics;

namespace KoromoEventScript.Runtime.Windows.Rendering;

public enum SceneTransitionKind
{
    None = 0,
    Fade = 1,
    Crossfade = 2,
}

public sealed record SceneTransitionState(
    SceneTransitionKind Kind,
    double DurationSeconds);

public sealed record SceneTransitionResult(
    SceneTransitionState? Transition,
    IReadOnlyList<RuntimeDiagnostic> Diagnostics,
    RuntimeFailureKind FailureKind)
{
    public bool Succeeded => Transition is not null && FailureKind == RuntimeFailureKind.None;

    public static SceneTransitionResult Success(SceneTransitionState transition)
    {
        return new SceneTransitionResult(transition, [], RuntimeFailureKind.None);
    }

    public static SceneTransitionResult Failure(RuntimeFailureKind failureKind, params RuntimeDiagnostic[] diagnostics)
    {
        return new SceneTransitionResult(null, diagnostics, failureKind);
    }
}

public static class SceneTransitionController
{
    public static SceneTransitionResult Create(string mode, double durationSeconds)
    {
        var kind = mode switch
        {
            "none" => SceneTransitionKind.None,
            "fade" => SceneTransitionKind.Fade,
            "crossfade" => SceneTransitionKind.Crossfade,
            _ => (SceneTransitionKind?)null,
        };

        if (kind is null)
        {
            return SceneTransitionResult.Failure(
                RuntimeFailureKind.Runtime,
                RuntimeDiagnostic.Error(
                    "KESR3501",
                    $"Transition '{mode}' is not supported.",
                    RuntimeFailureKind.Runtime));
        }

        return SceneTransitionResult.Success(new SceneTransitionState(kind.Value, Math.Max(0d, durationSeconds)));
    }
}
