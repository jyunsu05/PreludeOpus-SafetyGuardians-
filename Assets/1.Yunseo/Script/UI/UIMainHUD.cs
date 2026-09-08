using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIMainHUD : MonoBehaviour
{
    [Header("--- HUD 상시 자식 컴포넌트들 ---")]
    [SerializeField] private Button bagButton;        // 가방 버튼 (UI_Bag_Button)
    [SerializeField] private Slider oxygenBarSlider;  // 산소 게이지 바 (UI_OxygenBar_Slider)
    [SerializeField] private TextMeshProUGUI oxygenValueText;
    [SerializeField] private TextMeshProUGUI pollutionValueText;

    [Header("--- target 진행도 텍스트 ---")]
    [SerializeField] private TextMeshProUGUI currentChapterText;
    [SerializeField] private TextMeshProUGUI currentPurificationText;
    [SerializeField] private TextMeshProUGUI currentItemText;

    [Header("--- 인벤토리 ---")]
    [SerializeField] private UIInventory inventory;

    private bool isBattleEventBound;
    private bool chapterSubscribed;
    private bool pollutionSubscribed;
    private bool inventorySubscribed;
    private bool targetProgressBound;
    private int targetProgressResolveAttempts;
    private Coroutine bagPulseRoutine;

    private void Awake()
    {
        TryBindBattleEvents();
        TryResolveGaugeTextReferences();
        TryResolveTargetProgressTextReferences();
    }

    private void OnEnable()
    {
        targetProgressBound = false;
        targetProgressResolveAttempts = 0;
        TryBindBattleEvents();
        TrySubscribeProgressSources();
        RefreshTargetProgressTexts();
    }

    void Start()
    {
        TryBindBattleEvents();
        TrySubscribeProgressSources();
        RefreshTargetProgressTexts();

        if (bagButton != null)
        {
            bagButton.onClick.AddListener(OnBagButtonClick);
        }
        else
        {
            Debug.LogWarning("[UI_MainHUD] bagButton 슬롯이 비어 있습니다! 하이어라키에서 연결해 주세요.");
        }
    }

    private void Update()
    {
        if (!isBattleEventBound)
            TryBindBattleEvents();

        if (!chapterSubscribed || !pollutionSubscribed || !inventorySubscribed)
        {
            bool beforeChapter = chapterSubscribed;
            bool beforePollution = pollutionSubscribed;
            bool beforeInventory = inventorySubscribed;
            TrySubscribeProgressSources();
            if (chapterSubscribed != beforeChapter
                || pollutionSubscribed != beforePollution
                || inventorySubscribed != beforeInventory)
            {
                RefreshTargetProgressTexts();
            }
        }

        // 챕터 매니저/스포너가 늦게 준비되거나 참조가 늦게 잡히는 경우 대비
        if (!targetProgressBound && targetProgressResolveAttempts < 120)
            RefreshTargetProgressTexts();
    }

    private void OnDestroy()
    {
        UnsubscribeProgressSources();

        if (GameManager.Instance != null)
            GameManager.Instance.OnBattleEnded -= HandleBattleEnded;

        isBattleEventBound = false;
    }

    private void TryBindBattleEvents()
    {
        if (GameManager.Instance == null)
            return;

        GameManager.Instance.OnBattleEnded -= HandleBattleEnded;
        GameManager.Instance.OnBattleEnded += HandleBattleEnded;
        isBattleEventBound = true;
    }

    private void HandleBattleEnded()
    {
        gameObject.SetActive(true);
        RefreshTargetProgressTexts();
    }

    public Slider GetOxygenBarSlider() => oxygenBarSlider;

    public void UpdateOxygenGauge(float currentOxygen, float maxOxygen)
    {
        if (oxygenBarSlider != null)
        {
            oxygenBarSlider.maxValue = maxOxygen;
            oxygenBarSlider.value = currentOxygen;
        }

        if (oxygenValueText != null)
            oxygenValueText.text = FormatRemainingGaugeText(currentOxygen, maxOxygen);
    }

    public void UpdatePollutionGauge(float currentPollution, float maxPollution)
    {
        if (pollutionValueText != null)
            pollutionValueText.text = FormatRemainingGaugeText(currentPollution, maxPollution);
    }

    public void RefreshTargetProgressTexts()
    {
        TryResolveTargetProgressTextReferences();

        if (currentChapterText != null)
            currentChapterText.SetText("현재 챕터 : {0}", ResolveCurrentChapterIndex());

        if (currentPurificationText != null)
        {
            ResolveMonsterPurificationProgress(out int purified, out int total);
            currentPurificationText.SetText("현재 몬스터 정화 : {0}/{1}", purified, total);
        }

        if (currentItemText != null)
        {
            ResolveFactoryItemProgress(out int acquired, out int max);
            currentItemText.SetText("현재 공장 정화 아이템 갯수 : {0}/{1}", acquired, max);
        }

        targetProgressBound = currentChapterText != null
            && currentPurificationText != null
            && currentItemText != null;

        if (!targetProgressBound)
            targetProgressResolveAttempts++;
    }

    public static void RefreshTargetProgressGlobal()
    {
        UIMainHUD[] huds = FindObjectsByType<UIMainHUD>(FindObjectsInactive.Include);
        for (int i = 0; i < huds.Length; i++)
        {
            if (huds[i] != null)
                huds[i].RefreshTargetProgressTexts();
        }
    }

    public static void PlayBagAcquirePulseGlobal()
    {
        UIMainHUD[] huds = FindObjectsByType<UIMainHUD>(FindObjectsInactive.Include);
        for (int i = 0; i < huds.Length; i++)
        {
            if (huds[i] != null)
                huds[i].PlayBagAcquirePulse();
        }
    }

    public void PlayBagAcquirePulse()
    {
        if (bagButton == null)
            return;

        if (bagPulseRoutine != null)
            StopCoroutine(bagPulseRoutine);

        bagPulseRoutine = StartCoroutine(BagAcquirePulseRoutine());
    }

    private IEnumerator BagAcquirePulseRoutine()
    {
        Transform buttonTransform = bagButton.transform;
        Vector3 originalScale = buttonTransform.localScale;
        Vector3 enlargedScale = originalScale * 1.12f;
        const float halfDuration = 0.18f;

        float elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            buttonTransform.localScale = Vector3.Lerp(originalScale, enlargedScale, elapsed / halfDuration);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            buttonTransform.localScale = Vector3.Lerp(enlargedScale, originalScale, elapsed / halfDuration);
            yield return null;
        }

        buttonTransform.localScale = originalScale;
        bagPulseRoutine = null;
    }

    private void TryResolveGaugeTextReferences()
    {
        if (oxygenValueText == null)
            oxygenValueText = FindGaugeTextUnderBar("OxygenBar");

        if (pollutionValueText == null)
            pollutionValueText = FindGaugeTextUnderBar("PollutionBar");
    }

    private void TryResolveTargetProgressTextReferences()
    {
        if (currentChapterText != null
            && currentPurificationText != null
            && currentItemText != null)
            return;

        Transform searchRoot = transform.Find("target");
        if (searchRoot == null)
            searchRoot = transform;

        TextMeshProUGUI[] texts = searchRoot.GetComponentsInChildren<TextMeshProUGUI>(true);

        if (currentChapterText == null)
        {
            currentChapterText = FindProgressText(
                texts, "current chapter", "현재 챕터");
        }

        if (currentPurificationText == null)
        {
            currentPurificationText = FindProgressText(
                texts, "current purification", "현재 몬스터 정화");
        }

        if (currentItemText == null)
        {
            currentItemText = FindProgressText(
                texts, "current item", "현재 공장 정화");
        }
    }

    private TextMeshProUGUI FindGaugeTextUnderBar(string barObjectName)
    {
        Transform barRoot = transform.Find(barObjectName);
        return barRoot != null
            ? barRoot.GetComponentInChildren<TextMeshProUGUI>(true)
            : null;
    }

    private static TextMeshProUGUI FindProgressText(
        TextMeshProUGUI[] texts,
        string objectName,
        string textHint)
    {
        if (texts == null)
            return null;

        for (int i = 0; i < texts.Length; i++)
        {
            TextMeshProUGUI text = texts[i];
            if (text == null)
                continue;

            if (text.gameObject.name.Equals(objectName, StringComparison.OrdinalIgnoreCase))
                return text;
        }

        if (string.IsNullOrEmpty(textHint))
            return null;

        for (int i = 0; i < texts.Length; i++)
        {
            TextMeshProUGUI text = texts[i];
            if (text == null || string.IsNullOrEmpty(text.text))
                continue;

            if (text.text.IndexOf(textHint, StringComparison.Ordinal) >= 0)
                return text;
        }

        return null;
    }

    private static string FormatRemainingGaugeText(float current, float max)
        => $"{Mathf.CeilToInt(current)}/{Mathf.CeilToInt(max)}";

    private static int ResolveCurrentChapterIndex()
    {
        if (ChapterManager.Instance != null)
            return Mathf.Max(1, ChapterManager.Instance.CurrentChapterIndex);

        FactoryChapterController factoryChapter = FactoryChapterController.EnsureInstance();
        if (factoryChapter != null)
            return Mathf.Max(1, factoryChapter.CurrentChapter);

        return 1;
    }

    private static void ResolveMonsterPurificationProgress(out int purified, out int total)
    {
        purified = 0;
        total = 0;

        PollutionManager manager = PollutionManager.EnsureInstance();
        if (manager == null)
            return;

        purified = manager.PurifiedMonstersThisChapter;
        total = Mathf.Max(manager.TotalMonstersThisChapter, purified);
    }

    private static void ResolveFactoryItemProgress(out int acquired, out int max)
    {
        acquired = 0;
        max = 0;

        if (InventoryManager.Instance != null)
            acquired = InventoryManager.Instance.GetFactoryPurificationItemCount();

        if (ItemSpawner.TryGetChapterFactoryItemProgress(out _, out int spawnMax) && spawnMax > 0)
        {
            max = Mathf.Max(spawnMax, acquired);
            return;
        }

        PollutionManager manager = PollutionManager.EnsureInstance();
        if (manager != null)
            max = Mathf.Max(manager.TotalMonstersThisChapter, acquired);
        else
            max = acquired;
    }

    private void TrySubscribeProgressSources()
    {
        if (!chapterSubscribed && ChapterManager.Instance != null)
        {
            ChapterManager.Instance.OnChapterLoaded -= HandleChapterLoaded;
            ChapterManager.Instance.OnChapterLoaded += HandleChapterLoaded;
            chapterSubscribed = true;
        }

        if (!pollutionSubscribed)
        {
            PollutionManager manager = PollutionManager.EnsureInstance();
            if (manager != null)
            {
                manager.OnPollutionChanged -= HandlePollutionChanged;
                manager.OnPollutionChanged += HandlePollutionChanged;
                pollutionSubscribed = true;
            }
        }

        if (!inventorySubscribed && InventoryManager.Instance != null)
        {
            InventoryManager.Instance.OnInventoryChanged -= HandleInventoryChanged;
            InventoryManager.Instance.OnInventoryChanged += HandleInventoryChanged;
            inventorySubscribed = true;
        }
    }

    private void UnsubscribeProgressSources()
    {
        if (chapterSubscribed && ChapterManager.Instance != null)
            ChapterManager.Instance.OnChapterLoaded -= HandleChapterLoaded;

        if (pollutionSubscribed && PollutionManager.Instance != null)
            PollutionManager.Instance.OnPollutionChanged -= HandlePollutionChanged;

        if (inventorySubscribed && InventoryManager.Instance != null)
            InventoryManager.Instance.OnInventoryChanged -= HandleInventoryChanged;

        chapterSubscribed = false;
        pollutionSubscribed = false;
        inventorySubscribed = false;
    }

    private void HandleChapterLoaded(ChapterLoadedEventArgs args)
    {
        RefreshTargetProgressTexts();
    }

    private void HandlePollutionChanged(float currentPollution, float maxPollution)
    {
        RefreshTargetProgressTexts();
    }

    private void HandleInventoryChanged()
    {
        RefreshTargetProgressTexts();
    }

    /// <summary>PlayerOxygen 등에서 HUD 산소 슬라이더만 안전하게 갱신합니다.</summary>
    public static bool TryUpdateOxygenGaugeGlobal(float currentOxygen, float maxOxygen)
    {
        UIMainHUD[] huds = FindObjectsByType<UIMainHUD>(FindObjectsInactive.Include);
        bool updated = false;

        for (int i = 0; i < huds.Length; i++)
        {
            UIMainHUD hud = huds[i];
            if (hud == null || hud.GetOxygenBarSlider() == null)
                continue;

            hud.UpdateOxygenGauge(currentOxygen, maxOxygen);
            updated = true;
        }

        return updated;
    }

    private void OnBagButtonClick()
    {
        Debug.Log("<color=cyan>[UI_MainHUD]</color> HUD 내부의 가방 버튼이 클릭되었습니다! 인벤토리 개방 프로토콜 가동.");

        if (inventory != null)
            inventory.Open();
    }
}
