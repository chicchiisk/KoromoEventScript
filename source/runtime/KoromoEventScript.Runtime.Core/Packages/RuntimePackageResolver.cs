using KoromoEventScript.Runtime.Core.Diagnostics;
using KoromoEventScript.Runtime.Core.Klib;
using KoromoEventScript.Runtime.Core.Manifests;

namespace KoromoEventScript.Runtime.Core.Packages;

public interface IRuntimePackageResolver
{
    RuntimePackageResolveResult Resolve(RuntimeManifestDocument manifest);
}

public sealed class RuntimePackageResolver : IRuntimePackageResolver
{
    private readonly IKlibModuleLoader klibLoader;

    public RuntimePackageResolver(IKlibModuleLoader klibLoader)
    {
        this.klibLoader = klibLoader;
    }

    public RuntimePackageResolveResult Resolve(RuntimeManifestDocument manifest)
    {
        var modules = new List<RuntimeScriptModule>();

        foreach (var script in manifest.Scripts)
        {
            if (!IsKlibPath(script.ResolvedKlibPath))
            {
                return RuntimePackageResolveResult.Failure(
                    RuntimeFailureKind.Startup,
                    RuntimeDiagnostic.Error(
                        "KESR2003",
                        $"Runtime script input must be a .klib file: {script.KlibPath}",
                        RuntimeFailureKind.Startup));
            }

            if (!File.Exists(script.ResolvedKlibPath))
            {
                return RuntimePackageResolveResult.Failure(
                    RuntimeFailureKind.Io,
                    RuntimeDiagnostic.Error(
                        "KESR2001",
                        $"Required klib file was not found: {script.ResolvedKlibPath}",
                        RuntimeFailureKind.Io));
            }

            var loadResult = klibLoader.Load(script.ResolvedKlibPath);
            if (!loadResult.Succeeded)
            {
                return RuntimePackageResolveResult.Failure(loadResult.FailureKind, loadResult.Diagnostics);
            }

            var document = loadResult.Document!;
            if (!StringComparer.Ordinal.Equals(document.Module.ScriptId, script.ScriptId))
            {
                return RuntimePackageResolveResult.Failure(
                    RuntimeFailureKind.Startup,
                    RuntimeDiagnostic.Error(
                        "KESR2002",
                        $"Manifest script id '{script.ScriptId}' does not match klib script id '{document.Module.ScriptId}'.",
                        RuntimeFailureKind.Startup));
            }

            modules.Add(new RuntimeScriptModule(script, document));
        }

        return RuntimePackageResolveResult.Success(new RuntimePackage(manifest, modules));
    }

    private static bool IsKlibPath(string path)
    {
        return StringComparer.OrdinalIgnoreCase.Equals(Path.GetExtension(path), ".klib");
    }
}

public sealed record RuntimePackage(
    RuntimeManifestDocument Manifest,
    IReadOnlyList<RuntimeScriptModule> Scripts);

public sealed record RuntimeScriptModule(
    RuntimeScriptEntry Entry,
    KlibDocument Document);

public sealed record RuntimePackageResolveResult(
    RuntimePackage? Package,
    IReadOnlyList<RuntimeDiagnostic> Diagnostics,
    RuntimeFailureKind FailureKind)
{
    public bool Succeeded => Package is not null && FailureKind == RuntimeFailureKind.None;

    public static RuntimePackageResolveResult Success(RuntimePackage package)
    {
        return new RuntimePackageResolveResult(package, [], RuntimeFailureKind.None);
    }

    public static RuntimePackageResolveResult Failure(RuntimeFailureKind failureKind, params RuntimeDiagnostic[] diagnostics)
    {
        return new RuntimePackageResolveResult(null, diagnostics, failureKind);
    }

    public static RuntimePackageResolveResult Failure(RuntimeFailureKind failureKind, IReadOnlyList<RuntimeDiagnostic> diagnostics)
    {
        return new RuntimePackageResolveResult(null, diagnostics, failureKind);
    }
}
