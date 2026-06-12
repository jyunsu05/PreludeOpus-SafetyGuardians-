using System;
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

    private void Awake()
    {
        TryBindBattleEvents();
        TryResolveGaugeTextReferences();
        TryResolveTargetProgressTextReferences();
    }

    private void OnEnable()
    {
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
            TrySubscribeProgressSources();
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
        if (currentChapterText != null)
            currentChapterText.text = $"현재 챕터 : {ResolveCurrentChapterIndex()}";

        if (currentPurificationText != null)
        {
            ResolveMonsterPurificationProgress(out int purified, out int total);
            currentPurificationText.text = $"현재 몬스터 정화 : {purified}/{total}";
        }

        if (currentItemText != null)
        {
            ResolveFactoryItemProgress(out int acquired, out int max);
            currentItemText.text = $"현재 공장 정화 아이템 갯수 : {acquired}/{max}";
        }
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

    private void TryResolveGaugeTextReferences()
    {
        if (oxygenValueText == null)
            oxygenValueText = FindGaugeTextUnderBar("OxygenBar");

        if (pollutionValueText == null)
            pollutionValueText = FindGaugeTextUnderBar("PollutionBar");
    }

    private void TryResolveTargetProgressTextReferences()
    {
        Transform targetRoot = transform.Find("target");
        if (targetRoot == null)
            return;

        if (currentChapterText == null)
            currentChapterText = FindChildTextByName(targetRoot, "current chapter");

        if (currentPurificationText == null)
            currentPurificationText = FindChildTextByName(targetRoot, "current purification");

        if (currentItemText == null)
            currentItemText = FindChildTextByName(targetRoot, "current item");
    }

    private TextMeshProUGUI FindGaugeTextUnderBar(string barObjectName)
    {
        Transform barRoot = transform.Find(barObjectName);
        return barRoot != null
            ? barRoot.GetComponentInChildren<TextMeshProUGUI>(true)
            : null;
    }

    private static TextMeshProUGUI FindChildTextByName(Transform root, string objectName)
    {
        if (root == null)
            return null;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (!child.name.Equals(objectName, StringComparison.OrdinalIgnoreCase))
                continue;

            TextMeshProUGUI text = child.GetComponent<TextMeshProUGUI>();
            if (text != null)
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
        if (!ItemSpawner.TryGetChapterFactoryItemProgress(out acquired, out max))
        {
            acquired = 0;
            max = 0;
        }
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
