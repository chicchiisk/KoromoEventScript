using KoromoEventScript.Runtime.Windows.Rendering;

namespace KoromoEventScript.Runtime.Windows.Tests.Rendering;

public sealed class SceneInputMappingTests
{
    [Test]
    public void TryToProductionPoint_MapsDisplayCoordinatesToProductionCoordinates()
    {
        var viewport = SceneCoordinateMapper.CreateViewport(2560, 1080);

        var mapped = SceneCoordinateMapper.TryToProductionPoint(viewport, new ScenePoint(420, 50));

        Assert.Multiple(() =>
        {
            Assert.That(mapped, Is.Not.Null);
            Assert.That(mapped?.X, Is.EqualTo(100d));
            Assert.That(mapped?.Y, Is.EqualTo(50d));
        });
    }

    [Test]
    public void TryToProductionPoint_ReturnsNullForLetterboxMargin()
    {
        var viewport = SceneCoordinateMapper.CreateViewport(2560, 1080);

        var mapped = SceneCoordinateMapper.TryToProductionPoint(viewport, new ScenePoint(100, 50));

        Assert.That(mapped, Is.Null);
    }

    [Test]
    public void HitTest_ReturnsTopmostInteractiveRenderableInProductionCoordinates()
    {
        var state = new RuntimeSceneState(
            [
                new SceneRenderable("background", SceneLayer.Background, new SceneRect(0, 0, 1920, 1080)),
                new SceneRenderable("choice-a", SceneLayer.Choices, new SceneRect(1200, 620, 420, 80), ZIndex: 0),
                new SceneRenderable("choice-b", SceneLayer.Choices, new SceneRect(1200, 620, 420, 80), ZIndex: 1),
                new SceneRenderable("menu", SceneLayer.SystemUi, new SceneRect(1600, 0, 320, 120)),
            ]);

        var hit = SceneHitTester.HitTest(state, new ScenePoint(1240, 640));

        Assert.That(hit?.Id, Is.EqualTo("choice-b"));
    }
}
