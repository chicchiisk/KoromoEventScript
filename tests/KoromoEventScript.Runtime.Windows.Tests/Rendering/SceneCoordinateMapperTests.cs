using KoromoEventScript.Runtime.Windows.Rendering;

namespace KoromoEventScript.Runtime.Windows.Tests.Rendering;

public sealed class SceneCoordinateMapperTests
{
    [Test]
    public void CreateViewport_WithSixteenToNineViewport_FillsEntireDisplay()
    {
        var viewport = SceneCoordinateMapper.CreateViewport(2560, 1440);

        Assert.Multiple(() =>
        {
            Assert.That(viewport.Scale, Is.EqualTo(4d / 3d).Within(0.0001d));
            Assert.That(viewport.OffsetX, Is.EqualTo(0d));
            Assert.That(viewport.OffsetY, Is.EqualTo(0d));
            Assert.That(viewport.ContentWidth, Is.EqualTo(2560d).Within(0.0001d));
            Assert.That(viewport.ContentHeight, Is.EqualTo(1440d).Within(0.0001d));
        });
    }

    [Test]
    public void CreateViewport_WithWideViewport_CentersContentWithSideMargins()
    {
        var viewport = SceneCoordinateMapper.CreateViewport(2560, 1080);

        Assert.Multiple(() =>
        {
            Assert.That(viewport.Scale, Is.EqualTo(1d));
            Assert.That(viewport.OffsetX, Is.EqualTo(320d));
            Assert.That(viewport.OffsetY, Is.EqualTo(0d));
            Assert.That(viewport.ContentWidth, Is.EqualTo(1920d));
            Assert.That(viewport.ContentHeight, Is.EqualTo(1080d));
        });
    }

    [Test]
    public void CreateViewport_WithTallViewport_CentersContentWithTopAndBottomMargins()
    {
        var viewport = SceneCoordinateMapper.CreateViewport(1080, 1080);

        Assert.Multiple(() =>
        {
            Assert.That(viewport.Scale, Is.EqualTo(0.5625d));
            Assert.That(viewport.OffsetX, Is.EqualTo(0d));
            Assert.That(viewport.OffsetY, Is.EqualTo(236.25d));
            Assert.That(viewport.ContentWidth, Is.EqualTo(1080d));
            Assert.That(viewport.ContentHeight, Is.EqualTo(607.5d));
        });
    }

    [Test]
    public void ToDisplayRect_MapsProductionCoordinatesThroughViewport()
    {
        var viewport = SceneCoordinateMapper.CreateViewport(2560, 1080);

        var rect = SceneCoordinateMapper.ToDisplayRect(viewport, new SceneRect(100, 50, 400, 200));

        Assert.Multiple(() =>
        {
            Assert.That(rect.X, Is.EqualTo(420d));
            Assert.That(rect.Y, Is.EqualTo(50d));
            Assert.That(rect.Width, Is.EqualTo(400d));
            Assert.That(rect.Height, Is.EqualTo(200d));
        });
    }
}
