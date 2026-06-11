#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// UIMainHUD의 UIInventoryIcon만 2.SLA 프리팹으로 교체하고 참조를 복구합니다.
/// Tools > Wire UIMainHUD Inventory Icon 메뉴로 실행하세요.
/// </summary>
public static class WireUIMainHUDChildPrefabs
{
    const string HudPrefabPath = "Assets/1.Yunseo/Prefab/UIMainHUD.prefab";
    const string InventoryIconPath = "Assets/2.SLA/Prefabs/UIMainHUD/UIInventoryIcon.prefab";

    [MenuItem("Tools/Wire UIMainHUD Inventory Icon")]
    public static void WireInventoryIcon()
    {
        if (!WirePrefab())
            return;

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[WireUIMainHUD] UIInventoryIcon 프리팹 연결 완료.");
    }

    static bool WirePrefab()
    {
        var inventoryIconPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(InventoryIconPath);
        if (inventoryIconPrefab == null)
        {
            Debug.LogError("[WireUIMainHUD] UIInventoryIcon 프리팹을 찾을 수 없습니다.");
            return false;
        }

        var root = PrefabUtility.LoadPrefabContents(HudPrefabPath);
        try
        {
            var hud = root.GetComponent<UIMainHUD>();
            if (hud == null)
            {
                Debug.LogError("[WireUIMainHUD] UIMainHUD 컴포넌트가 없습니다.");
                return false;
            }

            var hudSo = new SerializedObject(hud);
            var inventoryRef = hudSo.FindProperty("inventory")?.objectReferenceValue;

            var existingIcon = root.transform.Find("UIInventoryIcon");
            if (existingIcon != null && PrefabUtility.GetCorrespondingObjectFromSource(existingIcon.gameObject) == inventoryIconPrefab)
            {
                WireHudReferences(root, hud, inventoryRef);
                PrefabUtility.SaveAsPrefabAsset(root, HudPrefabPath);
                return true;
            }

            if (existingIcon != null)
                Object.DestroyImmediate(existingIcon.gameObject);

            var inventoryIcon = (GameObject)PrefabUtility.InstantiatePrefab(inventoryIconPrefab, root.transform);
            inventoryIcon.name = "UIInventoryIcon";
            ApplyInventoryIconLayout(inventoryIcon);

            WireHudReferences(root, hud, inventoryRef);
            PrefabUtility.SaveAsPrefabAsset(root, HudPrefabPath);
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static void ApplyInventoryIconLayout(GameObject inventoryIcon)
    {
        var rt = inventoryIcon.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(-226f, 186f);
        rt.sizeDelta = new Vector2(181.006f, 189.17f);
        rt.localScale = Vector3.one;
    }

    static void WireHudReferences(GameObject root, UIMainHUD hud, Object inventoryRef)
    {
        var bagButton = root.transform.Find("UIInventoryIcon/inventory button")?.GetComponent<Button>();
        var hudSo = new SerializedObject(hud);
        hudSo.FindProperty("bagButton").objectReferenceValue = bagButton;
        if (inventoryRef != null)
            hudSo.FindProperty("inventory").objectReferenceValue = inventoryRef;
        hudSo.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
