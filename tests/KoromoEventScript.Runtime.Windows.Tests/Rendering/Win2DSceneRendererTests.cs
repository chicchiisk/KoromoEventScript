using KoromoEventScript.Runtime.Core.Effects;
using KoromoEventScript.Runtime.Windows.Rendering;

namespace KoromoEventScript.Runtime.Windows.Tests.Rendering;

public sealed class Win2DSceneRendererTests
{
    [Test]
    public void BuildRenderPlan_OrdersLogicalLayersForCompositing()
    {
        var renderer = new Win2DSceneRenderer();
        renderer.Apply(
            new RuntimeSceneState(
                [
                    new SceneRenderable("system-menu", SceneLayer.SystemUi, new SceneRect(0, 0, 1920, 1080)),
                    new SceneRenderable("background", SceneLayer.Background, new SceneRect(0, 0, 1920, 1080)),
                    new SceneRenderable("choice-list", SceneLayer.Choices, new SceneRect(1280, 640, 400, 240)),
                    new SceneRenderable("noa", SceneLayer.Actor, new SceneRect(620, 120, 680, 960)),
                    new SceneRenderable("message", SceneLayer.Text, new SceneRect(160, 760, 1600, 240)),
                    new SceneRenderable("fade", SceneLayer.Effects, new SceneRect(0, 0, 1920, 1080)),
                ]));

        var plan = renderer.BuildRenderPlan(SceneCoordinateMapper.CreateViewport(1280, 720));

        Assert.That(
            plan.Items.Select(static item => item.Renderable.Id),
            Is.EqualTo(["background", "noa", "fade", "message", "choice-list", "system-menu"]));
    }

    [Test]
    public void BuildRenderPlan_MapsRenderableBoundsToDisplayCoordinates()
    {
        var renderer = new Win2DSceneRenderer();
        renderer.Apply(
            new RuntimeSceneState(
                [
                    new SceneRenderable("message", SceneLayer.Text, new SceneRect(160, 760, 1600, 240)),
                ]));

        var plan = renderer.BuildRenderPlan(SceneCoordinateMapper.CreateViewport(2560, 1080));

        Assert.Multiple(() =>
        {
            Assert.That(plan.Items.Single().DisplayBounds.X, Is.EqualTo(480d));
            Assert.That(plan.Items.Single().DisplayBounds.Y, Is.EqualTo(760d));
            Assert.That(plan.Items.Single().DisplayBounds.Width, Is.EqualTo(1600d));
            Assert.That(plan.Items.Single().DisplayBounds.Height, Is.EqualTo(240d));
        });
    }

    [Test]
    public void RuntimeSceneStateController_UsesActorAssetBaseNameAndFaceForAssetId()
    {
        var controller = new RuntimeSceneStateController();

        controller.Apply(SceneEffect(
            "actor.show",
            new Dictionary<string, string?>
            {
                ["actor"] = "actor.riku",
                ["assetBaseName"] = "riku",
                ["face"] = "normal",
                ["pos"] = "0",
            }));
        controller.Apply(SceneEffect(
            "actor.face",
            new Dictionary<string, string?>
            {
                ["actor"] = "actor.riku",
                ["assetBaseName"] = "riku",
                ["exp"] = "smile",
            }));

        var actor = controller.State.Renderables.Single(static renderable => renderable.Id == "actor.riku");
        Assert.Multiple(() =>
        {
            Assert.That(actor.Layer, Is.EqualTo(SceneLayer.Actor));
            Assert.That(actor.AssetId, Is.EqualTo("assets.actor.riku_smile"));
            Assert.That(actor.Properties?["assetBaseName"], Is.EqualTo("riku"));
            Assert.That(actor.Properties?["face"], Is.EqualTo("smile"));
        });
    }

    private static RuntimeEffect SceneEffect(string name, IReadOnlyDictionary<string, string?> payload)
    {
        return new RuntimeEffect(RuntimeEffectKind.Scene, name, payload);
    }
}
