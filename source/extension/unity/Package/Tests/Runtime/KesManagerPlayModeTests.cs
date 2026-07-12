using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace KoromoEventScript.Unity.Runtime.Tests;

public sealed class KesManagerPlayModeTests
{
    [UnityTest]
    public IEnumerator Component_CanEnterPlayMode()
    {
        var gameObject = new GameObject("KesManagerPlayModeTest");
        var manager = gameObject.AddComponent<KesManager>();

        yield return null;

        Assert.That(manager.isActiveAndEnabled, Is.True);
        Object.Destroy(gameObject);
    }
}
