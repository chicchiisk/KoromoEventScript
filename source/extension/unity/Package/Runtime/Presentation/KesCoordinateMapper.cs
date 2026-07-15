using UnityEngine;

namespace KoromoEventScript.Unity
{

public static class KesCoordinateMapper
{
    public const float DesignWidth = 1920f;
    public const float DesignHeight = 1080f;
    public const float FallbackPixelsPerUnit = 100f;

    public static Vector3 DesignToWorld(Camera camera, Vector2 designPosition, float worldZ = 0f)
    {
        if (camera == null)
        {
            return new Vector3(
                (designPosition.x - (DesignWidth * 0.5f)) / FallbackPixelsPerUnit,
                ((DesignHeight * 0.5f) - designPosition.y) / FallbackPixelsPerUnit,
                worldZ);
        }

        var distance = Mathf.Abs(worldZ - camera.transform.position.z);
        var world = camera.ViewportToWorldPoint(
            new Vector3(
                designPosition.x / DesignWidth,
                1f - (designPosition.y / DesignHeight),
                distance));
        world.z = worldZ;
        return world;
    }

    public static Vector2 DesignSizeToWorld(Camera camera, Vector2 designSize, float worldZ = 0f)
    {
        var topLeft = DesignToWorld(camera, Vector2.zero, worldZ);
        var bottomRight = DesignToWorld(camera, designSize, worldZ);
        return new Vector2(
            Mathf.Abs(bottomRight.x - topLeft.x),
            Mathf.Abs(bottomRight.y - topLeft.y));
    }
}
}
