using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIInventoryTester : MonoBehaviour
{
    // 테스트할 아이템 ID 목록 (JSON에 있는 실제 ID)
    private readonly string[] testItemIds = { "MI-101", "MI-102", "MI-201" };
    private int testIndex = 0;

    void Start()
    {
        CreateTestButton();
    }

    private void CreateTestButton()
    {
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[UIInventoryTester] Canvas를 찾을 수 없습니다!");
            return;
        }

        GameObject buttonObj = new GameObject("TestAddItemButton");
        buttonObj.transform.SetParent(canvas.transform, false);

        RectTransform rt = buttonObj.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(160f, 50f);
        rt.anchorMin = new Vector2(0, 0);
        rt.anchorMax = new Vector2(0, 0);
        rt.pivot = new Vector2(0, 0);
        rt.anchoredPosition = new Vector2(20f, 20f);

        Image img = buttonObj.AddComponent<Image>();
        img.color = new Color(0.2f, 0.6f, 1f, 1f);

        Button btn = buttonObj.AddComponent<Button>();
        btn.onClick.AddListener(OnAddItemClick);

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(buttonObj.transform, false);
        RectTransform textRt = textObj.AddComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.text = "아이템 추가 (테스트)";
        tmp.fontSize = 14;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;

        Debug.Log("[UIInventoryTester] 테스트 버튼 생성 완료.");
    }

    private void OnAddItemClick()
    {
        if (InventoryManager.Instance == null)
        {
            Debug.LogError("[UIInventoryTester] InventoryManager가 없습니다!");
            return;
        }

        string id = testItemIds[testIndex % testItemIds.Length];
        testIndex++;

        InventoryManager.Instance.AddItem(id);
        Debug.Log($"[UIInventoryTester] 아이템 추가 요청: {id}");
    }
}
