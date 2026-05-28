using KoromoEventScript.Cli.Semantics;

namespace KoromoEventScript.Cli.Tests.Semantics;

public class BuiltInSignatureRegistryTests
{
    [Test]
    public void TryResolve_ReturnsRepresentativeMvpSignatures()
    {
        var registry = new BuiltInSignatureRegistry();

        Assert.Multiple(() =>
        {
            Assert.That(registry.TryResolve("print", out var print), Is.True);
            Assert.That(print.Parameters.Single().Type, Is.EqualTo(KesType.String));
            Assert.That(print.ReturnType, Is.EqualTo(KesType.Void));

            Assert.That(registry.TryResolve("range", out var range), Is.True);
            Assert.That(range.Parameters.Select(static parameter => parameter.Type), Is.EqualTo([KesType.Number, KesType.Number]));
            Assert.That(range.ReturnType, Is.EqualTo(KesType.Array(KesType.Number)));

            Assert.That(registry.TryResolve("show", out var show), Is.True);
            Assert.That(show.Parameters[0].Type, Is.EqualTo(KesType.Actor));
            Assert.That(show.Parameters[1].Type, Is.EqualTo(KesType.Number));
            Assert.That(show.Parameters[1].IsOptional, Is.True);
        });
    }
}
