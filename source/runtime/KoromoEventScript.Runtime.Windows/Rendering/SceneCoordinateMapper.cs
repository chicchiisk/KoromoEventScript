namespace KoromoEventScript.Runtime.Windows.Rendering;

public static class SceneCoordinateMapper
{
    public const double ProductionWidth = 1920d;
    public const double ProductionHeight = 1080d;

    public static SceneViewport CreateViewport(double displayWidth, double displayHeight)
    {
        if (displayWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(displayWidth), displayWidth, "Display width must be positive.");
        }

        if (displayHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(displayHeight), displayHeight, "Display height must be positive.");
        }

        var scale = Math.Min(displayWidth / ProductionWidth, displayHeight / ProductionHeight);
        var contentWidth = ProductionWidth * scale;
        var contentHeight = ProductionHeight * scale;
        return new SceneViewport(
            displayWidth,
            displayHeight,
            scale,
            (displayWidth - contentWidth) / 2d,
            (displayHeight - contentHeight) / 2d,
            contentWidth,
            contentHeight);
    }

    public static SceneRect ToDisplayRect(SceneViewport viewport, SceneRect productionRect)
    {
        return new SceneRect(
            viewport.OffsetX + productionRect.X * viewport.Scale,
            viewport.OffsetY + productionRect.Y * viewport.Scale,
            productionRect.Width * viewport.Scale,
            productionRect.Height * viewport.Scale);
    }

    public static ScenePoint? TryToProductionPoint(SceneViewport viewport, ScenePoint displayPoint)
    {
        if (displayPoint.X < viewport.OffsetX ||
            displayPoint.Y < viewport.OffsetY ||
            displayPoint.X > viewport.OffsetX + viewport.ContentWidth ||
            displayPoint.Y > viewport.OffsetY + viewport.ContentHeight)
        {
            return null;
        }

        return new ScenePoint(
            (displayPoint.X - viewport.OffsetX) / viewport.Scale,
            (displayPoint.Y - viewport.OffsetY) / viewport.Scale);
    }
}

public readonly record struct SceneViewport(
    double DisplayWidth,
    double DisplayHeight,
    double Scale,
    double OffsetX,
    double OffsetY,
    double ContentWidth,
    double ContentHeight);

public readonly record struct SceneRect(
    double X,
    double Y,
    double Width,
    double Height)
{
    public bool Contains(ScenePoint point)
    {
        return point.X >= X &&
            point.Y >= Y &&
            point.X <= X + Width &&
            point.Y <= Y + Height;
    }
}

public readonly record struct ScenePoint(double X, double Y);
