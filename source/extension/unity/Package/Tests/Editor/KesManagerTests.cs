using NUnit.Framework;
using UnityEngine;

namespace KoromoEventScript.Unity.Editor.Tests
{

public sealed class KesManagerTests
{
    [Test]
    public void NewComponent_PlayOnStartIsEnabled()
    {
        var gameObject = new GameObject("KesManagerTest");

        try
        {
            var manager = gameObject.AddComponent<KesManager>();
            Assert.That(manager.PlayOnStart, Is.True);
        }
        finally
        {
            Object.DestroyImmediate(gameObject);
        }
    }
}
}
