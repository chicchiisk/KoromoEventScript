using KoromoEventScript.Runtime.Core.Execution;
using KoromoEventScript.Runtime.Core.Persistence;
using KoromoEventScript.Runtime.Windows.Persistence;

namespace KoromoEventScript.Runtime.Windows.Tests.Persistence;

public sealed class WindowsSaveStoreTests
{
    private string tempRoot = string.Empty;

    [SetUp]
    public void SetUp()
    {
        tempRoot = Path.Combine(Path.GetTempPath(), "kes-runtime-tests", Guid.NewGuid().ToString("N"));
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
    public async Task SaveAsync_WritesSaveUnderUserDataRootSeparatedByGameId()
    {
        var locator = new WindowsUserDataLocator(tempRoot, "Koromosoft", "Sample Game");
        var store = new WindowsSaveStore(locator);
        var envelope = CreateEnvelope("Chapter 1");

        await store.SaveAsync(new SaveSlot(1), envelope);

        var savePath = locator.GetSavePath(new SaveSlot(1));
        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(savePath), Is.True);
            Assert.That(savePath, Does.StartWith(Path.Combine(tempRoot, "Koromosoft", "Sample_Game")));
        });
    }

    [Test]
    public async Task SaveAsync_DoesNotWriteToDistributionDirectory()
    {
        var distributionRoot = Path.Combine(tempRoot, "dist");
        var userRoot = Path.Combine(tempRoot, "user");
        Directory.CreateDirectory(distributionRoot);
        var locator = new WindowsUserDataLocator(userRoot, "Koromosoft", "Readonly Game");
        var store = new WindowsSaveStore(locator);

        await store.SaveAsync(new SaveSlot(2), CreateEnvelope("Readonly layout"));

        Assert.Multiple(() =>
        {
            Assert.That(Directory.EnumerateFileSystemEntries(distributionRoot), Is.Empty);
            Assert.That(File.Exists(locator.GetSavePath(new SaveSlot(2))), Is.True);
        });
    }

    [Test]
    public void UserDataLocator_WithDifferentGameIds_SeparatesSaveAndSettingsPaths()
    {
        var first = new WindowsUserDataLocator(tempRoot, "Koromosoft", "First Game");
        var second = new WindowsUserDataLocator(tempRoot, "Koromosoft", "Second Game");

        Assert.Multiple(() =>
        {
            Assert.That(first.GameDataRoot, Is.Not.EqualTo(second.GameDataRoot));
            Assert.That(first.GetSavePath(new SaveSlot(1)), Is.Not.EqualTo(second.GetSavePath(new SaveSlot(1))));
            Assert.That(first.SettingsPath, Is.Not.EqualTo(second.SettingsPath));
            Assert.That(first.GetSavePath(new SaveSlot(1)), Does.StartWith(Path.Combine(tempRoot, "Koromosoft", "First_Game")));
            Assert.That(second.GetSavePath(new SaveSlot(1)), Does.StartWith(Path.Combine(tempRoot, "Koromosoft", "Second_Game")));
        });
    }

    [Test]
    public async Task LoadAsync_ReadsPreviouslySavedEnvelope()
    {
        var locator = new WindowsUserDataLocator(tempRoot, "Koromosoft", "Load Game");
        var store = new WindowsSaveStore(locator);
        await store.SaveAsync(new SaveSlot(3), CreateEnvelope("Resume here"));

        var loaded = await store.LoadAsync(new SaveSlot(3));

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Title, Is.EqualTo("Resume here"));
            Assert.That(loaded.Snapshot.Position.ScriptId, Is.EqualTo("chapter001"));
            Assert.That(loaded.Snapshot.Position.InstructionIndex, Is.EqualTo(12));
        });
    }

    [Test]
    public async Task SaveSettingsAsync_WritesSettingsUnderSameGameDataRoot()
    {
        var locator = new WindowsUserDataLocator(tempRoot, "Koromosoft", "Settings Game");
        var store = new WindowsUserSettingsStore(locator);
        var settings = new WindowsRuntimeUserSettings(
            MasterVolume: 0.8d,
            BgmVolume: 0.7d,
            SeVolume: 0.6d,
            VoiceVolume: 0.5d,
            TextSpeed: 42,
            AutoSpeed: 2.5d,
            SkipMode: "read",
            Fullscreen: true,
            WindowWidth: 1600,
            WindowHeight: 900,
            Locale: "ja-JP");

        await store.SaveAsync(settings);
        var loaded = await store.LoadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(locator.SettingsPath), Is.True);
            Assert.That(loaded, Is.EqualTo(settings));
        });
    }

    private static WindowsSaveEnvelope CreateEnvelope(string title)
    {
        return new WindowsSaveEnvelope(
            SchemaVersion: 1,
            Title: title,
            SavedAt: new DateTimeOffset(2026, 6, 22, 12, 0, 0, TimeSpan.Zero),
            Snapshot: new RuntimeSaveSnapshot(
                SchemaVersion: 1,
                new RuntimeExecutionPosition("chapter001", 12, null),
                RuntimeContinuation.Running,
                [RuntimeValue.String("marker")],
                [new RuntimeVariableSnapshot(1, RuntimeValue.Number(10d))]),
            Locale: "ja-JP");
    }
}
