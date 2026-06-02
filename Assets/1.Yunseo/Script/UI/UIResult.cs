using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIResult : MonoBehaviour
{
    [System.Serializable]
    private class ItemDisplaySlot
    {
        [Tooltip("ItemDisplay_1 같은 슬롯 루트 오브젝트")]
        [SerializeField] private GameObject root;
        [SerializeField] private TextMeshProUGUI text;
        [SerializeField] private Image image;

        public void Setup(string content, Sprite sprite)
        {
            if (root != null)
                root.SetActive(true);

            if (text != null)
                text.text = content;

            if (image != null)
            {
                image.sprite = sprite;
                image.enabled = sprite != null;
            }
        }

        public void Hide()
        {
            if (root != null)
            {
                root.SetActive(false);
                return;
            }

            if (text != null)
                text.text = string.Empty;

            if (image != null)
            {
                image.sprite = null;
                image.enabled = false;
            }
        }
    }

    [Header("--- 결과 문구 (선택) ---")]
    [SerializeField] private TextMeshProUGUI resultText;

    [Header("--- 아이템 표시 슬롯 ---")]
    [SerializeField] private ItemDisplaySlot[] itemDisplays;

    [Header("--- 스테이지 클리어 자동 표시 ---")]
    [SerializeField] private bool showWhenAllMonstersCleared = true;
    [SerializeField] private string stageClearMessage = "공장 정화 완료!";
    [SerializeField] private bool fillItemsFromInventory = true;

    [Header("--- 확인 버튼 → 로딩 ---")]
    [SerializeField] private Button confirmButton;
    [SerializeField] private UILoading loadingUI;
    [SerializeField] private string loadingMessage = "다음 공장으로 이동 중...";
    [SerializeField] private float loadingDuration = 2f;

    private bool stageResultShown;

    private void OnEnable()
    {
        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveListener(OnConfirmButtonClick);
            confirmButton.onClick.AddListener(OnConfirmButtonClick);
        }
    }

    private void OnDisable()
    {
        if (confirmButton != null)
            confirmButton.onClick.RemoveListener(OnConfirmButtonClick);
    }

    private void Start()
    {
        // Scene에서 비활성으로 두는 것이 기본입니다. 실수로 켜져 있으면 시작 시 숨깁니다.
        // Awake에서 끄면 Show() → SetActive(true) 직후 Awake가 다시 꺼버립니다.
        if (Application.isPlaying && !stageResultShown)
            gameObject.SetActive(false);
    }

    public void ResetStageResultState()
    {
        stageResultShown = false;
    }

    public void ShowStageClearResult()
    {
        if (!showWhenAllMonstersCleared || stageResultShown)
            return;

        stageResultShown = true;
        SetupText(stageClearMessage);

        if (fillItemsFromInventory && InventoryManager.Instance != null)
        {
            IReadOnlyList<string> itemIds = InventoryManager.Instance.GetItemIds();
            if (itemIds.Count > 0)
            {
                string[] ids = new string[itemIds.Count];
                for (int i = 0; i < itemIds.Count; i++)
                    ids[i] = itemIds[i];

                SetupItemsById(ids);
            }
            else
            {
                ClearItems();
            }
        }

        Show();
        Debug.Log("[UIResult] 모든 몬스터 제거 - 결과 UI 표시");
    }

    public void Show()
    {
        EnsureOnRootCanvas();
        transform.SetAsLastSibling();
        gameObject.SetActive(true);

        if (!gameObject.activeInHierarchy)
        {
            Debug.LogWarning(
                $"[UIResult] activeInHierarchy=false 입니다. 비활성 부모: {GetFirstInactiveAncestorName() ?? "없음"}");
        }
    }

    private void EnsureOnRootCanvas()
    {
        Canvas rootCanvas = GetComponentInParent<Canvas>(true);
        if (rootCanvas == null)
            rootCanvas = FindAnyObjectByType<Canvas>(FindObjectsInactive.Include);

        if (rootCanvas == null)
            return;

        Transform canvasTransform = rootCanvas.transform;
        if (transform.parent != canvasTransform)
            transform.SetParent(canvasTransform, false);

        if (!rootCanvas.gameObject.activeSelf)
            rootCanvas.gameObject.SetActive(true);
    }

    private string GetFirstInactiveAncestorName()
    {
        Transform current = transform.parent;
        while (current != null)
        {
            if (!current.gameObject.activeSelf)
                return current.name;

            current = current.parent;
        }

        if (!gameObject.activeSelf)
            return gameObject.name;

        return null;
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }

    public void OnConfirmButtonClick()
    {
        Close();

        UILoading loading = ResolveLoadingUI();
        if (loading == null)
        {
            Debug.LogWarning("[UIResult] UILoading을 찾을 수 없습니다.");
            return;
        }

        loading.transform.SetAsLastSibling();
        loading.ShowLoadingWithAutoProgress(loadingMessage, loadingDuration);
        Debug.Log("[UIResult] 확인 버튼 → UILoading 표시");
    }

    private UILoading ResolveLoadingUI()
    {
        if (loadingUI != null)
            return loadingUI;

        return FindAnyObjectByType<UILoading>(FindObjectsInactive.Include);
    }

    public void SetupText(string text)
    {
        if (resultText != null)
            resultText.text = text;
    }

    public void SetupItem(int index, string text, Sprite image = null)
    {
        if (itemDisplays == null || index < 0 || index >= itemDisplays.Length)
        {
            Debug.LogWarning($"[UIResult] itemDisplays[{index}] 범위를 벗어났습니다.");
            return;
        }

        itemDisplays[index].Setup(text, image);
    }

    public void SetupItemById(int index, string itemId)
    {
        string resolvedId = ResolveItemId(itemId);
        ItemData data = DataManager.Instance != null
            ? DataManager.Instance.GetItemData(resolvedId)
            : null;

        string text = data != null ? data.name : resolvedId;
        Sprite image = GetItemSprite(data);
        SetupItem(index, text, image);
    }

    public void SetupItemsById(string[] itemIds)
    {
        ClearItems();

        if (itemDisplays == null || itemDisplays.Length == 0 || itemIds == null)
            return;

        int count = Mathf.Min(itemDisplays.Length, itemIds.Length);
        for (int i = 0; i < count; i++)
            SetupItemById(i, itemIds[i]);

        for (int i = count; i < itemDisplays.Length; i++)
            itemDisplays[i].Hide();
    }

    public void SetupItems(string[] texts, Sprite[] images)
    {
        ClearItems();

        if (itemDisplays == null || itemDisplays.Length == 0)
            return;

        int count = itemDisplays.Length;
        if (texts != null)
            count = Mathf.Min(count, texts.Length);
        if (images != null)
            count = Mathf.Min(count, images.Length);

        for (int i = 0; i < count; i++)
        {
            string text = texts != null ? texts[i] : string.Empty;
            Sprite image = images != null ? images[i] : null;
            itemDisplays[i].Setup(text, image);
        }

        for (int i = count; i < itemDisplays.Length; i++)
            itemDisplays[i].Hide();
    }

    public void ClearItems()
    {
        if (itemDisplays == null)
            return;

        for (int i = 0; i < itemDisplays.Length; i++)
            itemDisplays[i].Hide();
    }

    private string ResolveItemId(string itemId)
    {
        if (DataManager.Instance == null)
            return itemId;

        return DataManager.Instance.GetFactoryItemIdForInventory(itemId);
    }

    private Sprite GetItemSprite(ItemData data)
    {
        if (AtlasManager.Instance == null || data == null)
            return null;

        if (!string.IsNullOrEmpty(data.image_key))
        {
            Sprite sprite = AtlasManager.Instance.GetSprite(data.image_key);
            if (sprite != null)
                return sprite;
        }

        if (!string.IsNullOrEmpty(data.id))
        {
            Sprite sprite = AtlasManager.Instance.GetSprite(data.id);
            if (sprite != null)
                return sprite;
        }

        if (!string.IsNullOrEmpty(data.name))
        {
            Sprite sprite = AtlasManager.Instance.GetSprite(data.name);
            if (sprite != null)
                return sprite;
        }

        return null;
    }
}
