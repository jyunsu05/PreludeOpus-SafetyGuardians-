using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 필드에서 아이템을 획득했을 때 HUD에 잠깐 표시되는 비모달 토스트입니다.
/// </summary>
public class UIItemAcquireToast : MonoBehaviour
{
    [SerializeField] private RectTransform panelRoot;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI messageText;

    [Header("--- 타이밍 ---")]
    [SerializeField] private float displayDuration = 2.2f;
    [SerializeField] private float fadeDuration = 0.25f;

    private readonly Queue<string> pendingItemIds = new Queue<string>();
    private Coroutine displayRoutine;

    public static UIItemAcquireToast EnsureInstance(Canvas hostCanvas)
    {
        UIItemAcquireToast existing = FindAnyObjectByType<UIItemAcquireToast>(FindObjectsInactive.Include);
        if (existing != null)
            return existing;

        if (hostCanvas == null)
            hostCanvas = FindAnyObjectByType<Canvas>(FindObjectsInactive.Include);
        if (hostCanvas == null)
            return null;

        GameObject root = new GameObject("UIItemAcquireToast", typeof(RectTransform));
        root.transform.SetParent(hostCanvas.transform, false);
        root.transform.SetAsLastSibling();

        UIItemAcquireToast toast = root.AddComponent<UIItemAcquireToast>();
        toast.BuildDefaultUi();
        return toast;
    }

    private void Awake()
    {
        if (panelRoot == null)
            BuildDefaultUi();
        else
            HideImmediate();
    }

    public void Show(string itemId)
    {
        if (string.IsNullOrEmpty(itemId))
            return;

        pendingItemIds.Enqueue(itemId);
        if (displayRoutine == null)
            displayRoutine = StartCoroutine(ProcessQueue());
    }

    private IEnumerator ProcessQueue()
    {
        while (pendingItemIds.Count > 0)
        {
            string itemId = pendingItemIds.Dequeue();
            ApplyItemPresentation(itemId);
            yield return FadePanel(0f, 1f, fadeDuration);
            yield return new WaitForSecondsRealtime(displayDuration);
            yield return FadePanel(1f, 0f, fadeDuration);
            HideImmediate();
        }

        displayRoutine = null;
    }

    private void ApplyItemPresentation(string itemId)
    {
        string resolvedId = ResolveInventoryItemId(itemId);
        string itemName = resolvedId;
        Sprite itemIcon = null;

        if (DataManager.Instance != null)
        {
            ItemData data = DataManager.Instance.GetItemData(resolvedId);
            if (data != null)
            {
                itemName = data.name;
                itemIcon = GetItemSprite(data);
            }
        }

        if (messageText != null)
            messageText.text = $"{itemName} 획득";

        if (iconImage != null)
        {
            iconImage.sprite = itemIcon;
            iconImage.enabled = itemIcon != null;
        }

        if (panelRoot != null)
            panelRoot.gameObject.SetActive(true);
    }

    private IEnumerator FadePanel(float from, float to, float duration)
    {
        if (canvasGroup == null)
            yield break;

        if (duration <= 0f)
        {
            canvasGroup.alpha = to;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            canvasGroup.alpha = Mathf.Lerp(from, to, t);
            yield return null;
        }

        canvasGroup.alpha = to;
    }

    private void HideImmediate()
    {
        if (canvasGroup != null)
            canvasGroup.alpha = 0f;

        if (panelRoot != null)
            panelRoot.gameObject.SetActive(false);
    }

    private void BuildDefaultUi()
    {
        RectTransform rootRect = transform as RectTransform;
        if (rootRect == null)
            rootRect = gameObject.AddComponent<RectTransform>();

        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        GameObject panelObject = new GameObject("Panel", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        panelObject.transform.SetParent(transform, false);
        panelRoot = panelObject.GetComponent<RectTransform>();
        canvasGroup = panelObject.GetComponent<CanvasGroup>();
        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.color = new Color(0.08f, 0.1f, 0.14f, 0.88f);
        panelImage.raycastTarget = false;

        panelRoot.anchorMin = new Vector2(0.5f, 0f);
        panelRoot.anchorMax = new Vector2(0.5f, 0f);
        panelRoot.pivot = new Vector2(0.5f, 0f);
        panelRoot.anchoredPosition = new Vector2(0f, 130f);
        panelRoot.sizeDelta = new Vector2(500f, 88f);

        HorizontalLayoutGroup layout = panelObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(20, 24, 16, 16);
        layout.spacing = 18f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = false;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(panelRoot, false);
        iconImage = iconObject.GetComponent<Image>();
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;

        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.sizeDelta = new Vector2(58f, 58f);

        LayoutElement iconLayout = iconObject.AddComponent<LayoutElement>();
        iconLayout.preferredWidth = 58f;
        iconLayout.preferredHeight = 58f;

        GameObject textObject = new GameObject("Message", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(panelRoot, false);
        messageText = textObject.GetComponent<TextMeshProUGUI>();
        messageText.font = ResolveSilverFont();
        messageText.fontSize = 30f;
        messageText.color = Color.white;
        messageText.alignment = TextAlignmentOptions.MidlineLeft;
        messageText.raycastTarget = false;
        messageText.enableWordWrapping = false;

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.sizeDelta = new Vector2(360f, 58f);

        LayoutElement textLayout = textObject.AddComponent<LayoutElement>();
        textLayout.flexibleWidth = 1f;
        textLayout.preferredHeight = 58f;

        HideImmediate();
    }

    private static string ResolveInventoryItemId(string itemId)
    {
        if (DataManager.Instance == null)
            return itemId;

        return DataManager.Instance.GetFactoryItemIdForInventory(itemId);
    }

    private static Sprite GetItemSprite(ItemData data)
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

    private static TMP_FontAsset ResolveSilverFont()
    {
        TMP_FontAsset[] fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
        for (int i = 0; i < fonts.Length; i++)
        {
            if (fonts[i] != null && fonts[i].name == "Silver SDF")
                return fonts[i];
        }

        return TMP_Settings.defaultFontAsset;
    }
}
