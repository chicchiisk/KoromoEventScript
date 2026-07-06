using KoromoEventScript.Runtime.Core.Effects;

namespace KoromoEventScript.Runtime.Windows.Rendering;

public sealed class RuntimeSceneStateController
{
    private const double ActorWidth = 680;
    private const double ActorHeight = 960;
    private const double ActorTop = 120;
    private const double ActorCenter = 960;
    private const double ActorPositionStep = 420;

    private readonly Dictionary<string, ActorSceneModel> actors = new(StringComparer.Ordinal);
    private SceneRenderable? background;

    public RuntimeSceneState State => new(
        background is null
            ? actors.Values.Select(static actor => actor.ToRenderable()).ToArray()
            : [background, .. actors.Values.Select(static actor => actor.ToRenderable())]);

    public void Apply(RuntimeEffect effect)
    {
        ArgumentNullException.ThrowIfNull(effect);
        if (effect.Kind != RuntimeEffectKind.Scene)
        {
            return;
        }

        switch (effect.Name)
        {
            case "scene.bg":
                ApplyBackground(effect.Payload);
                break;

            case "actor.show":
                ApplyActorShow(effect.Payload);
                break;

            case "actor.face":
                ApplyActorFace(effect.Payload);
                break;

            case "actor.hide":
                if (TryGetActor(effect.Payload, out var actorId))
                {
                    actors.Remove(actorId);
                }
                break;

            case "actor.move":
                ApplyActorMove(effect.Payload);
                break;
        }
    }

    private void ApplyBackground(IReadOnlyDictionary<string, string?> payload)
    {
        var backgroundId = payload.TryGetValue("id", out var id) && !string.IsNullOrWhiteSpace(id)
            ? id!
            : "background";
        background = new SceneRenderable(
            "background",
            SceneLayer.Background,
            new SceneRect(0, 0, 1920, 1080),
            AssetId: $"assets.bg.{backgroundId}",
            Properties: new Dictionary<string, string?> { ["id"] = backgroundId });
    }

    private void ApplyActorShow(IReadOnlyDictionary<string, string?> payload)
    {
        if (!TryGetActor(payload, out var actorId))
        {
            return;
        }

        var assetBaseName = ReadPayload(payload, "assetBaseName", NormalizeActorReference(actorId));
        var face = ReadPayload(payload, "face", "normal");
        var position = ReadNumber(payload, "pos", 0);
        var zIndex = (int)ReadNumber(payload, "z", 0);
        actors[actorId] = new ActorSceneModel(actorId, assetBaseName, face, position, zIndex);
    }

    private void ApplyActorFace(IReadOnlyDictionary<string, string?> payload)
    {
        if (!TryGetActor(payload, out var actorId) || !actors.TryGetValue(actorId, out var actor))
        {
            return;
        }

        var assetBaseName = ReadPayload(payload, "assetBaseName", actor.AssetBaseName);
        var face = ReadPayload(payload, "exp", ReadPayload(payload, "face", actor.Face));
        actors[actorId] = actor with
        {
            AssetBaseName = assetBaseName,
            Face = face,
        };
    }

    private void ApplyActorMove(IReadOnlyDictionary<string, string?> payload)
    {
        if (!TryGetActor(payload, out var actorId) || !actors.TryGetValue(actorId, out var actor))
        {
            return;
        }

        actors[actorId] = actor with
        {
            Position = ReadNumber(payload, "pos", actor.Position),
        };
    }

    private static bool TryGetActor(IReadOnlyDictionary<string, string?> payload, out string actorId)
    {
        actorId = string.Empty;
        if (!payload.TryGetValue("actor", out var actor) || string.IsNullOrWhiteSpace(actor))
        {
            return false;
        }

        actorId = actor!;
        return true;
    }

    private static string ReadPayload(IReadOnlyDictionary<string, string?> payload, string key, string fallback)
    {
        return payload.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value!
            : fallback;
    }

    private static double ReadNumber(IReadOnlyDictionary<string, string?> payload, string key, double fallback)
    {
        return payload.TryGetValue(key, out var value) &&
            double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var number)
            ? number
            : fallback;
    }

    private static string NormalizeActorReference(string actorReference)
    {
        var dotIndex = actorReference.LastIndexOf('.');
        return dotIndex >= 0 && dotIndex + 1 < actorReference.Length
            ? actorReference[(dotIndex + 1)..]
            : actorReference;
    }

    private sealed record ActorSceneModel(
        string ActorId,
        string AssetBaseName,
        string Face,
        double Position,
        int ZIndex)
    {
        public SceneRenderable ToRenderable()
        {
            var centerX = ActorCenter + (Position * ActorPositionStep);
            var properties = new Dictionary<string, string?>
            {
                ["actor"] = ActorId,
                ["assetBaseName"] = AssetBaseName,
                ["face"] = Face,
                ["pos"] = Position.ToString("G", System.Globalization.CultureInfo.InvariantCulture),
            };

            return new SceneRenderable(
                ActorId,
                SceneLayer.Actor,
                new SceneRect(centerX - (ActorWidth / 2), ActorTop, ActorWidth, ActorHeight),
                ZIndex,
                $"assets.actor.{AssetBaseName}_{Face}",
                properties);
        }
    }
}
