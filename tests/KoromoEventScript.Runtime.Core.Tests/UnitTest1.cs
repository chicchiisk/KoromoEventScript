using KoromoEventScript.Runtime.Core;

namespace KoromoEventScript.Runtime.Core.Tests;

public sealed class RuntimeCoreAssemblyTests
{
    [Test]
    public void RuntimeCoreAssemblyIsReferenceable()
    {
        Assert.That(typeof(RuntimeCoreAssemblyMarker).Assembly.GetName().Name, Is.EqualTo("KoromoEventScript.Runtime.Core"));
    }
}
