using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class AutoStartFromOpeningScene
{
    private const string GameStartScreenScenePath = "Assets/Scenes/Game start screen.unity";

    static AutoStartFromOpeningScene()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.ExitingEditMode)
            return;

        var gameStartScene = SceneUtility.GetBuildIndexByScenePath(GameStartScreenScenePath);
        if (gameStartScene < 0)
        {
            Debug.LogWarning(
                $"[AutoStartFromOpeningScene] '{GameStartScreenScenePath}' is not in Build Settings. " +
                "Play mode will start from the currently open scene.");
            return;
        }

        if (gameStartScene != 0)
        {
            Debug.LogWarning(
                $"[AutoStartFromOpeningScene] Game start screen is at build index {gameStartScene}, not 0. " +
                "Please move it to the top of File > Build Settings.");
        }

        var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(GameStartScreenScenePath);
        if (sceneAsset == null)
        {
            Debug.LogError($"[AutoStartFromOpeningScene] Failed to load scene asset at '{GameStartScreenScenePath}'.");
            return;
        }

        EditorSceneManager.playModeStartScene = sceneAsset;
    }
}
