using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIInventoryTester : MonoBehaviour
{
    [SerializeField] private UIInventory inventory;

    private int itemCount = 0;

    void Start()
    {
        CreateTestButton();
    }

    private void CreateTestButton()
    {
        // Canvas 찾기
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[UIInventoryTester] Canvas를 찾을 수 없습니다!");
            return;
        }

        // 버튼 오브젝트 생성
        GameObject buttonObj = new GameObject("TestAddItemButton");
        buttonObj.transform.SetParent(canvas.transform, false);

        // RectTransform 설정 (우측 하단)
        RectTransform rt = buttonObj.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(160f, 50f);
        rt.anchorMin = new Vector2(0, 0);
        rt.anchorMax = new Vector2(0, 0);
        rt.pivot = new Vector2(0, 0);
        rt.anchoredPosition = new Vector2(20f, 20f);

        // 배경 이미지
        Image img = buttonObj.AddComponent<Image>();
        img.color = new Color(0.2f, 0.6f, 1f, 1f);

        // 버튼 컴포넌트
        Button btn = buttonObj.AddComponent<Button>();
        btn.onClick.AddListener(OnAddItemClick);

        // 텍스트 추가
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
        itemCount++;
        inventory.AddItem($"테스트 아이템 {itemCount}", "테스트용 아이템입니다.", "소모품");
        Debug.Log($"[Tester] 아이템 추가: 테스트 아이템 {itemCount}");
    }
}
