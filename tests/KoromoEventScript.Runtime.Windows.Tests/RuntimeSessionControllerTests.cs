using KoromoEventScript.Runtime.Windows.Bootstrap;
using KoromoEventScript.Runtime.Windows.Audio;
using KoromoEventScript.Runtime.Windows.Input;
using KoromoEventScript.Runtime.Windows.ViewModels;

namespace KoromoEventScript.Runtime.Windows.Tests;

public sealed class RuntimeSessionControllerTests
{
    [Test]
    public void ShouldAdvancePageTap_AfterChoiceSelection_SuppressesOneBubbledTap()
    {
        var gate = new PageTapInputGate();
        gate.SuppressNextPageTap();

        var first = gate.ShouldAdvance(choicesVisible: false);
        var second = gate.ShouldAdvance(choicesVisible: false);

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.False);
            Assert.That(second, Is.True);
        });
    }

    [Test]
    public void ShouldAdvancePageTap_WhileChoicesVisible_DoesNotAdvance()
    {
        var gate = new PageTapInputGate();

        var result = gate.ShouldAdvance(choicesVisible: true);

        Assert.That(result, Is.False);
    }

    [Test]
    public void Initialize_WithFullCommandSampleManifest_StartsFromScriptHeadBeforeEntryLabel()
    {
        var manifestPath = Path.Combine(
            GetRepositoryRoot(),
            "testdata",
            "projects",
            "full-command-sample",
            "build",
            "windows",
            "manifest.json");
        var viewModel = new MainPageViewModel();
        var controller = new RuntimeSessionController(
            new WindowsRuntimeOptions(
                ManifestPath: manifestPath,
                Locale: null,
                Start: null,
                Fullscreen: false,
                Width: null,
                Height: null,
                Debug: false,
                Profile: false),
            viewModel);

        controller.Initialize();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.SpeakerName, Is.EqualTo("riku"));
            Assert.That(viewModel.MessageText, Is.EqualTo("全命令サンプルの一章目。ここから二つの導入へ分岐する。"));
            Assert.That(viewModel.MessageText, Does.Not.StartWith("KESR3102"));
            Assert.That(viewModel.IsMessageVisible, Is.True);
            Assert.That(viewModel.BacklogEntries, Does.Contain("riku: 全命令サンプルの一章目。ここから二つの導入へ分岐する。"));
        });

        controller.Advance();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.AreChoicesVisible, Is.True);
            Assert.That(viewModel.Choices, Is.EqualTo(["中庭の二章へ進む", "図書室の三章へ進む"]));
        });
    }

    [Test]
    public void Initialize_WithFullCommandSampleManifest_PlaysScriptAudioCommands()
    {
        var manifestPath = Path.Combine(
            GetRepositoryRoot(),
            "testdata",
            "projects",
            "full-command-sample",
            "build",
            "windows",
            "manifest.json");
        var viewModel = new MainPageViewModel();
        var audioBackend = new RecordingAudioBackend();
        var controller = new RuntimeSessionController(
            new WindowsRuntimeOptions(
                ManifestPath: manifestPath,
                Locale: null,
                Start: null,
                Fullscreen: false,
                Width: null,
                Height: null,
                Debug: false,
                Profile: false),
            viewModel,
            audioBackend: audioBackend);

        controller.Initialize();
        controller.Advance();
        controller.ChooseSelection(0);

        Assert.Multiple(() =>
        {
            Assert.That(audioBackend.Plays.Select(static play => play.Channel), Is.EqualTo([AudioChannel.Bgm, AudioChannel.Se]));
            Assert.That(audioBackend.Plays[0].Asset.AssetId, Is.EqualTo("assets.audio.bgm.bgm_001_alice2"));
            Assert.That(audioBackend.Plays[0].Options.Loop, Is.True);
            Assert.That(audioBackend.Plays[1].Asset.AssetId, Is.EqualTo("assets.audio.se.se_001_door"));
        });
    }

    [Test]
    public void ChooseSelection_WithTriggerRoute_CanLoopAcrossEvents()
    {
        var manifestPath = Path.Combine(
            GetRepositoryRoot(),
            "testdata",
            "projects",
            "full-command-sample",
            "build",
            "windows",
            "manifest.json");
        var viewModel = new MainPageViewModel();
        var controller = new RuntimeSessionController(
            new WindowsRuntimeOptions(
                ManifestPath: manifestPath,
                Locale: null,
                Start: null,
                Fullscreen: false,
                Width: null,
                Height: null,
                Debug: false,
                Profile: false),
            viewModel);

        controller.Initialize();
        controller.Advance();
        controller.ChooseSelection(0);

        Assert.That(viewModel.MessageText, Is.EqualTo("中庭へ向かおう。"));

        controller.Advance();
        Assert.That(viewModel.MessageText, Is.EqualTo("二章目の中庭ルート。ここから結末候補を選ぶ。"));

        controller.Advance();
        controller.ChooseSelection(0);

        Assert.That(viewModel.MessageText, Is.EqualTo("夕焼けのほうへ向かおう。"));

        controller.Advance();
        Assert.That(viewModel.MessageText, Is.EqualTo("四章目。夕焼けの答えを得たので、もう一度一章へ戻る。"));

        controller.Advance();
        Assert.That(viewModel.MessageText, Is.EqualTo("イベント遷移は trigger によって次の一章を選ぶ。"));

        controller.Advance();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.SpeakerName, Is.EqualTo("riku"));
            Assert.That(viewModel.MessageText, Is.EqualTo("全命令サンプルの一章目。ここから二つの導入へ分岐する。"));
        });
    }

    private static string GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "KoromoEventScript.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }

    private sealed class RecordingAudioBackend : IAudioPlaybackBackend
    {
        public List<AudioPlaybackRequest> Plays { get; } = [];

        public List<AudioStopRequest> Stops { get; } = [];

        public List<AudioVolumeChange> VolumeChanges { get; } = [];

        public Task PlayAsync(AudioPlaybackRequest request, CancellationToken cancellationToken = default)
        {
            Plays.Add(request);
            return Task.CompletedTask;
        }

        public Task StopAsync(AudioStopRequest request, CancellationToken cancellationToken = default)
        {
            Stops.Add(request);
            return Task.CompletedTask;
        }

        public Task SetVolumeAsync(AudioVolumeChange change, CancellationToken cancellationToken = default)
        {
            VolumeChanges.Add(change);
            return Task.CompletedTask;
        }
    }
}
