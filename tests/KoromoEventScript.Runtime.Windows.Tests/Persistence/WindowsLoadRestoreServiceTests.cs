using KoromoEventScript.Runtime.Core.Execution;
using KoromoEventScript.Runtime.Core.Klib;
using KoromoEventScript.Runtime.Core.Persistence;
using KoromoEventScript.Runtime.Windows.Persistence;

namespace KoromoEventScript.Runtime.Windows.Tests.Persistence;

public sealed class WindowsLoadRestoreServiceTests
{
    private string tempRoot = string.Empty;

    [SetUp]
    public void SetUp()
    {
        tempRoot = Path.Combine(Path.GetTempPath(), "kes-runtime-load-tests", Guid.NewGuid().ToString("N"));
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(tempRoot))
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Test]
    public async Task LoadAsync_WithValidSave_RestoresVmAndReturnsHostState()
    {
        var store = CreateStore();
        await store.SaveAsync(new SaveSlot(1), CreateEnvelope("chapter001", 12));
        var session = new KesVmSession(CreateDocument("chapter001", 0, 12, 20));
        var notifications = new RecordingNotificationSink();
        var service = new WindowsLoadRestoreService(store, notifications);

        var result = await service.LoadAsync(new SaveSlot(1), session);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(session.Position.InstructionIndex, Is.EqualTo(12));
            Assert.That(session.Variables[1].NumberValue, Is.EqualTo(10d));
            Assert.That(result.Envelope?.HostState?.Ui.MessageText, Is.EqualTo("Saved message"));
            Assert.That(result.Envelope?.HostState?.Ui.SelectedChoiceIndex, Is.EqualTo(1));
            Assert.That(result.Envelope?.HostState?.Audio.BgmAssetId, Is.EqualTo("bgm.daily"));
            Assert.That(result.Envelope?.HostState?.Locale, Is.EqualTo("ja-JP"));
            Assert.That(notifications.Notifications, Is.Empty);
        });
    }

    [Test]
    public async Task LoadAsync_WithInvalidInstructionIndex_NotifiesPlayerAndKeepsCurrentSession()
    {
        var store = CreateStore();
        await store.SaveAsync(new SaveSlot(2), CreateEnvelope("chapter001", 999));
        var session = new KesVmSession(CreateDocument("chapter001", 0, 12, 20));
        var notifications = new RecordingNotificationSink();
        var service = new WindowsLoadRestoreService(store, notifications);

        var result = await service.LoadAsync(new SaveSlot(2), session);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(session.Position.InstructionIndex, Is.EqualTo(0));
            Assert.That(result.Diagnostics.Single().Code, Is.EqualTo("KESR3002"));
            Assert.That(notifications.Notifications.Single().Code, Is.EqualTo("KESR3002"));
            Assert.That(notifications.Notifications.Single().Message, Does.Contain("could not be loaded"));
        });
    }

    [Test]
    public async Task LoadAsync_WithDifferentScriptId_NotifiesPlayerAndKeepsCurrentSession()
    {
        var store = CreateStore();
        await store.SaveAsync(new SaveSlot(3), CreateEnvelope("chapter999", 12));
        var session = new KesVmSession(CreateDocument("chapter001", 0, 12, 20));
        var notifications = new RecordingNotificationSink();
        var service = new WindowsLoadRestoreService(store, notifications);

        var result = await service.LoadAsync(new SaveSlot(3), session);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(session.Position.ScriptId, Is.EqualTo("chapter001"));
            Assert.That(result.Diagnostics.Single().Code, Is.EqualTo("KESR3001"));
            Assert.That(notifications.Notifications.Single().Severity, Is.EqualTo(PlayerNotificationSeverity.Error));
        });
    }

    private WindowsSaveStore CreateStore()
    {
        var locator = new WindowsUserDataLocator(tempRoot, "Koromosoft", "Load Restore Game");
        return new WindowsSaveStore(locator);
    }

    private static WindowsSaveEnvelope CreateEnvelope(string scriptId, int instructionIndex)
    {
        return new WindowsSaveEnvelope(
            SchemaVersion: 1,
            Title: "Restore",
            SavedAt: new DateTimeOffset(2026, 6, 22, 13, 0, 0, TimeSpan.Zero),
            Snapshot: new RuntimeSaveSnapshot(
                SchemaVersion: 1,
                new RuntimeExecutionPosition(scriptId, instructionIndex, null),
                RuntimeContinuation.Running,
                [RuntimeValue.String("stack")],
                [new RuntimeVariableSnapshot(1, RuntimeValue.Number(10d))]),
            Locale: "ja-JP",
            HostState: new WindowsHostSaveState(
                new WindowsUiSaveState(
                    MessageText: "Saved message",
                    SpeakerName: "Noa",
                    Choices: ["A", "B"],
                    SelectedChoiceIndex: 1),
                new WindowsAudioSaveState(
                    BgmAssetId: "bgm.daily",
                    VoiceAssetId: "voice.noa.001"),
                Locale: "ja-JP"));
    }

    private static KlibDocument CreateDocument(string scriptId, params int[] instructionIndexes)
    {
        return new KlibDocument(
            new KlibVersion(1, 0, 0),
            new KlibModuleInfo(scriptId, scriptId, $"{scriptId}.kc", null),
            [],
            [],
            [],
            instructionIndexes
                .Select((index, offset) => new KlibInstruction(index, offset, KlibOpCode.PushNull, [], null, KlibMappingKind.Synthetic))
                .ToArray(),
            [],
            new KlibDebugInfo(null, null, []));
    }

    private sealed class RecordingNotificationSink : IPlayerNotificationSink
    {
        private readonly List<PlayerNotification> notifications = [];

        public IReadOnlyList<PlayerNotification> Notifications => notifications;

        public void Notify(PlayerNotification notification)
        {
            notifications.Add(notification);
        }
    }
}
