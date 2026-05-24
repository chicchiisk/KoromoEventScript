using KoromoEventScript.Cli.Parsing;

namespace KoromoEventScript.Cli.Build;

public sealed class KelScriptReferenceResolver
{
    public IReadOnlyList<string> ResolveScriptReferences(KelDocumentSyntax document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var results = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        VisitObject(document.Root, results, seen);
        return results;
    }

    private static void VisitObject(KelObjectSyntax syntax, List<string> results, HashSet<string> seen)
    {
        foreach (var property in syntax.Properties)
        {
            foreach (var value in property.Values)
            {
                if (string.Equals(property.Key, "chapter", StringComparison.Ordinal))
                {
                    AddReference(value, results, seen);
                }

                if (value is KelObjectValueSyntax objectValue)
                {
                    VisitObject(objectValue.Object, results, seen);
                }
            }
        }
    }

    private static void AddReference(KelValueSyntax value, List<string> results, HashSet<string> seen)
    {
        var reference = value switch
        {
            KelStringValueSyntax stringValue => stringValue.Value,
            KelIdentifierValueSyntax identifierValue => identifierValue.Value,
            _ => null,
        };

        if (string.IsNullOrWhiteSpace(reference) || !seen.Add(reference))
        {
            return;
        }

        results.Add(reference);
    }
}
