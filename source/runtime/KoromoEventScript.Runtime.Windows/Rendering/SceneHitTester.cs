namespace KoromoEventScript.Runtime.Windows.Rendering;

public static class SceneHitTester
{
    public static SceneRenderable? HitTest(RuntimeSceneState sceneState, ScenePoint productionPoint)
    {
        ArgumentNullException.ThrowIfNull(sceneState);

        return sceneState.Renderables
            .Where(static renderable => renderable.Layer is SceneLayer.Choices or SceneLayer.SystemUi)
            .Where(renderable => renderable.Bounds.Contains(productionPoint))
            .OrderByDescending(static renderable => renderable.Layer)
            .ThenByDescending(static renderable => renderable.ZIndex)
            .ThenBy(static renderable => renderable.Id, StringComparer.Ordinal)
            .FirstOrDefault();
    }
}
