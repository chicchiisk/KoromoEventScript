using KoromoEventScript.Runtime.Core.Stl;

namespace KoromoEventScript.Runtime.Core.Tests.Stl;

public sealed class StlSyscallCoverageTests
{
    private static readonly string[] ExpectedSyscallsFromStlSpec =
    [
        "core.print",
        "core.array_len",
        "core.str_len",
        "core.bool_to_string",
        "core.number_to_string",
        "core.range",
        "core.assert",
        "scene.rt_back",
        "scene.rt_front",
        "scene.bg",
        "scene.camera_autofocus",
        "actor.show",
        "actor.face",
        "actor.move",
        "scene.trans",
        "actor.action_jump",
        "actor.cast",
        "actor.hide",
        "scenario.say",
        "scenario.nar",
        "text.p",
        "text.l",
        "text.wait_click",
        "text.vo",
        "text.r",
        "text.cm",
        "audio.vo_auto",
        "audio.bgm",
        "audio.bgm_stop",
        "audio.se",
        "audio.se_stop_all",
        "audio.se_stop",
        "audio.voice_stop",
        "state.load",
        "state.autosave",
        "state.mark_read",
        "state.is_read",
        "state.save",
        "localize.get",
        "system.wait",
        "system.set_auto",
        "system.set_skip",
        "system.set_config_string",
        "system.set_config_number",
        "system.set_config_bool",
        "system.get_config",
        "system.set_param_string",
        "system.set_param_number",
        "system.set_param_bool",
        "system.get_param",
    ];

    [Test]
    public void DispatcherRegistersEveryStlSyscallFixture()
    {
        var expected = ExpectedSyscallsFromStlSpec.Order(StringComparer.Ordinal).ToArray();
        var actual = StlSyscallDispatcher.SupportedSyscallIds.Order(StringComparer.Ordinal).ToArray();
        var missing = expected.Except(actual, StringComparer.Ordinal).ToArray();
        var extra = actual.Except(expected, StringComparer.Ordinal).ToArray();
        var malformed = actual.Where(static id => !id.Contains('.', StringComparison.Ordinal)).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(missing, Is.Empty, $"Missing STL syscall registrations: {string.Join(", ", missing)}");
            Assert.That(extra, Is.Empty, $"Unknown STL syscall registrations: {string.Join(", ", extra)}");
            Assert.That(malformed, Is.Empty, $"Malformed STL syscall IDs: {string.Join(", ", malformed)}");
        });
    }
}
