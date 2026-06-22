using KoromoEventScript.Runtime.Core.Diagnostics;
using KoromoEventScript.Runtime.Core.Klib;
using KoromoEventScript.Runtime.Core.Manifests;

namespace KoromoEventScript.Runtime.Core.Packages;

public interface IRuntimePackageResolver
{
    RuntimePackageResolveResult Resolve(RuntimeManifestDocument manifest);

    RuntimePackageResolveResult Resolve(RuntimeManifestDocument manifest, RuntimePackageResolveOptions options);
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
        return Resolve(manifest, new RuntimePackageResolveOptions());
    }

    public RuntimePackageResolveResult Resolve(RuntimeManifestDocument manifest, RuntimePackageResolveOptions options)
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

        foreach (var asset in manifest.Assets)
        {
            if (!File.Exists(asset.ResolvedPath))
            {
                return RuntimePackageResolveResult.Failure(
                    RuntimeFailureKind.Io,
                    RuntimeDiagnostic.Error(
                        "KESR2004",
                        $"Required asset file was not found: {asset.ResolvedPath}",
                        RuntimeFailureKind.Io));
            }
        }

        var selectedLocale = SelectLocale(manifest, options.Locale);
        var selectedScripts = modules
            .Where(module => IsLocale(module.Entry.Locale, selectedLocale))
            .ToArray();
        if (selectedScripts.Length == 0)
        {
            return RuntimePackageResolveResult.Failure(
                RuntimeFailureKind.Startup,
                RuntimeDiagnostic.Error(
                    "KESR2005",
                    $"Manifest does not contain scripts for locale '{selectedLocale}'.",
                    RuntimeFailureKind.Startup));
        }

        var resources = RuntimeResourceCatalog.Create(selectedLocale, manifest.Assets);
        return RuntimePackageResolveResult.Success(new RuntimePackage(manifest, selectedLocale, selectedScripts, resources));
    }

    private static bool IsKlibPath(string path)
    {
        return StringComparer.OrdinalIgnoreCase.Equals(Path.GetExtension(path), ".klib");
    }

    private static string SelectLocale(RuntimeManifestDocument manifest, string? requestedLocale)
    {
        if (!string.IsNullOrWhiteSpace(requestedLocale) && HasLocale(manifest, requestedLocale))
        {
            return requestedLocale;
        }

        return manifest.DefaultLocale;
    }

    private static bool HasLocale(RuntimeManifestDocument manifest, string locale)
    {
        return manifest.Scripts.Any(script => IsLocale(script.Locale, locale));
    }

    private static bool IsLocale(string candidate, string locale)
    {
        return StringComparer.OrdinalIgnoreCase.Equals(candidate, locale);
    }
}

public sealed record RuntimePackageResolveOptions(string? Locale = null);

public sealed record RuntimePackage(
    RuntimeManifestDocument Manifest,
    string SelectedLocale,
    IReadOnlyList<RuntimeScriptModule> Scripts,
    RuntimeResourceCatalog Resources);

public sealed record RuntimeScriptModule(
    RuntimeScriptEntry Entry,
    KlibDocument Document);

public sealed record RuntimeResourceCatalog(
    string SelectedLocale,
    IReadOnlyList<RuntimeAssetEntry> Assets,
    IReadOnlyDictionary<string, RuntimeAssetEntry> AssetsById)
{
    public static RuntimeResourceCatalog Create(string selectedLocale, IReadOnlyList<RuntimeAssetEntry> assets)
    {
        var selectedAssets = assets
            .GroupBy(static asset => asset.AssetId, StringComparer.Ordinal)
            .Select(group => SelectAssetVariant(group, selectedLocale))
            .ToArray();
        var assetsById = selectedAssets.ToDictionary(static asset => asset.AssetId, StringComparer.Ordinal);

        return new RuntimeResourceCatalog(selectedLocale, selectedAssets, assetsById);
    }

    public RuntimeAssetEntry? ResolveAsset(string assetId)
    {
        return AssetsById.GetValueOrDefault(assetId);
    }

    private static RuntimeAssetEntry SelectAssetVariant(IEnumerable<RuntimeAssetEntry> variants, string selectedLocale)
    {
        RuntimeAssetEntry? neutral = null;
        RuntimeAssetEntry? first = null;

        foreach (var variant in variants)
        {
            first ??= variant;

            if (variant.Locale is null)
            {
                neutral ??= variant;
                continue;
            }

            if (StringComparer.OrdinalIgnoreCase.Equals(variant.Locale, selectedLocale))
            {
                return variant;
            }
        }

        return neutral ?? first!;
    }
}

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
