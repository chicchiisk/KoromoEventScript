using KoromoEventScript.Cli.Commands;
using KoromoEventScript.Cli.Execution;
using KoromoEventScript.Runtime.Core.Klib;

namespace KoromoEventScript.Cli.Tests.Execution;

public sealed class FullCommandSampleExecutionTests
{
    [TestCase("events/chapter001.klib", 0, new[] { "全命令サンプルの一章目。ここから二つの導入へ分岐する。", "中庭へ向かおう。" })]
    [TestCase("events/chapter001.klib", 1, new[] { "全命令サンプルの一章目。ここから二つの導入へ分岐する。", "図書室で手がかりを探そう。" })]
    [TestCase("events/chapter002.klib", 0, new[] { "二章目の中庭ルート。ここから結末候補を選ぶ。", "夕焼けのほうへ向かおう。" })]
    [TestCase("events/chapter002.klib", 1, new[] { "二章目の中庭ルート。ここから結末候補を選ぶ。", "星空を見に行こう。" })]
    [TestCase("events/chapter003.klib", 0, new[] { "三章目の図書室ルート。こちらも二つの結末候補へ分岐する。", "夕焼けの記録を確かめよう。", "hogehoge" })]
    [TestCase("events/chapter003.klib", 1, new[] { "三章目の図書室ルート。こちらも二つの結末候補へ分岐する。", "星空の手がかりを追おう。" })]
    [TestCase("events/chapter004.klib", null, new[] { "四章目。夕焼けの答えを得たので、もう一度一章へ戻る。", "イベント遷移は trigger によって次の一章を選ぶ。" })]
    [TestCase("events/chapter005.klib", null, new[] { "五章目。星空の答えを得たので、もう一度一章へ戻る。", "こちらの経路でも同じ trigger でループできる。" })]
    [TestCase("events/lib/Common.klib", null, new[] { "共通ロジックの初期化が完了した。" })]
    public void BuiltFullCommandSampleKlib_ReachesEnd(string relativeKlibPath, int? selectedIndex, string[] expectedTranscript)
    {
        using var fixture = TemporaryProject.Create();
        using var output = new StringWriter();
        using var error = new StringWriter();
        CopyProject(GetTestDataPath("projects", "full-command-sample"), fixture.Root);

        var exitCode = new CliApplication().Run(
            ["build", fixture.Root, "--txt-il"],
            output,
            error,
            TestContext.CurrentContext.WorkDirectory);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo((int)CliExitCode.Success));
            Assert.That(output.ToString(), Is.Empty);
            Assert.That(error.ToString(), Is.Empty);
        });

        var document = LoadKlib(Path.Combine(fixture.Root, "build", "windows", relativeKlibPath));
        var session = new HeadlessVmSession();

        session.Start(document);
        RunToEnd(session, selectedIndex);

        Assert.Multiple(() =>
        {
            Assert.That(session.State.Kind, Is.EqualTo(HeadlessVmStateKind.Completed), DescribeState(session));
            Assert.That(session.State.StopReason, Is.EqualTo(HeadlessVmStopReason.Completed));
            Assert.That(session.Observation.Transcript.Select(static entry => entry.Text), Is.EqualTo(expectedTranscript));
        });
    }

    private static void RunToEnd(HeadlessVmSession session, int? selectedIndex)
    {
        for (var step = 0; step < 64; step++)
        {
            switch (session.State.Kind)
            {
                case HeadlessVmStateKind.Completed:
                    return;
                case HeadlessVmStateKind.WaitingForAdvance:
                    session.ResumeAdvance();
                    break;
                case HeadlessVmStateKind.WaitingForSelection:
                    if (selectedIndex is not int choiceIndex)
                    {
                        Assert.Fail(DescribeState(session));
                        return;
                    }

                    session.ResumeSelection(choiceIndex);
                    break;
                case HeadlessVmStateKind.Faulted:
                    Assert.Fail(DescribeState(session));
                    return;
                default:
                    Assert.Fail($"Unexpected VM state '{session.State.Kind}'.");
                    return;
            }
        }

        Assert.Fail($"The VM did not complete within the step limit. {DescribeState(session)}");
    }

    private static KlibDocument LoadKlib(string path)
    {
        var loadResult = new KlibModuleLoader().Load(path);
        Assert.That(loadResult.Succeeded, Is.True, string.Join(Environment.NewLine, loadResult.Diagnostics.Select(static diagnostic => diagnostic.Message)));
        return loadResult.Document!;
    }

    private static string DescribeState(HeadlessVmSession session)
    {
        var fault = session.State.Fault is null ? string.Empty : $" Fault: {session.State.Fault.Message}";
        var choices = session.State.PendingChoices is null
            ? string.Empty
            : $" Choices: {string.Join(", ", session.State.PendingChoices.Select(static choice => choice.Text))}";

        return $"State: {session.State.Kind}, Script: {session.State.ScriptId}, Offset: {session.State.InstructionOffset}.{fault}{choices}";
    }

    private static string GetTestDataPath(params string[] segments)
    {
        return Path.GetFullPath(Path.Combine(GetRepositoryRoot(), "testdata", Path.Combine(segments)));
    }

    private static void CopyProject(string sourceRoot, string destinationRoot)
    {
        foreach (var directory in Directory.EnumerateDirectories(sourceRoot, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(destinationRoot, Path.GetRelativePath(sourceRoot, directory)));
        }

        foreach (var file in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            var destination = Path.Combine(destinationRoot, Path.GetRelativePath(sourceRoot, file));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, overwrite: true);
        }
    }

    private static string GetRepositoryRoot()
    {
        return Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", ".."));
    }
}
