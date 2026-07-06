using Windows.Media.Core;
using Windows.Media.Playback;

namespace KoromoEventScript.Runtime.Windows.Audio;

public sealed class MediaPlayerAudioPlaybackBackend : IAudioPlaybackBackend, IDisposable
{
    private readonly List<(string AssetId, MediaPlayer Player)> soundEffects = [];
    private MediaPlayer? bgmPlayer;
    private MediaPlayer? voicePlayer;

    public Task PlayAsync(AudioPlaybackRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var player = new MediaPlayer
        {
            Source = MediaSource.CreateFromUri(new Uri(request.Asset.ResolvedPath)),
            IsLoopingEnabled = request.Options.Loop,
            Volume = request.Options.Volume,
        };

        switch (request.Channel)
        {
            case AudioChannel.Bgm:
                DisposePlayer(bgmPlayer);
                bgmPlayer = player;
                break;

            case AudioChannel.Se:
                soundEffects.Add((request.Asset.AssetId, player));
                player.MediaEnded += (_, _) => RemoveSoundEffect(player);
                break;

            case AudioChannel.Voice:
                DisposePlayer(voicePlayer);
                voicePlayer = player;
                break;
        }

        player.Play();
        return Task.CompletedTask;
    }

    public Task StopAsync(AudioStopRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        switch (request.Channel)
        {
            case AudioChannel.Bgm:
                DisposePlayer(bgmPlayer);
                bgmPlayer = null;
                break;

            case AudioChannel.Se:
                StopSoundEffects(request.AssetId);
                break;

            case AudioChannel.Voice:
                DisposePlayer(voicePlayer);
                voicePlayer = null;
                break;
        }

        return Task.CompletedTask;
    }

    public Task SetVolumeAsync(AudioVolumeChange change, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        switch (change.Channel)
        {
            case AudioChannel.Bgm when bgmPlayer is not null:
                bgmPlayer.Volume = change.EffectiveVolume;
                break;

            case AudioChannel.Se:
                foreach (var (_, player) in soundEffects)
                {
                    player.Volume = change.EffectiveVolume;
                }

                break;

            case AudioChannel.Voice when voicePlayer is not null:
                voicePlayer.Volume = change.EffectiveVolume;
                break;
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        DisposePlayer(bgmPlayer);
        DisposePlayer(voicePlayer);
        StopSoundEffects(assetId: null);
    }

    private void StopSoundEffects(string? assetId)
    {
        for (var index = soundEffects.Count - 1; index >= 0; index--)
        {
            var (currentAssetId, player) = soundEffects[index];
            if (assetId is null || StringComparer.Ordinal.Equals(currentAssetId, assetId))
            {
                soundEffects.RemoveAt(index);
                DisposePlayer(player);
            }
        }
    }

    private void RemoveSoundEffect(MediaPlayer player)
    {
        for (var index = soundEffects.Count - 1; index >= 0; index--)
        {
            if (ReferenceEquals(soundEffects[index].Player, player))
            {
                soundEffects.RemoveAt(index);
                DisposePlayer(player);
                return;
            }
        }
    }

    private static void DisposePlayer(MediaPlayer? player)
    {
        if (player is null)
        {
            return;
        }

        player.Pause();
        player.Source = null;
        player.Dispose();
    }
}
