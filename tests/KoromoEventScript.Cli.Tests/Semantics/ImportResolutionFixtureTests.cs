using KoromoEventScript.Cli.Build;
using KoromoEventScript.Cli.Commands;
using KoromoEventScript.Cli.Commands.Build;
using KoromoEventScript.Cli.Diagnostics;
using KoromoEventScript.Cli.Lexing;
using KoromoEventScript.Cli.Parsing;

namespace KoromoEventScript.Cli.Tests.Semantics;

public class ImportResolutionFixtureTests
{
    private static readonly string[] ScenarioNames =
    [
        "success",
        "missing-import",
        "ambiguous-import",
        "cycle",
        "syntax-error",
        "name-resolution-failure",
    ];

    [Test]
    public void Fixtures_ContainExpectedImportResolutionScenarios()
    {
        var fixtureRoot = GetImportResolutionFixtureRoot();

        Assert.Multiple(() =>
        {
            Assert.That(Directory.Exists(fixtureRoot), Is.True, "fixture root should exist");
            foreach (var scenarioName in ScenarioNames)
            {
                Assert.That(File.Exists(Path.Combine(fixtureRoot, scenarioName, "kes.xml")), Is.True, scenarioName);
                Assert.That(File.Exists(Path.Combine(fixtureRoot, scenarioName, "events", "main.kel")), Is.True, scenarioName);
                Assert.That(File.Exists(Path.Combine(fixtureRoot, scenarioName, "events", "main.ke")), Is.True, scenarioName);
            }
        });
    }

    [Test]
    public void Fixtures_ParseAllSyntaxValidProjectInputsAndMarkIntentionalSyntaxError()
    {
        var parser = new SourceFileParser();

        foreach (var scenarioName in ScenarioNames)
        {
            var projectRoot = Path.Combine(GetImportResolutionFixtureRoot(), scenarioName);
            var kelResult = parser.ParseKel(
                Path.Combine(projectRoot, "events", "main.kel"),
                "events/main.kel");

            Assert.That(kelResult.Status, Is.EqualTo(SourceParseStatus.Success), scenarioName);

            foreach (var scriptPath in Directory.EnumerateFiles(Path.Combine(projectRoot, "events"), "*.*", SearchOption.AllDirectories)
                         .Where(static path => path.EndsWith(".ke", StringComparison.Ordinal) || path.EndsWith(".kc", StringComparison.Ordinal)))
            {
                var relativePath = Path.GetRelativePath(projectRoot, scriptPath).Replace('\\', '/');
                var result = parser.ParseKe(scriptPath, relativePath);
                var expectedStatus = relativePath == "events/broken/Broken.ke"
                    ? SourceParseStatus.SyntaxError
                    : SourceParseStatus.Success;

                Assert.That(result.Status, Is.EqualTo(expectedStatus), $"{scenarioName}: {relativePath}");
            }
        }
    }

    [Test]
    public void Fixtures_ReproduceImportAndNameResolutionShapes()
    {
        var expectations = new Dictionary<string, FixtureExpectation>(StringComparer.Ordinal)
        {
            ["success"] = new(
                Imports: ["Common", "LegacyCommon", "Shared"],
                PresentModules: ["main", "Common", "LegacyCommon", "Shared"],
                DuplicateModules: [],
                MissingImports: [],
                CycleEdges: [],
                UnimportedReferences: [],
                AmbiguousReferences: []),
            ["missing-import"] = new(
                Imports: ["DoesNotExist"],
                PresentModules: ["main"],
                DuplicateModules: [],
                MissingImports: ["DoesNotExist"],
                CycleEdges: [],
                UnimportedReferences: [],
                AmbiguousReferences: []),
            ["ambiguous-import"] = new(
                Imports: ["Common"],
                PresentModules: ["main", "Common"],
                DuplicateModules: ["Common"],
                MissingImports: [],
                CycleEdges: [],
                UnimportedReferences: [],
                AmbiguousReferences: []),
            ["cycle"] = new(
                Imports: ["A", "B"],
                PresentModules: ["main", "A", "B"],
                DuplicateModules: [],
                MissingImports: [],
                CycleEdges: [("A", "B"), ("B", "A")],
                UnimportedReferences: [],
                AmbiguousReferences: []),
            ["syntax-error"] = new(
                Imports: ["Broken"],
                PresentModules: ["main", "Broken"],
                DuplicateModules: [],
                MissingImports: [],
                CycleEdges: [],
                UnimportedReferences: [],
                AmbiguousReferences: []),
            ["name-resolution-failure"] = new(
                Imports: ["Common", "Other"],
                PresentModules: ["main", "Common", "Other", "Hidden"],
                DuplicateModules: [],
                MissingImports: [],
                CycleEdges: [],
                UnimportedReferences: ["hiddenOnly"],
                AmbiguousReferences: ["sharedName"]),
        };

        foreach (var (scenarioName, expectation) in expectations)
        {
            var scenario = ReadScenario(scenarioName);

            Assert.Multiple(() =>
            {
                Assert.That(scenario.Imports, Is.SupersetOf(expectation.Imports), scenarioName);
                Assert.That(scenario.Modules.Keys, Is.SupersetOf(expectation.PresentModules), scenarioName);
                Assert.That(scenario.DuplicateModules, Is.EqualTo(expectation.DuplicateModules), scenarioName);
                Assert.That(scenario.Imports.Except(scenario.Modules.Keys), Is.EqualTo(expectation.MissingImports), scenarioName);
                Assert.That(scenario.ImportEdges, Is.SupersetOf(expectation.CycleEdges), scenarioName);
                Assert.That(scenario.UnimportedReferences, Is.EqualTo(expectation.UnimportedReferences), scenarioName);
                Assert.That(scenario.AmbiguousReferences, Is.EqualTo(expectation.AmbiguousReferences), scenarioName);
            });
        }
    }

    [Test]
    public void BuildCheckOnly_LoadsEveryFixtureProject()
    {
        foreach (var scenarioName in ScenarioNames)
        {
            var projectRoot = Path.Combine(GetImportResolutionFixtureRoot(), scenarioName);

            var result = new BuildCheckOnlyCommand().Execute(
                new BuildCommandOptions(projectRoot, DiagnosticOutputFormat.Text),
                TestContext.CurrentContext.WorkDirectory);

            Assert.That(
                result.ExitCode,
                Is.EqualTo(CliExitCode.Success),
                scenarioName);
        }
    }

    private static FixtureScenario ReadScenario(string scenarioName)
    {
        var projectRoot = Path.Combine(GetImportResolutionFixtureRoot(), scenarioName);
        var modulePaths = Directory.EnumerateFiles(Path.Combine(projectRoot, "events"), "*.*", SearchOption.AllDirectories)
            .Where(static path => path.EndsWith(".ke", StringComparison.Ordinal) || path.EndsWith(".kc", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();
        var modules = modulePaths
            .GroupBy(static path => Path.GetFileNameWithoutExtension(path), StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.ToArray(), StringComparer.Ordinal);
        var syntaxByModule = new Dictionary<string, ScriptSyntax>(StringComparer.Ordinal);
        var imports = new List<string>();
        var importEdges = new List<(string From, string To)>();
        var importedModulesByModule = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var definitionsByModule = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var modulePath in modulePaths)
        {
            var moduleName = Path.GetFileNameWithoutExtension(modulePath);
            try
            {
                syntaxByModule[moduleName] = KeParser.Parse(File.ReadAllText(modulePath));
            }
            catch (Exception exception) when (exception is LexerException or ParserException)
            {
            }
        }

        foreach (var (moduleName, syntax) in syntaxByModule)
        {
            foreach (var statement in syntax.Statements)
            {
                if (statement is ImportStatementSyntax importStatement)
                {
                    imports.Add(importStatement.ModuleName);
                    importEdges.Add((moduleName, importStatement.ModuleName));
                    if (!importedModulesByModule.TryGetValue(moduleName, out var importedModules))
                    {
                        importedModules = [];
                        importedModulesByModule[moduleName] = importedModules;
                    }

                    importedModules.Add(importStatement.ModuleName);
                }

                if (statement is VarStatementSyntax varStatement)
                {
                    if (!definitionsByModule.TryGetValue(moduleName, out var definitions))
                    {
                        definitions = [];
                        definitionsByModule[moduleName] = definitions;
                    }

                    definitions.Add(varStatement.Name);
                }
            }
        }

        var unimportedReferences = new List<string>();
        var ambiguousReferences = new List<string>();

        foreach (var (moduleName, syntax) in syntaxByModule)
        {
            foreach (var statement in syntax.Statements)
            {
                if (statement is CommandStatementSyntax { Name: "use" } command)
                {
                    foreach (var identifier in command.Arguments.Where(static token => token.Kind == TokenKind.Identifier))
                    {
                        var importedDefinitions = importedModulesByModule.TryGetValue(moduleName, out var importedModules)
                            ? importedModules.Count(importedModule =>
                                definitionsByModule.TryGetValue(importedModule, out var definitions) &&
                                definitions.Contains(identifier.Lexeme, StringComparer.Ordinal))
                            : 0;

                        if (importedDefinitions > 1)
                        {
                            ambiguousReferences.Add(identifier.Lexeme);
                        }

                        if (importedDefinitions == 0 &&
                            definitionsByModule.TryGetValue("Hidden", out var hiddenDefinitions) &&
                            hiddenDefinitions.Contains(identifier.Lexeme, StringComparer.Ordinal))
                        {
                            unimportedReferences.Add(identifier.Lexeme);
                        }
                    }
                }
            }
        }

        var duplicateModules = modules
            .Where(static pair => pair.Value.Length > 1)
            .Select(static pair => pair.Key)
            .Order(StringComparer.Ordinal)
            .ToArray();

        return new FixtureScenario(
            imports.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
            modules,
            duplicateModules,
            importEdges.Distinct().OrderBy(static edge => edge.From, StringComparer.Ordinal).ThenBy(static edge => edge.To, StringComparer.Ordinal).ToArray(),
            unimportedReferences.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
            ambiguousReferences.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());
    }

    private static string GetImportResolutionFixtureRoot()
    {
        return Path.Combine(GetRepositoryRoot(), "testdata", "projects", "import-resolution");
    }

    private static string GetRepositoryRoot()
    {
        return Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", ".."));
    }

    private sealed record FixtureScenario(
        IReadOnlyList<string> Imports,
        IReadOnlyDictionary<string, string[]> Modules,
        IReadOnlyList<string> DuplicateModules,
        IReadOnlyList<(string From, string To)> ImportEdges,
        IReadOnlyList<string> UnimportedReferences,
        IReadOnlyList<string> AmbiguousReferences);

    private sealed record FixtureExpectation(
        IReadOnlyList<string> Imports,
        IReadOnlyList<string> PresentModules,
        IReadOnlyList<string> DuplicateModules,
        IReadOnlyList<string> MissingImports,
        IReadOnlyList<(string From, string To)> CycleEdges,
        IReadOnlyList<string> UnimportedReferences,
        IReadOnlyList<string> AmbiguousReferences);
}
