#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MonsterSpawner))]
public class MonsterSpawnerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            if (GUILayout.Button("Next Factory Stage"))
            {
                MonsterSpawner spawner = (MonsterSpawner)target;
                spawner.NextFactoryStage();
            }
        }

        if (!Application.isPlaying)
            EditorGUILayout.HelpBox("Next Factory Stage 버튼은 Play 모드에서만 사용할 수 있습니다.", MessageType.Info);
    }
}
#endif
