using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class AutoStartFromOpeningScene
{
    private const string OpeningScenePath = "Assets/3.ChangHEE/Scene/OpeningScene.unity";

    static AutoStartFromOpeningScene()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.ExitingEditMode)
            return;

        var openingScene = SceneUtility.GetBuildIndexByScenePath(OpeningScenePath);
        if (openingScene < 0)
        {
            Debug.LogWarning(
                $"[AutoStartFromOpeningScene] '{OpeningScenePath}' is not in Build Settings. " +
                "Play mode will start from the currently open scene.");
            return;
        }

        if (openingScene != 0)
        {
            Debug.LogWarning(
                $"[AutoStartFromOpeningScene] Opening scene is at build index {openingScene}, not 0. " +
                "Please move it to the top of File > Build Settings.");
        }

        var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(OpeningScenePath);
        if (sceneAsset == null)
        {
            Debug.LogError($"[AutoStartFromOpeningScene] Failed to load scene asset at '{OpeningScenePath}'.");
            return;
        }

        EditorSceneManager.playModeStartScene = sceneAsset;
    }
}
