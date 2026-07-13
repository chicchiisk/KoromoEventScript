using UnityEngine;

namespace KoromoEventScript.Unity
{

[DisallowMultipleComponent]
[AddComponentMenu("KoromoEventScript/KES Manager")]
public sealed class KesManager : MonoBehaviour
{
    [SerializeField]
    private KesBuildAsset buildAsset;

    [SerializeField]
    private bool playOnStart = true;

    public KesBuildAsset BuildAsset => buildAsset;

    public bool PlayOnStart => playOnStart;

    public void SetBuildAsset(KesBuildAsset value)
    {
        buildAsset = value;
    }

    private void Start()
    {
        if (playOnStart && buildAsset == null)
        {
            Debug.LogError("KESU2001: KES Manager cannot start because no KES Build Asset is assigned.", this);
        }
    }
}
}
