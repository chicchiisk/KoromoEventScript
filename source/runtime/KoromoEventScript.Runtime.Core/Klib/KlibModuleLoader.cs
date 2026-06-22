using System.Text;
using KoromoEventScript.Runtime.Core.Diagnostics;

namespace KoromoEventScript.Runtime.Core.Klib;

public interface IKlibModuleLoader
{
    KlibModuleLoadResult Load(string path);
}

public sealed class KlibModuleLoader : IKlibModuleLoader
{
    private const int ModuleInfoSectionType = 0x0001;

    public KlibModuleLoadResult Load(string path)
    {
        if (!File.Exists(path))
        {
            return KlibModuleLoadResult.Failure(
                RuntimeFailureKind.Io,
                RuntimeDiagnostic.Error(
                    "KESR2101",
                    $"Klib file was not found: {path}",
                    RuntimeFailureKind.Io));
        }

        try
        {
            using var stream = File.OpenRead(path);
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: false);

            var magic = reader.ReadBytes(4);
            if (!magic.SequenceEqual(Encoding.ASCII.GetBytes("KLIB")))
            {
                return InvalidKlib(path, "Klib file has an invalid magic header.");
            }

            var version = new KlibVersion(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
            var features = reader.ReadInt32();
            if (features != 0)
            {
                return InvalidKlib(path, $"Klib file uses unsupported feature flags: {features}.");
            }

            var sectionCount = reader.ReadInt32();
            if (sectionCount < 0)
            {
                return InvalidKlib(path, "Klib file has an invalid section count.");
            }

            var moduleSection = default(SectionHeader?);
            for (var i = 0; i < sectionCount; i++)
            {
                var section = new SectionHeader(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
                if (section.Type == ModuleInfoSectionType)
                {
                    moduleSection = section;
                }
            }

            if (moduleSection is null)
            {
                return InvalidKlib(path, "Klib file does not contain a module info section.");
            }

            var sectionHeader = moduleSection.Value;
            if (!IsValidSection(stream.Length, sectionHeader))
            {
                return InvalidKlib(path, "Klib file has an invalid module info section range.");
            }

            stream.Position = sectionHeader.Offset;
            var moduleBytes = reader.ReadBytes(sectionHeader.Size);
            if (moduleBytes.Length != sectionHeader.Size)
            {
                return InvalidKlib(path, "Klib file ended before the module info section could be read.");
            }

            using var moduleStream = new MemoryStream(moduleBytes);
            using var moduleReader = new BinaryReader(moduleStream, Encoding.UTF8, leaveOpen: false);
            var module = ReadModuleInfo(moduleReader);
            var document = new KlibDocument(
                version,
                module,
                [],
                [],
                [],
                [],
                [],
                new KlibDebugInfo(null, null, []));

            return KlibModuleLoadResult.Success(document);
        }
        catch (EndOfStreamException exception)
        {
            return InvalidKlib(path, exception.Message);
        }
        catch (IOException exception)
        {
            return KlibModuleLoadResult.Failure(
                RuntimeFailureKind.Io,
                RuntimeDiagnostic.Error(
                    "KESR2101",
                    $"Klib file could not be read: {path}. {exception.Message}",
                    RuntimeFailureKind.Io));
        }
        catch (InvalidDataException exception)
        {
            return InvalidKlib(path, exception.Message);
        }
    }

    private static KlibModuleInfo ReadModuleInfo(BinaryReader reader)
    {
        var scriptId = ReadString(reader);
        var moduleId = ReadString(reader);
        var sourcePath = ReadString(reader);
        var hasEntry = reader.ReadInt32();
        if (hasEntry is not 0 and not 1)
        {
            throw new InvalidDataException("Klib module info has an invalid entry label flag.");
        }

        var entryLabel = hasEntry == 1 ? ReadString(reader) : null;
        return new KlibModuleInfo(scriptId, moduleId, sourcePath, entryLabel);
    }

    private static string ReadString(BinaryReader reader)
    {
        var byteLength = reader.ReadInt32();
        if (byteLength < 0)
        {
            throw new InvalidDataException("Klib string length must not be negative.");
        }

        var remaining = reader.BaseStream.Length - reader.BaseStream.Position;
        if (byteLength > remaining)
        {
            throw new InvalidDataException("Klib string length exceeds the remaining section data.");
        }

        return Encoding.UTF8.GetString(reader.ReadBytes(byteLength));
    }

    private static bool IsValidSection(long streamLength, SectionHeader section)
    {
        if (section.Offset < 0 || section.Size < 0)
        {
            return false;
        }

        return section.Offset <= streamLength && section.Size <= streamLength - section.Offset;
    }

    private static KlibModuleLoadResult InvalidKlib(string path, string message)
    {
        return KlibModuleLoadResult.Failure(
            RuntimeFailureKind.Startup,
            RuntimeDiagnostic.Error(
                "KESR2102",
                $"{message} Path: {path}",
                RuntimeFailureKind.Startup));
    }

    private readonly record struct SectionHeader(int Type, int Offset, int Size);
}

public sealed record KlibModuleLoadResult(
    KlibDocument? Document,
    IReadOnlyList<RuntimeDiagnostic> Diagnostics,
    RuntimeFailureKind FailureKind)
{
    public bool Succeeded => Document is not null && FailureKind == RuntimeFailureKind.None;

    public static KlibModuleLoadResult Success(KlibDocument document)
    {
        return new KlibModuleLoadResult(document, [], RuntimeFailureKind.None);
    }

    public static KlibModuleLoadResult Failure(RuntimeFailureKind failureKind, params RuntimeDiagnostic[] diagnostics)
    {
        return new KlibModuleLoadResult(null, diagnostics, failureKind);
    }
}
