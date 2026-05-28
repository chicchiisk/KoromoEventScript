using KoromoEventScript.Cli.Commands;
using KoromoEventScript.Cli.Diagnostics;
using KoromoEventScript.Cli.Semantics;

namespace KoromoEventScript.Cli.Tests.Semantics;

public class SemanticTypesTests
{
    [Test]
    public void IsAssignableFrom_AllowsNullOnlyForSupportedReferenceTypes()
    {
        Assert.Multiple(() =>
        {
            Assert.That(KesType.String.IsAssignableFrom(KesType.Null), Is.True);
            Assert.That(KesType.Actor.IsAssignableFrom(KesType.Null), Is.True);
            Assert.That(KesType.Array(KesType.Number).IsAssignableFrom(KesType.Null), Is.True);
            Assert.That(KesType.Number.IsAssignableFrom(KesType.Null), Is.False);
            Assert.That(KesType.Bool.IsAssignableFrom(KesType.Null), Is.False);
            Assert.That(KesType.Void.IsAssignableFrom(KesType.Null), Is.False);
        });
    }

    [Test]
    public void IsAssignableFrom_RequiresMatchingPrimitiveAndArrayTypes()
    {
        Assert.Multiple(() =>
        {
            Assert.That(KesType.Number.IsAssignableFrom(KesType.Number), Is.True);
            Assert.That(KesType.Number.IsAssignableFrom(KesType.String), Is.False);
            Assert.That(KesType.Array(KesType.String).IsAssignableFrom(KesType.Array(KesType.String)), Is.True);
            Assert.That(KesType.Array(KesType.String).IsAssignableFrom(KesType.Array(KesType.Number)), Is.False);
            Assert.That(KesType.Array(KesType.Array(KesType.Number)).IsAssignableFrom(KesType.Array(KesType.Array(KesType.Number))), Is.True);
        });
    }

    [Test]
    public void TypeCheckingResult_FailureCarriesCompileErrorDiagnostics()
    {
        var diagnostic = new Diagnostic(DiagnosticLevel.Error, "KES2015", "events/main.ke", 1, 1, "Type mismatch.");
        var result = TypeCheckingResult.Failure([diagnostic]);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.ExitCode, Is.EqualTo(CliExitCode.CompileError));
            Assert.That(result.Diagnostics, Is.EqualTo(new[] { diagnostic }));
        });
    }
}
