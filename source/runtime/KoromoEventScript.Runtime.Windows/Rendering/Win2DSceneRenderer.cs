namespace KoromoEventScript.Runtime.Windows.Rendering;

public sealed class Win2DSceneRenderer : ISceneRenderer
{
    private RuntimeSceneState sceneState = RuntimeSceneState.Empty;

    public void Apply(RuntimeSceneState sceneState)
    {
        ArgumentNullException.ThrowIfNull(sceneState);
        this.sceneState = sceneState;
    }

    public SceneRenderPlan BuildRenderPlan(SceneViewport viewport)
    {
        var items = sceneState.Renderables
            .OrderBy(static renderable => renderable.Layer)
            .ThenBy(static renderable => renderable.ZIndex)
            .ThenBy(static renderable => renderable.Id, StringComparer.Ordinal)
            .Select(renderable => new SceneRenderPlanItem(
                renderable,
                SceneCoordinateMapper.ToDisplayRect(viewport, renderable.Bounds)))
            .ToArray();

        return new SceneRenderPlan(items);
    }
}
