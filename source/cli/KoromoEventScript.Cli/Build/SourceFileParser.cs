using KoromoEventScript.Cli.Diagnostics;
using KoromoEventScript.Cli.Lexing;
using KoromoEventScript.Cli.Parsing;

namespace KoromoEventScript.Cli.Build;

public sealed class SourceFileParser
{
    public SourceParseResult<KelDocumentSyntax> ParseKel(string absolutePath, string displayPath)
    {
        return Parse(absolutePath, displayPath, KelParser.Parse);
    }

    public SourceParseResult<ScriptSyntax> ParseKe(string absolutePath, string displayPath)
    {
        return Parse(absolutePath, displayPath, KeParser.Parse);
    }

    private static SourceParseResult<T> Parse<T>(string absolutePath, string displayPath, Func<string, T> parse)
        where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(absolutePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayPath);

        try
        {
            var source = File.ReadAllText(absolutePath);
            return new SourceParseResult<T>(parse(source), null, SourceParseStatus.Success);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new SourceParseResult<T>(
                null,
                new Diagnostic(DiagnosticLevel.Error, "KES9004", displayPath, 1, 1, $"Could not read input file: {exception.Message}"),
                SourceParseStatus.FileError);
        }
        catch (LexerException exception)
        {
            return new SourceParseResult<T>(
                null,
                WithFile(EnsureSyntaxCode(exception.Diagnostic), displayPath),
                SourceParseStatus.SyntaxError);
        }
        catch (ParserException exception)
        {
            return new SourceParseResult<T>(
                null,
                WithFile(EnsureSyntaxCode(exception.Diagnostic), displayPath),
                SourceParseStatus.SyntaxError);
        }
    }

    private static Diagnostic EnsureSyntaxCode(Diagnostic diagnostic)
    {
        return diagnostic.Code.StartsWith("KES1", StringComparison.Ordinal)
            ? diagnostic
            : diagnostic with { Code = "KES1000" };
    }

    private static Diagnostic WithFile(Diagnostic diagnostic, string displayPath)
    {
        return diagnostic with { File = displayPath };
    }
}
