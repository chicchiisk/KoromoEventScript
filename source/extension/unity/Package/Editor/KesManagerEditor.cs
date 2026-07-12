using UnityEditor;

namespace KoromoEventScript.Unity.Editor;

[CustomEditor(typeof(KesManager))]
internal sealed class KesManagerEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.HelpBox(
            "KES Build Asset support will be added with the runtime importer implementation.",
            MessageType.Info);
    }
}
