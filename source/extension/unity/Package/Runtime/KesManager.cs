using UnityEngine;

namespace KoromoEventScript.Unity;

[DisallowMultipleComponent]
[AddComponentMenu("KoromoEventScript/KES Manager")]
public sealed class KesManager : MonoBehaviour
{
    [SerializeField]
    private bool playOnStart = true;

    public bool PlayOnStart => playOnStart;
}
