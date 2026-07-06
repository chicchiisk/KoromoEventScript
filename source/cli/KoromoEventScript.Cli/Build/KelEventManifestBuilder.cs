using System.Globalization;
using KoromoEventScript.Cli.Parsing;

namespace KoromoEventScript.Cli.Build;

public sealed class KelEventManifestBuilder
{
    public IReadOnlyList<BuildManifestEventEntry> BuildEvents(
        KelDocumentSyntax document,
        IReadOnlyList<BuildManifestScriptArtifact> scripts)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(scripts);

        var entryEventId = ReadScalar(document.Root, "entry");
        var scriptBySourcePath = scripts.ToDictionary(
            static script => NormalizePath(script.SourcePath),
            static script => script,
            StringComparer.OrdinalIgnoreCase);
        var events = new List<BuildManifestEventEntry>();

        foreach (var property in document.Root.Properties)
        {
            foreach (var value in property.Values)
            {
                if (value is not KelObjectValueSyntax eventObject ||
                    TryReadScalar(eventObject.Object, "chapter", out var chapter) is false)
                {
                    continue;
                }

                if (!scriptBySourcePath.TryGetValue(NormalizePath(chapter), out var script))
                {
                    continue;
                }

                events.Add(new BuildManifestEventEntry(
                    property.Key,
                    ReadScalar(eventObject.Object, "type"),
                    chapter,
                    script.ScriptId,
                    StringComparer.Ordinal.Equals(property.Key, entryEventId),
                    ReadTrigger(eventObject.Object)));
            }
        }

        return events;
    }

    private static BuildManifestTrigger? ReadTrigger(KelObjectSyntax eventObject)
    {
        var triggerValues = eventObject.Properties
            .Where(static property => StringComparer.Ordinal.Equals(property.Key, "trigger"))
            .SelectMany(static property => property.Values)
            .OfType<KelObjectValueSyntax>()
            .ToArray();
        if (triggerValues.Length == 0)
        {
            return null;
        }

        var conditions = new List<BuildManifestTriggerCondition>();
        var or = new List<BuildManifestTrigger>();
        foreach (var trigger in triggerValues)
        {
            ReadTriggerObject(trigger.Object, conditions, or);
        }

        return new BuildManifestTrigger(conditions, or);
    }

    private static BuildManifestTrigger ReadNestedTrigger(KelObjectSyntax syntax)
    {
        var conditions = new List<BuildManifestTriggerCondition>();
        var or = new List<BuildManifestTrigger>();
        ReadTriggerObject(syntax, conditions, or);
        return new BuildManifestTrigger(conditions, or);
    }

    private static void ReadTriggerObject(
        KelObjectSyntax syntax,
        List<BuildManifestTriggerCondition> conditions,
        List<BuildManifestTrigger> or)
    {
        foreach (var property in syntax.Properties)
        {
            foreach (var value in property.Values)
            {
                switch (property.Key)
                {
                    case "from":
                        if (TryReadScalar(value, out var from))
                        {
                            conditions.Add(new BuildManifestTriggerCondition("from", from, null, null));
                        }

                        break;

                    case "is":
                        if (value is KelObjectValueSyntax isObject &&
                            TryReadScalar(isObject.Object, "param", out var param) &&
                            TryReadFirstValue(isObject.Object, "value", out var expectedValue) &&
                            TryReadTriggerValue(expectedValue, out var triggerValue))
                        {
                            conditions.Add(new BuildManifestTriggerCondition("is", null, param, triggerValue));
                        }

                        break;

                    case "or":
                        if (value is KelObjectValueSyntax orObject)
                        {
                            or.Add(ReadNestedTrigger(orObject.Object));
                        }

                        break;
                }
            }
        }
    }

    private static bool TryReadTriggerValue(KelValueSyntax value, out BuildManifestTriggerValue triggerValue)
    {
        switch (value)
        {
            case KelStringValueSyntax stringValue:
                triggerValue = new BuildManifestTriggerValue("string", stringValue.Value);
                return true;

            case KelIdentifierValueSyntax identifierValue:
                triggerValue = new BuildManifestTriggerValue("string", identifierValue.Value);
                return true;

            case KelNumberValueSyntax numberValue:
                triggerValue = new BuildManifestTriggerValue("number", double.Parse(numberValue.Value, CultureInfo.InvariantCulture).ToString("G", CultureInfo.InvariantCulture));
                return true;

            case KelBooleanValueSyntax boolValue:
                triggerValue = new BuildManifestTriggerValue("bool", boolValue.Value ? "true" : "false");
                return true;

            default:
                triggerValue = null!;
                return false;
        }
    }

    private static string? ReadScalar(KelObjectSyntax syntax, string key)
    {
        return TryReadScalar(syntax, key, out var value) ? value : null;
    }

    private static bool TryReadScalar(KelObjectSyntax syntax, string key, out string value)
    {
        value = string.Empty;
        return TryReadFirstValue(syntax, key, out var syntaxValue) && TryReadScalar(syntaxValue, out value);
    }

    private static bool TryReadFirstValue(KelObjectSyntax syntax, string key, out KelValueSyntax value)
    {
        value = null!;
        var property = syntax.Properties.FirstOrDefault(property => StringComparer.Ordinal.Equals(property.Key, key));
        if (property is null || property.Values.Count == 0)
        {
            return false;
        }

        value = property.Values[0];
        return true;
    }

    private static bool TryReadScalar(KelValueSyntax syntax, out string value)
    {
        switch (syntax)
        {
            case KelStringValueSyntax stringValue:
                value = stringValue.Value;
                return true;

            case KelIdentifierValueSyntax identifierValue:
                value = identifierValue.Value;
                return true;

            case KelNumberValueSyntax numberValue:
                value = numberValue.Value;
                return true;

            case KelBooleanValueSyntax boolValue:
                value = boolValue.Value ? "true" : "false";
                return true;

            default:
                value = string.Empty;
                return false;
        }
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/');
    }
}
