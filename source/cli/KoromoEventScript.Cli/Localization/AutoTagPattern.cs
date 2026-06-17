using System.Text;
using KoromoEventScript.Cli.Parsing;

namespace KoromoEventScript.Cli.Localization;

internal static class AutoTagPattern
{
    public static string NormalizeScriptFileName(string projectRelativePath)
    {
        var fileName = Path.GetFileNameWithoutExtension(projectRelativePath);
        var builder = new StringBuilder(fileName.Length);
        foreach (var character in fileName)
        {
            if (character == '_')
            {
                builder.Append('_');
            }
            else if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return builder.ToString();
    }

    public static string CreateTag(string prefix, string normalizedFileName, int number)
    {
        return $"#{prefix}_{normalizedFileName}_{FormatNumber(number)}";
    }

    public static bool TryParseNumber(string tag, string normalizedFileName, out int number)
    {
        number = 0;
        if (string.IsNullOrWhiteSpace(tag))
        {
            return false;
        }

        var prefixes = new[] { $"#sy_{normalizedFileName}_", $"#na_{normalizedFileName}_", $"#se_{normalizedFileName}_" };
        foreach (var prefix in prefixes)
        {
            if (!tag.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            return int.TryParse(tag[prefix.Length..], out number);
        }

        return false;
    }

    public static string GetPrefix(TagAssignmentKind kind)
    {
        return kind switch
        {
            TagAssignmentKind.Say => "sy",
            TagAssignmentKind.Nar => "na",
            TagAssignmentKind.Select => "se",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }

    public static string FormatNumber(int number)
    {
        return number >= 10000 ? number.ToString() : number.ToString("0000");
    }
}
