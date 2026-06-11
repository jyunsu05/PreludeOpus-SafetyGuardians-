using UnityEditor;
using UnityEngine;

namespace SLA.Editor
{
    public static class FactorySoundSetupEditor
    {
        private const string SoundRoot = "Assets/1.Yunseo/sound/Fx/factory/";
        private const string MapRoot = "Assets/2.SLA/Prefabs/FactoryMaps/";
        private const string PlayerPrefabPath = "Assets/2.SLA/Prefabs/Player.prefab";
        private const string SirenClipPath = "Assets/1.Yunseo/sound/Fx/siren.wav";

        public static void ApplyFactorySoundSetupSilent()
        {
            ApplyFactorySoundSetupInternal(showDialog: false);
        }

        [MenuItem("Tools/Safety Guardians/공장 사운드 자동 적용")]
        public static void ApplyFactorySoundSetup()
        {
            ApplyFactorySoundSetupInternal(showDialog: true);
        }

        private static void ApplyFactorySoundSetupInternal(bool showDialog)
        {
            AudioClip machine = LoadClip("factory machine sound.wav");
            AudioClip abandoned = LoadClip("abandoned factory.wav");
            AudioClip pipe = LoadClip("factory pipe.wav");
            AudioClip waterDrop = LoadClip("factory water drop.wav");
            AudioClip smoke1 = LoadClip("pipe smoke_1.wav");
            AudioClip smoke2 = LoadClip("pipe smoke_2.wav");
            AudioClip siren = AssetDatabase.LoadAssetAtPath<AudioClip>(SirenClipPath);

            int appliedMaps = 0;

            appliedMaps += ApplyMapPrefab($"{MapRoot}FactoryMap_Chapter1.prefab", 1, machine, abandoned, pipe, waterDrop, smoke1, smoke2);
            appliedMaps += ApplyMapPrefab($"{MapRoot}FactoryMap_Chapter2.prefab", 2, machine, abandoned, pipe, waterDrop, smoke1, smoke2);
            appliedMaps += ApplyMapPrefab($"{MapRoot}FactoryMap_Chapter3.prefab", 3, machine, abandoned, pipe, waterDrop, smoke1, smoke2);

            bool playerUpdated = ApplyPlayerSiren(siren);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"[FactorySoundSetup] 완료 — 맵 {appliedMaps}/3, 플레이어 사이렌={(playerUpdated ? "OK" : "FAIL")}");

            if (!showDialog)
                return;

            EditorUtility.DisplayDialog(
                "공장 사운드 적용 완료",
                $"맵 프리팹 {appliedMaps}/3 적용\n" +
                $"플레이어 사이렌: {(playerUpdated ? "적용됨" : "실패")}\n\n" +
                "프로젝트 창에서 FactoryMap_Chapter1.prefab 을 열어 확인하세요.",
                "확인");

            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>($"{MapRoot}FactoryMap_Chapter1.prefab");
            EditorGUIUtility.PingObject(Selection.activeObject);
        }

        [MenuItem("Tools/Safety Guardians/공장 사운드 설정 위치 열기")]
        public static void PingFactorySoundAssets()
        {
            GameObject chapter1 = AssetDatabase.LoadAssetAtPath<GameObject>($"{MapRoot}FactoryMap_Chapter1.prefab");
            if (chapter1 != null)
            {
                Selection.activeObject = chapter1;
                EditorGUIUtility.PingObject(chapter1);
            }

            Debug.Log(
                "[공장 사운드 위치]\n" +
                "1) FactoryMap_Chapter1/2/3 프리팹 루트 → FactoryAmbientSoundController\n" +
                "2) FactoryMap_Chapter1/2/3_Grid → FactoryPipeSmokeSoundZone\n" +
                "3) Player.prefab → PlayerOxygen → Oxygen Siren Clip");
        }

        private static int ApplyMapPrefab(
            string prefabPath,
            int chapterIndex,
            AudioClip machine,
            AudioClip abandoned,
            AudioClip pipe,
            AudioClip waterDrop,
            AudioClip smoke1,
            AudioClip smoke2)
        {
            GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefabRoot == null)
            {
                Debug.LogError($"[FactorySoundSetup] 프리팹 없음: {prefabPath}");
                return 0;
            }

            string prefabContentsPath = prefabPath;
            GameObject root = PrefabUtility.LoadPrefabContents(prefabContentsPath);
            if (root == null)
            {
                Debug.LogError($"[FactorySoundSetup] 프리팹 로드 실패: {prefabPath}");
                return 0;
            }

            try
            {
                bool enableWaterDrop = chapterIndex == 1;
                bool enablePipeSmoke = chapterIndex <= 2;

                FactoryAmbientSoundController ambient =
                    root.GetComponent<FactoryAmbientSoundController>() ??
                    root.AddComponent<FactoryAmbientSoundController>();

                ConfigureAmbient(ambient, machine, abandoned, pipe, waterDrop, enableWaterDrop);

                Transform grid = FindChildRecursive(root.transform, $"FactoryMap_Chapter{chapterIndex}_Grid");
                if (grid == null)
                {
                    Debug.LogError($"[FactorySoundSetup] Grid 없음: {prefabPath}");
                    return 0;
                }

                FactoryPipeSmokeSoundZone smokeZone =
                    grid.GetComponent<FactoryPipeSmokeSoundZone>() ??
                    grid.gameObject.AddComponent<FactoryPipeSmokeSoundZone>();

                ConfigurePipeSmoke(smokeZone, smoke1, smoke2, enablePipeSmoke);

                PrefabUtility.SaveAsPrefabAsset(root, prefabContentsPath);
                Debug.Log($"[FactorySoundSetup] 적용 완료 — {prefabPath} (waterDrop={enableWaterDrop}, pipeSmoke={enablePipeSmoke})");
                return 1;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ConfigureAmbient(
            FactoryAmbientSoundController ambient,
            AudioClip machine,
            AudioClip abandoned,
            AudioClip pipe,
            AudioClip waterDrop,
            bool enableWaterDrop)
        {
            SerializedObject so = new SerializedObject(ambient);
            so.FindProperty("machineLoopClip").objectReferenceValue = machine;
            so.FindProperty("abandonedFactoryLoopClip").objectReferenceValue = abandoned;
            so.FindProperty("machineVolume").floatValue = 0.28f;
            so.FindProperty("abandonedVolume").floatValue = 0.85f;
            so.FindProperty("pauseDuringBattle").boolValue = true;
            so.FindProperty("pipeAmbienceClip").objectReferenceValue = pipe;
            so.FindProperty("pipeVolume").floatValue = 0.22f;
            so.FindProperty("pipeIntervalMinSeconds").floatValue = 18f;
            so.FindProperty("pipeIntervalMaxSeconds").floatValue = 38f;
            so.FindProperty("pipeFirstPlayDelaySeconds").floatValue = 6f;
            so.FindProperty("enableWaterDropAmbience").boolValue = enableWaterDrop;
            so.FindProperty("waterDropAmbienceClip").objectReferenceValue = waterDrop;
            so.FindProperty("waterDropVolume").floatValue = 0.35f;
            so.FindProperty("waterDropIntervalMinSeconds").floatValue = 8f;
            so.FindProperty("waterDropIntervalMaxSeconds").floatValue = 16f;
            so.FindProperty("waterDropFirstPlayDelaySeconds").floatValue = 3f;
            so.FindProperty("doublePlayChance").floatValue = 0.35f;
            so.FindProperty("doublePlayGapMinSeconds").floatValue = 0.15f;
            so.FindProperty("doublePlayGapMaxSeconds").floatValue = 0.45f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigurePipeSmoke(
            FactoryPipeSmokeSoundZone smokeZone,
            AudioClip smoke1,
            AudioClip smoke2,
            bool enablePipeSmoke)
        {
            SerializedObject so = new SerializedObject(smokeZone);
            so.FindProperty("enablePipeSmokeSound").boolValue = enablePipeSmoke;
            so.FindProperty("pipeSmokeClip1").objectReferenceValue = smoke1;
            so.FindProperty("pipeSmokeClip2").objectReferenceValue = smoke2;
            so.FindProperty("proximityRadius").floatValue = 2.5f;
            so.FindProperty("volume").floatValue = 0.22f;
            so.FindProperty("playChance").floatValue = 0.35f;
            so.FindProperty("checkIntervalMinSeconds").floatValue = 2f;
            so.FindProperty("checkIntervalMaxSeconds").floatValue = 4.5f;
            so.FindProperty("cooldownAfterPlaySeconds").floatValue = 5f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static bool ApplyPlayerSiren(AudioClip siren)
        {
            if (siren == null)
            {
                Debug.LogWarning("[FactorySoundSetup] siren.wav 없음");
                return false;
            }

            GameObject playerRoot = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            if (playerRoot == null)
                return false;

            try
            {
                PlayerOxygen oxygen = playerRoot.GetComponent<PlayerOxygen>();
                if (oxygen == null)
                    return false;

                SerializedObject so = new SerializedObject(oxygen);
                so.FindProperty("oxygenSirenClip").objectReferenceValue = siren;
                so.FindProperty("sirenThresholdHigh").floatValue = 50f;
                so.FindProperty("sirenThresholdMid").floatValue = 30f;
                so.FindProperty("sirenThresholdLow").floatValue = 10f;
                so.FindProperty("sirenPitchAtHigh").floatValue = 0.85f;
                so.FindProperty("sirenPitchAtMid").floatValue = 1.15f;
                so.FindProperty("sirenPitchAtLow").floatValue = 1.5f;
                so.FindProperty("sirenVolume").floatValue = 0.7f;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(playerRoot, PlayerPrefabPath);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(playerRoot);
            }
        }

        private static Transform FindChildRecursive(Transform parent, string objectName)
        {
            if (parent.name == objectName)
                return parent;

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform found = FindChildRecursive(parent.GetChild(i), objectName);
                if (found != null)
                    return found;
            }

            return null;
        }

        private static AudioClip LoadClip(string fileName)
        {
            return AssetDatabase.LoadAssetAtPath<AudioClip>($"{SoundRoot}{fileName}");
        }
    }
}
