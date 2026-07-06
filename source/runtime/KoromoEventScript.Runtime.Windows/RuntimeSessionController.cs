using KoromoEventScript.Runtime.Core.Diagnostics;
using KoromoEventScript.Runtime.Core.Effects;
using KoromoEventScript.Runtime.Core.Execution;
using KoromoEventScript.Runtime.Core.Klib;
using KoromoEventScript.Runtime.Core.Manifests;
using KoromoEventScript.Runtime.Core.Packages;
using KoromoEventScript.Runtime.Windows.Audio;
using KoromoEventScript.Runtime.Windows.Bootstrap;
using KoromoEventScript.Runtime.Windows.Rendering;
using KoromoEventScript.Runtime.Windows.ViewModels;

namespace KoromoEventScript.Runtime.Windows;

public sealed class RuntimeSessionController
{
    private readonly WindowsRuntimeOptions options;
    private readonly MainPageViewModel viewModel;
    private readonly IRuntimeManifestReader manifestReader;
    private readonly IKlibModuleLoader klibModuleLoader;
    private readonly IAudioPlaybackBackend audioBackend;
    private readonly KesVmExecutor executor;
    private readonly RuntimeGameParameterStore gameParameters = new();
    private readonly RuntimeSceneStateController sceneStateController = new();
    private readonly RuntimeTriggerEvaluator triggerEvaluator;
    private readonly ViewModelEffectSink effectSink;
    private AudioChannelService? audioService;
    private KesVmSession? session;
    private RuntimeManifestDocument? manifest;
    private RuntimeEventEntry? currentEvent;
    private RuntimeScriptEntry? currentScript;

    public RuntimeSessionController(
        WindowsRuntimeOptions options,
        MainPageViewModel viewModel,
        IRuntimeManifestReader? manifestReader = null,
        IKlibModuleLoader? klibModuleLoader = null,
        IAudioPlaybackBackend? audioBackend = null)
    {
        this.options = options;
        this.viewModel = viewModel;
        this.manifestReader = manifestReader ?? new RuntimeManifestReader();
        this.klibModuleLoader = klibModuleLoader ?? new KlibModuleLoader();
        this.audioBackend = audioBackend ?? new MediaPlayerAudioPlaybackBackend();
        effectSink = new ViewModelEffectSink(viewModel, sceneStateController, () => audioService);
        triggerEvaluator = new RuntimeTriggerEvaluator(gameParameters);
        executor = new KesVmExecutor(effectSink: effectSink, gameParameters: gameParameters);
    }

    public void Initialize()
    {
        viewModel.HideChoices();
        var manifestResult = manifestReader.Read(options.ManifestPath);
        if (!manifestResult.Succeeded)
        {
            PresentFailure(manifestResult.Diagnostics);
            return;
        }

        manifest = manifestResult.Document!;
        viewModel.SetAssets(manifest.Assets);
        audioService = new AudioChannelService(RuntimeResourceCatalog.Create(options.Locale ?? manifest.DefaultLocale, manifest.Assets), audioBackend);
        var script = ResolveStartScript(manifest, options, out currentEvent);
        if (script is null)
        {
            PresentFailure([RuntimeDiagnostic.Error("KESR1003", "Runtime manifest does not contain a matching start script.", RuntimeFailureKind.Startup)]);
            return;
        }

        if (!StartScript(script, options.Start))
        {
            return;
        }
    }

    private bool StartScript(RuntimeScriptEntry script, string? start)
    {
        currentScript = script;
        var loadResult = klibModuleLoader.Load(script.ResolvedKlibPath);
        if (!loadResult.Succeeded)
        {
            PresentFailure(loadResult.Diagnostics);
            return false;
        }

        var document = loadResult.Document!;
        if (!StringComparer.Ordinal.Equals(document.Module.ScriptId, script.ScriptId))
        {
            PresentFailure([RuntimeDiagnostic.Error("KESR2102", $"Klib script id '{document.Module.ScriptId}' does not match manifest script id '{script.ScriptId}'.", RuntimeFailureKind.Startup)]);
            return false;
        }

        session = new KesVmSession(document);
        var startInstructionIndex = ResolveStartInstructionIndex(document, script, start);
        if (startInstructionIndex is null)
        {
            PresentFailure([RuntimeDiagnostic.Error("KESR2102", $"Start label could not be resolved for script '{script.ScriptId}'.", RuntimeFailureKind.Startup)]);
            return false;
        }

        session.SetInstructionIndex(startInstructionIndex.Value);
        RunSession();
        return true;
    }

    public void Advance()
    {
        if (session is null || session.Continuation.Kind != RuntimeContinuationKind.WaitingForAdvance)
        {
            return;
        }

        var result = executor.ContinueAdvance(session);
        if (!result.Succeeded)
        {
            PresentFailure(result.Diagnostics);
            return;
        }

        RunSession();
    }

    public void ChooseSelection(int index)
    {
        if (session is null || session.Continuation.Kind != RuntimeContinuationKind.WaitingForSelection)
        {
            return;
        }

        var result = executor.ChooseSelection(session, index);
        if (!result.Succeeded)
        {
            PresentFailure(result.Diagnostics);
            return;
        }

        viewModel.HideChoices();
        RunSession();
    }

    private void RunSession()
    {
        if (session is null)
        {
            return;
        }

        var result = executor.Run(session);
        if (!result.Succeeded)
        {
            PresentFailure(result.Diagnostics);
            return;
        }

        if (session.Continuation.Kind == RuntimeContinuationKind.WaitingForSelection)
        {
            if (!string.IsNullOrWhiteSpace(session.Continuation.Prompt))
            {
                viewModel.ShowDialogue(string.Empty, session.Continuation.Prompt!);
            }

            viewModel.ShowChoices(session.Continuation.PendingChoices.Select(static choice => choice.Text));
            return;
        }

        if (session.Continuation.Kind == RuntimeContinuationKind.Completed &&
            TryStartNextEvent())
        {
            return;
        }

        viewModel.HideChoices();
    }

    private bool TryStartNextEvent()
    {
        if (manifest is null || currentEvent is null)
        {
            return false;
        }

        var completedEventId = currentEvent.EventId;
        var nextEvent = manifest.Events.FirstOrDefault(entry => triggerEvaluator.IsMatch(entry.Trigger, completedEventId));
        if (nextEvent is null)
        {
            return false;
        }

        var nextScript = ResolveScriptById(manifest, options, nextEvent.ScriptId);
        if (nextScript is null)
        {
            PresentFailure([RuntimeDiagnostic.Error("KESR1003", $"Runtime manifest does not contain script '{nextEvent.ScriptId}' for event '{nextEvent.EventId}'.", RuntimeFailureKind.Startup)]);
            return true;
        }

        currentEvent = nextEvent;
        StartScript(nextScript, start: null);
        return true;
    }

    private void PresentFailure(IReadOnlyList<RuntimeDiagnostic> diagnostics)
    {
        var primary = diagnostics.FirstOrDefault();
        var message = primary is null
            ? "The game could not be started."
            : $"{primary.Code}: {primary.Message}";
        viewModel.ShowRuntimeError(message);
    }

    private static RuntimeScriptEntry? ResolveStartScript(RuntimeManifestDocument manifest, WindowsRuntimeOptions options, out RuntimeEventEntry? startEvent)
    {
        startEvent = null;
        var requestedScriptKey = TryGetRequestedScriptKey(options.Start);
        var locale = options.Locale ?? manifest.DefaultLocale;
        var localeScripts = manifest.Scripts.Where(script => StringComparer.OrdinalIgnoreCase.Equals(script.Locale, locale)).ToArray();
        var candidates = localeScripts.Length > 0 ? localeScripts : manifest.Scripts.ToArray();

        if (!string.IsNullOrWhiteSpace(requestedScriptKey))
        {
            return candidates.FirstOrDefault(script => ScriptMatches(script, requestedScriptKey!));
        }

        if (manifest.Events.Count > 0)
        {
            startEvent = manifest.Events.FirstOrDefault(static entry => entry.IsEntry) ?? manifest.Events.FirstOrDefault();
            if (startEvent is not null)
            {
                var scriptId = startEvent.ScriptId;
                return candidates.FirstOrDefault(script => ScriptMatches(script, scriptId));
            }
        }

        return candidates.FirstOrDefault(static script => script.IsEntry) ?? candidates.FirstOrDefault();
    }

    private static RuntimeScriptEntry? ResolveScriptById(RuntimeManifestDocument manifest, WindowsRuntimeOptions options, string scriptId)
    {
        var locale = options.Locale ?? manifest.DefaultLocale;
        var localeScripts = manifest.Scripts.Where(script => StringComparer.OrdinalIgnoreCase.Equals(script.Locale, locale)).ToArray();
        var candidates = localeScripts.Length > 0 ? localeScripts : manifest.Scripts.ToArray();
        return candidates.FirstOrDefault(script => ScriptMatches(script, scriptId));
    }

    private static string? TryGetRequestedScriptKey(string? start)
    {
        if (string.IsNullOrWhiteSpace(start))
        {
            return null;
        }

        var separatorIndex = start.IndexOf(':');
        if (separatorIndex <= 0)
        {
            return null;
        }

        return start[..separatorIndex];
    }

    private static bool ScriptMatches(RuntimeScriptEntry script, string requestedScriptKey)
    {
        return StringComparer.OrdinalIgnoreCase.Equals(script.ScriptId, requestedScriptKey)
            || StringComparer.OrdinalIgnoreCase.Equals(Path.GetFileName(script.ScriptId), requestedScriptKey)
            || StringComparer.OrdinalIgnoreCase.Equals(Path.GetFileNameWithoutExtension(script.ScriptId), requestedScriptKey)
            || script.ScriptId.EndsWith($"/{requestedScriptKey}", StringComparison.OrdinalIgnoreCase)
            || script.ScriptId.EndsWith($"\\{requestedScriptKey}", StringComparison.OrdinalIgnoreCase);
    }

    private static int? ResolveStartInstructionIndex(KlibDocument document, RuntimeScriptEntry script, string? start)
    {
        if (string.IsNullOrWhiteSpace(start))
        {
            return document.Instructions.FirstOrDefault()?.Index;
        }

        var labelName = ResolveStartLabelName(document, script, start);
        if (labelName is null)
        {
            return document.Instructions.FirstOrDefault()?.Index;
        }

        var label = document.Labels.FirstOrDefault(candidate => LabelMatches(document, candidate, labelName));
        if (label is null)
        {
            return null;
        }

        return document.Instructions.FirstOrDefault(instruction => instruction.Offset == label.Offset)?.Index;
    }

    private static string? ResolveStartLabelName(KlibDocument document, RuntimeScriptEntry script, string? start)
    {
        var requested = start;
        if (!string.IsNullOrWhiteSpace(requested))
        {
            var separatorIndex = requested.IndexOf(':');
            if (separatorIndex >= 0 && separatorIndex + 1 < requested.Length)
            {
                requested = requested[(separatorIndex + 1)..];
            }

            if (!string.IsNullOrWhiteSpace(requested))
            {
                var exact = document.Labels.FirstOrDefault(candidate => LabelMatches(document, candidate, requested));
                if (exact is not null)
                {
                    return GetLabelName(document, exact);
                }

                var hashed = requested.StartsWith('#') ? requested : $"#{requested}";
                var suffixMatch = document.Labels.FirstOrDefault(candidate => LabelEndsWith(document, candidate, requested) || LabelEndsWith(document, candidate, hashed));
                if (suffixMatch is not null)
                {
                    return GetLabelName(document, suffixMatch);
                }
            }
        }
        return null;
    }

    private static bool LabelMatches(KlibDocument document, KlibLabel label, string expected)
    {
        return StringComparer.OrdinalIgnoreCase.Equals(GetLabelName(document, label), expected);
    }

    private static bool LabelEndsWith(KlibDocument document, KlibLabel label, string suffix)
    {
        return GetLabelName(document, label).EndsWith(suffix, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetLabelName(KlibDocument document, KlibLabel label)
    {
        return label.NameIndex >= 0 && label.NameIndex < document.Constants.Count
            ? document.Constants[label.NameIndex].StringValue ?? string.Empty
            : string.Empty;
    }

    private sealed class ViewModelEffectSink(
        MainPageViewModel viewModel,
        RuntimeSceneStateController sceneStateController,
        Func<AudioChannelService?> getAudioService) : IRuntimeEffectSink
    {
        public void Publish(RuntimeEffectBatch batch)
        {
            foreach (var effect in batch.Effects)
            {
                if (effect.Kind == RuntimeEffectKind.Scene)
                {
                    sceneStateController.Apply(effect);
                    viewModel.ApplySceneState(sceneStateController.State);
                    continue;
                }

                if (effect.Kind == RuntimeEffectKind.Audio)
                {
                    ApplyAudio(effect);
                    continue;
                }

                switch (effect.Name)
                {
                    case "scenario.say":
                        viewModel.ShowDialogue(NormalizeSpeaker(effect.Payload.TryGetValue("actor", out var actor) ? actor : null), effect.Payload.TryGetValue("text", out var sayText) ? sayText ?? string.Empty : string.Empty);
                        break;

                    case "scenario.nar":
                        viewModel.ShowDialogue(string.Empty, effect.Payload.TryGetValue("text", out var narration) ? narration ?? string.Empty : string.Empty);
                        break;
                }
            }
        }

        private void ApplyAudio(RuntimeEffect effect)
        {
            var audioService = getAudioService();
            if (audioService is null)
            {
                return;
            }

            var result = audioService.ApplyAsync(effect).GetAwaiter().GetResult();
            if (result.Succeeded)
            {
                return;
            }

            var primary = result.Diagnostics.FirstOrDefault();
            viewModel.ShowRuntimeError(primary is null ? "Audio playback failed." : $"{primary.Code}: {primary.Message}");
        }

        private static string NormalizeSpeaker(string? actor)
        {
            if (string.IsNullOrWhiteSpace(actor))
            {
                return string.Empty;
            }

            var normalized = actor.Replace('\\', '/');
            var slashIndex = normalized.LastIndexOf('/');
            if (slashIndex >= 0)
            {
                normalized = normalized[(slashIndex + 1)..];
            }

            var dotIndex = normalized.LastIndexOf('.');
            return dotIndex >= 0 && dotIndex + 1 < normalized.Length
                ? normalized[(dotIndex + 1)..]
                : normalized;
        }
    }
}
