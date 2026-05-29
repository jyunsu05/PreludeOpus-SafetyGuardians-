#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ItemSpawner))]
public class ItemSpawnerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            if (GUILayout.Button("Next Factory Stage"))
            {
                ItemSpawner spawner = (ItemSpawner)target;
                spawner.NextFactoryStage();
            }
        }

        if (!Application.isPlaying)
            EditorGUILayout.HelpBox("Next Factory Stage button is available only in Play Mode.", MessageType.Info);
    }
}
#endif
