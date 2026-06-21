using KoromoEventScript.Runtime.Core;

namespace KoromoEventScript.Runtime.Windows.Tests;

public sealed class WindowsRuntimeTestProjectTests
{
    [Test]
    public void WindowsHostTestsCanReferenceRuntimeCore()
    {
        Assert.That(typeof(RuntimeCoreAssemblyMarker).Assembly.GetName().Name, Is.EqualTo("KoromoEventScript.Runtime.Core"));
    }
}
