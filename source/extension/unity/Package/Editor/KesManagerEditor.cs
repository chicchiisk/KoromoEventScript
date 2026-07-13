using UnityEditor;

namespace KoromoEventScript.Unity.Editor
{

[CustomEditor(typeof(KesManager))]
internal sealed class KesManagerEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var manager = (KesManager)target;
        if (manager.BuildAsset == null)
        {
            EditorGUILayout.HelpBox(
                "Assign a KES Build Asset imported from manifest.kson.",
                MessageType.Warning);
        }
    }
}
}
