namespace KoromoEventScript.Runtime.Windows.Rendering;

public enum SceneLayer
{
    Background = 0,
    Actor = 10,
    Effects = 20,
    Text = 30,
    Choices = 40,
    SystemUi = 50,
}

public sealed record SceneRenderable(
    string Id,
    SceneLayer Layer,
    SceneRect Bounds,
    int ZIndex = 0,
    string? AssetId = null,
    IReadOnlyDictionary<string, string?>? Properties = null);

public sealed record RuntimeSceneState(IReadOnlyList<SceneRenderable> Renderables)
{
    public static RuntimeSceneState Empty { get; } = new([]);
}

public sealed record SceneRenderPlan(IReadOnlyList<SceneRenderPlanItem> Items);

public sealed record SceneRenderPlanItem(
    SceneRenderable Renderable,
    SceneRect DisplayBounds);

public interface ISceneRenderer
{
    void Apply(RuntimeSceneState sceneState);

    SceneRenderPlan BuildRenderPlan(SceneViewport viewport);
}
