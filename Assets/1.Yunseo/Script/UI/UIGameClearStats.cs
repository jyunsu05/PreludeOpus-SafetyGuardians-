using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.U2D;
using TMPro;

/// <summary>
/// 공장 3 클리어 성과 UI — 메인(S~D) + 상세(그래프 / 2열 / 아이템 3-page).
/// </summary>
public class UIGameClearStats : MonoBehaviour
{
    public static bool IsVisible { get; private set; }

    private const int DetailPageGraph = 0;
    private const int DetailPageStats = 1;
    private const int DetailPageItems = 2;
    private const int ChapterBarCount = 3;

    private static readonly string[] GradeSpriteNames =
    {
        "ClearIcon_0",
        "ClearIcon_1",
        "ClearIcon_2",
        "ClearIcon_3",
        "ClearIcon_4",
    };

    [Header("--- Panels ---")]
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject detailPanel;

    [Header("--- Main Panel ---")]
    [SerializeField] private Image gradeIcon;
    [SerializeField] private TextMeshProUGUI clearText;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button fromBeginningButton;
    [SerializeField] private Button viewDetailsButton;

    [Header("--- Detail Pages ---")]
    [SerializeField] private GameObject pageGraph;
    [SerializeField] private GameObject pageDetail;
    [SerializeField] private Button btnPrev;
    [SerializeField] private Button btnNext;
    [SerializeField] private Button backButton;

    [Header("--- Chapter Graph (Page 1) ---")]
    [SerializeField] private ChapterBarRowBinding[] chapterBars = new ChapterBarRowBinding[ChapterBarCount];

    [Header("--- Clear Run Column (Page 2, Left) ---")]
    [SerializeField] private Image clearRunGradeIcon;
    [SerializeField] private TextMeshProUGUI clearRunGradeScoreText;
    [SerializeField] private TextMeshProUGUI clearRunPurifiedText;
    [SerializeField] private TextMeshProUGUI clearRunEscapeText;
    [SerializeField] private Slider clearRunOxygenSlider;
    [SerializeField] private Image clearRunOxygenFill;
    [SerializeField] private TextMeshProUGUI clearRunOxygenPercentText;

    [Header("--- Journey Column (Page 2, Right) ---")]
    [SerializeField] private FactoryDotToggleBinding[] journeyFactoryDotToggles =
        new FactoryDotToggleBinding[ChapterBarCount];
    [SerializeField] private TextMeshProUGUI journeyPurifiedText;
    [SerializeField] private TextMeshProUGUI journeyEscapeText;
    [SerializeField] private TextMeshProUGUI journeyPlayTimeText;
    [SerializeField] private TextMeshProUGUI journeyCrisisText;

    [Header("--- Item List (Page 3) ---")]
    [SerializeField] private GameObject itemListRoot;
    [SerializeField] private TextMeshProUGUI itemEmptyText;
    [SerializeField] private ItemListRowBinding[] itemListRows;

    [Header("--- Grade Icon (ClearIcon Atlas) ---")]
    [SerializeField] private SpriteAtlas clearIconAtlas;

    [Header("--- Options ---")]
    [SerializeField] private bool showScoreInSummary = true;

    private bool hasShownMainPanel;
    private int detailPageIndex = DetailPageGraph;

    private void Awake()
    {
        IsVisible = false;
        ResolveReferences();
        WireButtons();
        SetDetailPage(DetailPageGraph);
        ShowMainPanelOnly();
    }

    private void OnDisable()
    {
        IsVisible = false;
    }

    private void Start()
    {
        if (!hasShownMainPanel)
            gameObject.SetActive(false);
    }

    public void ResetShowState()
    {
        hasShownMainPanel = false;
        IsVisible = false;
        SetDetailPage(DetailPageGraph);
    }

    public void ShowMain()
    {
        SaveSnapshotIfNeeded();
        BindClearRunPresentation();
        ShowMainPanelOnly();
        Show();
    }

    public void Show()
    {
        EnsureOnRootCanvas();
        transform.SetAsLastSibling();
        hasShownMainPanel = true;
        IsVisible = true;
        gameObject.SetActive(true);
    }

    public void Close()
    {
        IsVisible = false;
        gameObject.SetActive(false);
    }

    public void OnCloseClick()
    {
        ReturnToGameStartScreen();
    }

    public void OnFromBeginningClick()
    {
        Close();
        PlaySessionStats.Instance?.ResetAll();

        if (GameManager.Instance != null)
            GameManager.Instance.LoadOpeningScene();
    }

    public void OnViewDetailsClick()
    {
        if (detailPanel == null)
        {
            Debug.LogWarning("[UIGameClearStats] DetailPanel이 없습니다.");
            return;
        }

        BindDetailPresentation();
        SetDetailPage(DetailPageGraph);
        ShowDetailPanelOnly();
    }

    public void OnBackFromDetailClick()
    {
        ReturnToGameStartScreen();
    }

    private void ReturnToGameStartScreen()
    {
        Close();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.LoadGameStartScreen();
            return;
        }

        const string gameStartScreenSceneName = "Game start screen";
        if (!Application.CanStreamedLevelBeLoaded(gameStartScreenSceneName))
        {
            Debug.LogError(
                $"[UIGameClearStats] '{gameStartScreenSceneName}' 씬을 로드할 수 없습니다. " +
                "Build Settings에 씬이 포함되어 있는지 확인하세요.");
            return;
        }

        UnityEngine.SceneManagement.SceneManager.LoadScene(gameStartScreenSceneName);
    }

    public void OnDetailPrevClick()
    {
        if (detailPageIndex <= DetailPageGraph)
            return;

        SetDetailPage(detailPageIndex - 1);
    }

    public void OnDetailNextClick()
    {
        if (detailPageIndex >= DetailPageItems)
            return;

        SetDetailPage(detailPageIndex + 1);
    }

    private void ShowMainPanelOnly()
    {
        if (mainPanel != null)
            mainPanel.SetActive(true);

        if (detailPanel != null)
            detailPanel.SetActive(false);
    }

    private void ShowDetailPanelOnly()
    {
        if (mainPanel != null)
            mainPanel.SetActive(false);

        if (detailPanel != null)
            detailPanel.SetActive(true);

        EnsureDetailNavigationVisible();
    }

    private void SetDetailPage(int pageIndex)
    {
        detailPageIndex = Mathf.Clamp(pageIndex, DetailPageGraph, DetailPageItems);
        bool showGraph = detailPageIndex == DetailPageGraph;
        bool showStats = detailPageIndex == DetailPageStats;
        bool showItems = detailPageIndex == DetailPageItems;

        if (pageGraph != null)
            pageGraph.SetActive(showGraph);

        if (pageDetail != null)
            pageDetail.SetActive(showStats);

        if (itemListRoot != null)
            itemListRoot.SetActive(showItems);

        SetDetailContentRaycastPassThrough(pageGraph, showGraph);
        SetDetailContentRaycastPassThrough(pageDetail, showStats);
        SetDetailContentRaycastPassThrough(itemListRoot, showItems);

        EnsureDetailNavigationVisible();
        RefreshDetailNavigationButtons();
    }

    private static void SetDetailContentRaycastPassThrough(GameObject pageRoot, bool isVisible)
    {
        if (pageRoot == null || !isVisible)
            return;

        Graphic[] graphics = pageRoot.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            if (graphics[i] is TextMeshProUGUI)
                continue;

            graphics[i].raycastTarget = false;
        }
    }

    private void EnsureDetailNavigationVisible()
    {
        Transform navRoot = GetDetailNavigationRoot();
        if (navRoot != null)
        {
            navRoot.gameObject.SetActive(true);
            navRoot.SetAsLastSibling();
        }

        SetNavigationButtonActive(btnPrev, true);
        SetNavigationButtonActive(btnNext, true);
        SetNavigationButtonActive(backButton, true);
    }

    private void RefreshDetailNavigationButtons()
    {
        if (btnPrev != null)
            btnPrev.interactable = detailPageIndex > DetailPageGraph;

        if (btnNext != null)
            btnNext.interactable = detailPageIndex < DetailPageItems;

        if (backButton != null)
            backButton.interactable = true;
    }

    private Transform GetDetailNavigationRoot()
    {
        if (btnPrev != null)
            return btnPrev.transform.parent;

        if (btnNext != null)
            return btnNext.transform.parent;

        if (backButton != null)
            return backButton.transform.parent;

        return null;
    }

    private static void SetNavigationButtonActive(Button button, bool isActive)
    {
        if (button != null)
            button.gameObject.SetActive(isActive);
    }

    private void SaveSnapshotIfNeeded()
    {
        PlaySessionStats stats = PlaySessionStats.EnsureInstance();
        ChapterManager chapterManager = ChapterManager.Instance;
        if (stats == null || chapterManager == null)
            return;

        stats.SaveSnapshotForCurrentChapter(chapterManager.CurrentChapterIndex);
    }

    private void BindClearRunPresentation()
    {
        PlaySessionStats stats = PlaySessionStats.EnsureInstance();
        if (stats == null)
        {
            ApplyGradeIcon(gradeIcon, SessionGrade.D);
            if (clearText != null)
                clearText.text = "버틴 수호자";
            return;
        }

        ApplyGradeIcon(gradeIcon, stats.GetMainGrade());

        if (clearText == null)
            return;

        StatBlock clearRun = stats.ClearRun;
        string summary = $"도망 {clearRun.escapeCount}회 · 산소 {clearRun.finalOxygenPercent:F0}%";
        if (showScoreInSummary)
            summary = $"{stats.GetMainScore()}점 · {summary}";

        clearText.text = $"{stats.GetMainTitle()}\n{summary}";
    }

    private void BindDetailPresentation()
    {
        PlaySessionStats stats = PlaySessionStats.EnsureInstance();
        if (stats == null)
            return;

        EnsureDetailBindings();
        BindChapterGraph(stats);
        BindClearRunColumn(stats);
        BindJourneyColumn(stats);
        BindItemList(stats);
    }

    private void EnsureDetailBindings()
    {
        if (detailPanel == null)
            return;

        Transform detailRoot = detailPanel.transform;
        Transform statsPage = pageDetail != null ? pageDetail.transform : detailRoot;

        Transform clearRunColumn = FindDeepChild(statsPage, "FinalClear", "ClearRunColumn");
        if (clearRunColumn != null)
        {
            clearRunPurifiedText ??= FindStatValueText(clearRunColumn, "purification");
            clearRunEscapeText ??= FindStatValueText(clearRunColumn, "escape");
            clearRunOxygenPercentText ??= FindStatValueText(clearRunColumn, "oxygen");
        }

        Transform journeyColumn = FindDeepChild(statsPage, "ThisJourney", "JourneyColumn");
        if (journeyColumn != null)
        {
            journeyPurifiedText ??= FindStatValueText(journeyColumn, "purification");
            journeyPlayTimeText ??= FindStatValueText(journeyColumn, "activity", "playtime");
            journeyCrisisText ??= FindStatValueText(journeyColumn, "crisis", "gameover");
            EnsureJourneyEscapeText(journeyColumn);
        }
    }

    private void BindChapterGraph(PlaySessionStats stats)
    {
        if (chapterBars == null || chapterBars.Length == 0)
            return;

        for (int i = 0; i < chapterBars.Length; i++)
        {
            int chapterIndex = i + 1;
            ChapterSnapshot? snapshot = stats.GetChapterSnapshot(chapterIndex);
            chapterBars[i].Bind(snapshot, clearIconAtlas);
        }
    }

    private void BindClearRunColumn(PlaySessionStats stats)
    {
        StatBlock clearRun = stats.ClearRun;
        SessionGrade grade = stats.GetMainGrade();

        ApplyGradeIcon(clearRunGradeIcon, grade);
        string scoreLine = clearRunGradeIcon != null
            ? $"{stats.GetMainScore()}점"
            : $"{grade}  {stats.GetMainScore()}점";
        SetText(clearRunGradeScoreText, scoreLine);
        SetText(clearRunPurifiedText, $"정화  {clearRun.purifiedMonsters}마리");
        SetText(clearRunEscapeText, $"도망  {clearRun.escapeCount}회");

        float oxygen = clearRun.finalOxygenPercent;
        if (clearRunOxygenSlider != null)
        {
            clearRunOxygenSlider.minValue = 0f;
            clearRunOxygenSlider.maxValue = 100f;
            clearRunOxygenSlider.value = oxygen;
        }

        if (clearRunOxygenFill != null)
            clearRunOxygenFill.fillAmount = oxygen / 100f;

        SetText(clearRunOxygenPercentText, $"산소  {oxygen:F0}%");
    }

    private void BindJourneyColumn(PlaySessionStats stats)
    {
        if (detailPanel != null)
            EnsureJourneyEscapeText(FindDeepChild(detailPanel.transform, "ThisJourney", "JourneyColumn"));

        StatBlock sessionTotal = stats.SessionTotal;

        BindFactoryDots(stats.ClearedFactoryCount);
        SetText(journeyPurifiedText, $"정화  {sessionTotal.purifiedMonsters}마리");
        SetText(journeyEscapeText, $"도망  {sessionTotal.escapeCount}회");
        SetText(journeyPlayTimeText, $"활동  {FormatPlayTimeMinutes(sessionTotal.playTimeSeconds)}");
        SetText(journeyCrisisText, $"위기  {stats.GameOverCount}회");
    }

    private void BindItemList(PlaySessionStats stats)
    {
        List<InventoryManager.StackedInventoryItem> items =
            AggregateItemsById(stats.SessionAcquiredItems);
        bool hasItems = items.Count > 0;

        if (itemEmptyText != null)
        {
            itemEmptyText.gameObject.SetActive(!hasItems);
            if (!hasItems)
                itemEmptyText.text = "획득한 아이템이 없습니다.";
        }

        if (!HasAnyItemListRow())
            return;

        if (!hasItems)
        {
            itemListRows[0].BindEmpty("획득한 아이템이 없습니다.");
            for (int i = 1; i < itemListRows.Length; i++)
                itemListRows[i].Hide();
            return;
        }

        int displayCount = Mathf.Min(itemListRows.Length, items.Count);
        for (int i = 0; i < displayCount; i++)
            itemListRows[i].Bind(items[i].itemId, items[i].count);

        for (int i = displayCount; i < itemListRows.Length; i++)
            itemListRows[i].Hide();
    }

    private static List<InventoryManager.StackedInventoryItem> AggregateItemsById(
        IReadOnlyList<InventoryManager.StackedInventoryItem> items)
    {
        var aggregated = new List<InventoryManager.StackedInventoryItem>();
        if (items == null || items.Count == 0)
            return aggregated;

        var countsById = new Dictionary<string, int>();
        var orderedIds = new List<string>();

        for (int i = 0; i < items.Count; i++)
        {
            string itemId = ResolveItemId(items[i].itemId);
            int count = items[i].count;
            if (string.IsNullOrEmpty(itemId) || count <= 0)
                continue;

            if (countsById.ContainsKey(itemId))
            {
                countsById[itemId] += count;
                continue;
            }

            countsById[itemId] = count;
            orderedIds.Add(itemId);
        }

        for (int i = 0; i < orderedIds.Count; i++)
        {
            string itemId = orderedIds[i];
            aggregated.Add(new InventoryManager.StackedInventoryItem(itemId, countsById[itemId]));
        }

        return aggregated;
    }

    private void BindFactoryDots(int clearedFactoryCount)
    {
        if (!HasFactoryDotToggles())
            return;

        for (int i = 0; i < journeyFactoryDotToggles.Length; i++)
            journeyFactoryDotToggles[i].Bind(i + 1 <= clearedFactoryCount);
    }

    private void ApplyGradeIcon(Image target, SessionGrade grade)
    {
        if (target == null)
            return;

        int index = (int)grade;
        Sprite sprite = null;
        if (clearIconAtlas != null && index >= 0 && index < GradeSpriteNames.Length)
            sprite = clearIconAtlas.GetSprite(GradeSpriteNames[index]);

        target.sprite = sprite;
        target.enabled = sprite != null;
        target.preserveAspect = true;
    }

    private bool HasFactoryDotToggles()
    {
        if (journeyFactoryDotToggles == null || journeyFactoryDotToggles.Length == 0)
            return false;

        for (int i = 0; i < journeyFactoryDotToggles.Length; i++)
        {
            if (journeyFactoryDotToggles[i].IsValid)
                return true;
        }

        return false;
    }

    private bool HasAnyItemListRow()
    {
        if (itemListRows == null || itemListRows.Length == 0)
            return false;

        for (int i = 0; i < itemListRows.Length; i++)
        {
            if (itemListRows[i].HasReference)
                return true;
        }

        return false;
    }

    private void HideAllItemListRows()
    {
        if (itemListRows == null)
            return;

        for (int i = 0; i < itemListRows.Length; i++)
            itemListRows[i].Hide();
    }

    private void ResolveReferences()
    {
        if (mainPanel == null)
            mainPanel = transform.Find("MainPanel")?.gameObject;

        if (detailPanel == null)
            detailPanel = transform.Find("DetailPanel")?.gameObject;

        Transform mainRoot = mainPanel != null ? mainPanel.transform : transform;

        gradeIcon ??= FindDeep(mainRoot, "ClearIcon")?.GetComponent<Image>();
        clearText ??= FindDeep(mainRoot, "ClearText")?.GetComponent<TextMeshProUGUI>();

        Transform buttonRoot = FindDeep(mainRoot, "UIButtonContainer");
        if (buttonRoot != null)
        {
            closeButton ??= FindDeep(buttonRoot, "CloseButton")?.GetComponent<Button>();
            fromBeginningButton ??= FindDeep(buttonRoot, "FromTheBeginningButton")?.GetComponent<Button>();
            viewDetailsButton ??= FindDeep(buttonRoot, "ViewDetailsButton")?.GetComponent<Button>();
        }

        if (detailPanel == null)
            return;

        Transform detailRoot = detailPanel.transform;

        if (pageGraph == null || pageGraph == detailPanel)
            pageGraph = FindDeepChild(detailRoot, "ChapterBarGraph", "PageGraph")?.gameObject;

        pageDetail ??= FindDeepChild(detailRoot, "2-column statistics", "PageDetail")?.gameObject;

        btnPrev ??= FindButton(detailRoot, "LeftButton", "BtnPrev", "PrevButton");
        btnNext ??= FindButton(detailRoot, "RightButton", "BtnNext", "NextButton");
        backButton ??= FindButton(detailRoot, "BackButton", "BtnBack", "CloseButton");

        ResolveChapterBars(detailRoot);
        ResolveStatColumns(detailRoot);
        ResolveItemList(detailRoot);
    }

    private void ResolveChapterBars(Transform detailRoot)
    {
        if (HasAnyChapterBarBinding())
            return;

        Transform graphRoot = FindDeepChild(detailRoot, "ChapterBarGraph", "PageGraph");
        if (graphRoot == null)
            return;

        var rows = new List<ChapterBarRowBinding>();
        foreach (Transform child in graphRoot)
        {
            if (!IsChapterBarRow(child.name))
                continue;

            rows.Add(ChapterBarRowBinding.FromTransform(child));
        }

        rows.Sort((a, b) => string.CompareOrdinal(a.SortName, b.SortName));
        if (rows.Count > 0)
            chapterBars = rows.ToArray();
    }

    private static bool IsChapterBarRow(string objectName)
    {
        return objectName.Contains("ChapterBar")
               || objectName.StartsWith("Chapter", StringComparison.Ordinal)
                   && objectName.Length <= 8;
    }

    private bool HasAnyChapterBarBinding()
    {
        if (chapterBars == null || chapterBars.Length == 0)
            return false;

        for (int i = 0; i < chapterBars.Length; i++)
        {
            if (chapterBars[i].HasAnyReference())
                return true;
        }

        return false;
    }

    private void ResolveStatColumns(Transform detailRoot)
    {
        Transform detailPageRoot = pageDetail != null ? pageDetail.transform : detailRoot;

        Transform clearRunColumn = FindDeepChild(detailPageRoot, "FinalClear", "ClearRunColumn");
        if (clearRunColumn != null)
        {
            clearRunGradeIcon ??= FindImage(clearRunColumn, "GradeIcon", "ClearIcon");
            clearRunGradeScoreText ??= FindTmp(clearRunColumn, "GradeScoreText", "GradeScore", "FinalClearText");
            clearRunPurifiedText ??= FindStatValueText(clearRunColumn, "purification");
            clearRunEscapeText ??= FindStatValueText(clearRunColumn, "escape");
            clearRunOxygenSlider ??= FindDeep(clearRunColumn, "OxygenSlider")?.GetComponent<Slider>();
            clearRunOxygenFill ??= FindImage(clearRunColumn, "OxygenFill", "Fill", "Bar");
            clearRunOxygenPercentText ??= FindStatValueText(clearRunColumn, "oxygen");
        }

        Transform journeyColumn = FindDeepChild(detailPageRoot, "ThisJourney", "JourneyColumn");
        if (journeyColumn != null)
        {
            ResolveFactoryDotToggles(journeyColumn);
            journeyPurifiedText ??= FindStatValueText(journeyColumn, "purification");
            journeyPlayTimeText ??= FindStatValueText(journeyColumn, "activity", "playtime");
            journeyCrisisText ??= FindStatValueText(journeyColumn, "crisis", "gameover");
            EnsureJourneyEscapeText(journeyColumn);
        }
    }

    private void EnsureJourneyEscapeText(Transform journeyColumn)
    {
        if (journeyEscapeText != null || journeyColumn == null)
            return;

        journeyEscapeText = FindStatValueText(journeyColumn, "escape");
        if (journeyEscapeText != null)
            return;

        if (journeyPlayTimeText == null)
            journeyPlayTimeText = FindStatValueText(journeyColumn, "activity", "playtime");

        if (journeyPlayTimeText == null)
            return;

        GameObject clone = Instantiate(journeyPlayTimeText.gameObject, journeyColumn);
        clone.name = "Number of escapes (1)";
        RectTransform rect = clone.GetComponent<RectTransform>();
        RectTransform source = journeyPlayTimeText.rectTransform;
        rect.anchoredPosition = new Vector2(source.anchoredPosition.x, source.anchoredPosition.y + 70f);
        journeyEscapeText = clone.GetComponent<TextMeshProUGUI>();
    }

    private void ResolveFactoryDotToggles(Transform journeyColumn)
    {
        if (HasFactoryDotToggles())
            return;

        Transform factoryRoot = FindDeepChild(journeyColumn, "factory", "FactoryDots");
        if (factoryRoot == null)
            return;

        var toggles = new List<FactoryDotToggleBinding>();
        for (int i = 0; i < factoryRoot.childCount; i++)
        {
            Transform child = factoryRoot.GetChild(i);
            if (!HasDirectOnOffPair(child))
                continue;

            FactoryDotToggleBinding binding = FactoryDotToggleBinding.FromTransform(child);
            if (binding.IsValid)
                toggles.Add(binding);
        }

        if (toggles.Count == 0)
            return;

        toggles.Sort((a, b) => string.CompareOrdinal(a.SortName, b.SortName));
        journeyFactoryDotToggles = toggles.ToArray();
    }

    private void ResolveItemList(Transform detailRoot)
    {
        itemListRoot ??= FindDeep(detailRoot, "ItemList")?.gameObject;

        if (itemListRoot != null)
            itemEmptyText ??= FindTmp(itemListRoot.transform, "ItemEmptyText", "EmptyText");

        if (HasAnyItemListRow())
            return;

        Transform listRoot = itemListRoot != null ? itemListRoot.transform : detailRoot;
        var rows = new List<ItemListRowBinding>();
        for (int i = 0; i < listRoot.childCount; i++)
        {
            Transform child = listRoot.GetChild(i);
            if (!child.name.Contains("ItemListInformation"))
                continue;

            rows.Add(ItemListRowBinding.FromTransform(child));
        }

        if (rows.Count == 0)
            return;

        rows.Sort((a, b) => string.CompareOrdinal(a.SortName, b.SortName));
        itemListRows = rows.ToArray();
        itemListRoot ??= listRoot.gameObject;
    }

    private void WireButtons()
    {
        WireButton(closeButton, OnCloseClick);
        WireButton(fromBeginningButton, OnFromBeginningClick);
        WireButton(viewDetailsButton, OnViewDetailsClick);
        WireButton(backButton, OnBackFromDetailClick);
        WireButton(btnPrev, OnDetailPrevClick);
        WireButton(btnNext, OnDetailNextClick);
    }

    private static void WireButton(Button button, UnityEngine.Events.UnityAction handler)
    {
        if (button == null)
            return;

        button.onClick.RemoveListener(handler);
        button.onClick.AddListener(handler);
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

    private static string FormatPlayTimeMinutes(float playTimeSeconds)
    {
        int minutes = Mathf.Max(0, Mathf.FloorToInt(playTimeSeconds / 60f));
        return $"{minutes}분";
    }

    private static void SetText(TextMeshProUGUI label, string value)
    {
        if (label != null)
            label.text = value;
    }

    private static string ResolveItemId(string itemId)
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
            return AtlasManager.Instance.GetSprite(data.name);

        return null;
    }

    private static Button FindButton(Transform root, params string[] names)
    {
        for (int i = 0; i < names.Length; i++)
        {
            Transform found = FindDeep(root, names[i]);
            if (found != null)
            {
                Button button = found.GetComponent<Button>();
                if (button != null)
                    return button;
            }
        }

        return null;
    }

    private static TextMeshProUGUI FindTmp(Transform root, params string[] names)
    {
        for (int i = 0; i < names.Length; i++)
        {
            Transform found = FindDeep(root, names[i]);
            if (found != null)
            {
                TextMeshProUGUI label = found.GetComponent<TextMeshProUGUI>();
                if (label != null)
                    return label;
            }
        }

        return null;
    }

    private static TextMeshProUGUI FindStatValueText(Transform root, params string[] nameContains)
    {
        if (root == null || nameContains == null || nameContains.Length == 0)
            return null;

        TextMeshProUGUI[] labels = root.GetComponentsInChildren<TextMeshProUGUI>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            string objectName = labels[i].name;
            for (int c = 0; c < nameContains.Length; c++)
            {
                if (objectName.IndexOf(nameContains[c], StringComparison.OrdinalIgnoreCase) >= 0)
                    return labels[i];
            }
        }

        return null;
    }

    private static Image FindImage(Transform root, params string[] names)
    {
        for (int i = 0; i < names.Length; i++)
        {
            Transform found = FindDeep(root, names[i]);
            if (found != null)
            {
                Image image = found.GetComponent<Image>();
                if (image != null)
                    return image;
            }
        }

        return null;
    }

    private static bool HasDirectOnOffPair(Transform root)
    {
        return FindDirectChildByName(root, "On", "ON") != null
               && FindDirectChildByName(root, "Off", "OFF") != null;
    }

    private static Transform FindDirectChildByName(Transform root, params string[] names)
    {
        if (root == null)
            return null;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            for (int n = 0; n < names.Length; n++)
            {
                if (string.Equals(child.name, names[n], StringComparison.OrdinalIgnoreCase))
                    return child;
            }
        }

        return null;
    }

    private static Transform FindDeepChild(Transform root, params string[] names)
    {
        for (int i = 0; i < names.Length; i++)
        {
            Transform found = FindDeep(root, names[i]);
            if (found != null)
                return found;
        }

        return null;
    }

    private static Transform FindDeep(Transform root, string targetName)
    {
        if (root == null)
            return null;

        if (root.name == targetName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDeep(root.GetChild(i), targetName);
            if (found != null)
                return found;
        }

        return null;
    }

    [Serializable]
    private class FactoryDotToggleBinding
    {
        [SerializeField] private GameObject onObject;
        [SerializeField] private GameObject offObject;

        public string SortName => onObject != null ? onObject.transform.parent.name : string.Empty;

        public bool IsValid => onObject != null && offObject != null;

        public static FactoryDotToggleBinding FromTransform(Transform toggleRoot)
        {
            Transform on = FindDirectChildByName(toggleRoot, "On", "ON");
            Transform off = FindDirectChildByName(toggleRoot, "Off", "OFF");

            return new FactoryDotToggleBinding
            {
                onObject = on != null ? on.gameObject : null,
                offObject = off != null ? off.gameObject : null,
            };
        }

        public void Bind(bool isCleared)
        {
            if (onObject != null)
                onObject.SetActive(isCleared);

            if (offObject != null)
                offObject.SetActive(!isCleared);
        }
    }

    [Serializable]
    private class ChapterBarRowBinding
    {
        [SerializeField] private string rowName;
        [SerializeField] private Image fillImage;
        [SerializeField] private Slider fillSlider;
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private Image gradeIconImage;

        public string SortName => rowName ?? string.Empty;

        public bool HasAnyReference()
        {
            return fillImage != null || fillSlider != null || scoreText != null || gradeIconImage != null;
        }

        public static ChapterBarRowBinding FromTransform(Transform root)
        {
            return new ChapterBarRowBinding
            {
                rowName = root.name,
                fillImage = FindFilledImage(root),
                fillSlider = root.GetComponentInChildren<Slider>(true),
                scoreText = FindFirstTmp(root, "ScoreText", "Score"),
                gradeIconImage = FindGradeIconImage(root),
            };
        }

        public void Bind(ChapterSnapshot? snapshot, SpriteAtlas gradeAtlas)
        {
            if (!snapshot.HasValue || !snapshot.Value.isCleared)
            {
                SetFill(0f);
                SetText(scoreText, "-");
                if (gradeIconImage != null)
                {
                    gradeIconImage.sprite = null;
                    gradeIconImage.enabled = false;
                }

                return;
            }

            ChapterSnapshot data = snapshot.Value;
            SetFill(data.score / 100f);
            SetText(scoreText, $"{data.score}");

            if (gradeIconImage != null && gradeAtlas != null)
            {
                int gradeIndex = (int)data.grade;
                if (gradeIndex >= 0 && gradeIndex < GradeSpriteNames.Length)
                {
                    Sprite sprite = gradeAtlas.GetSprite(GradeSpriteNames[gradeIndex]);
                    gradeIconImage.sprite = sprite;
                    gradeIconImage.enabled = sprite != null;
                    gradeIconImage.preserveAspect = true;
                }
            }
        }

        private void SetFill(float normalized)
        {
            float amount = Mathf.Clamp01(normalized);

            if (fillSlider != null)
            {
                fillSlider.minValue = 0f;
                fillSlider.maxValue = 1f;
                fillSlider.value = amount;
            }

            if (fillImage != null)
            {
                fillImage.type = Image.Type.Filled;
                fillImage.fillMethod = Image.FillMethod.Horizontal;
                fillImage.fillAmount = amount;
            }
        }

        private static Image FindFilledImage(Transform root)
        {
            Image[] images = root.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                string name = images[i].name;
                if (name.Contains("Fill") || name.Contains("Bar"))
                    return images[i];
            }

            return null;
        }

        private static TextMeshProUGUI FindFirstTmp(Transform root, params string[] contains)
        {
            TextMeshProUGUI[] labels = root.GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                for (int c = 0; c < contains.Length; c++)
                {
                    if (labels[i].name.Contains(contains[c]))
                        return labels[i];
                }
            }

            return null;
        }

        private static Image FindGradeIconImage(Transform root)
        {
            Image[] images = root.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                string name = images[i].name;
                if (name.Contains("GradeIcon", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(name, "Icon", StringComparison.OrdinalIgnoreCase))
                    return images[i];
            }

            return null;
        }

        private static void SetText(TextMeshProUGUI label, string value)
        {
            if (label != null)
                label.text = value;
        }
    }

    [Serializable]
    private class ItemListRowBinding
    {
        [SerializeField] private string rowName;
        [SerializeField] private GameObject root;
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI quantityText;
        [SerializeField] private TextMeshProUGUI dataText;

        public string SortName => !string.IsNullOrEmpty(rowName) ? rowName : root != null ? root.name : string.Empty;

        public bool HasReference => root != null;

        public static ItemListRowBinding FromTransform(Transform rowRoot)
        {
            Transform iconRoot = FindDirectChildByName(rowRoot, "ItemIcon") ?? rowRoot;

            return new ItemListRowBinding
            {
                rowName = rowRoot.name,
                root = rowRoot.gameObject,
                iconImage = iconRoot.GetComponent<Image>() ?? iconRoot.GetComponentInChildren<Image>(true),
                nameText = FindRowTmp(rowRoot, "ItemName"),
                quantityText = FindRowTmp(rowRoot, "ItemQuantity"),
                dataText = FindRowTmp(rowRoot, "ItemData"),
            };
        }

        public void BindEmpty(string message)
        {
            if (root != null)
                root.SetActive(true);

            SetText(nameText, message);
            SetText(quantityText, string.Empty);
            SetText(dataText, string.Empty);

            if (iconImage != null)
            {
                iconImage.sprite = null;
                iconImage.enabled = false;
            }
        }

        public void Bind(string itemId, int count)
        {
            if (root != null)
                root.SetActive(true);

            string resolvedId = ResolveItemId(itemId);
            ItemData data = DataManager.Instance != null
                ? DataManager.Instance.GetItemData(resolvedId)
                : null;

            string displayName = data != null ? data.name : resolvedId;
            Sprite sprite = GetItemSprite(data);

            int displayCount = Mathf.Max(1, count);
            SetText(nameText, displayName);
            SetText(quantityText, $"x{displayCount}");
            SetText(dataText, data != null ? data.description : string.Empty);

            if (iconImage != null)
            {
                iconImage.sprite = sprite;
                iconImage.enabled = sprite != null;
                iconImage.preserveAspect = true;
            }
        }

        public void Hide()
        {
            if (root != null)
            {
                root.SetActive(false);
                return;
            }

            SetText(nameText, string.Empty);
            SetText(quantityText, string.Empty);
            SetText(dataText, string.Empty);

            if (iconImage != null)
            {
                iconImage.sprite = null;
                iconImage.enabled = false;
            }
        }

        private static TextMeshProUGUI FindRowTmp(Transform rowRoot, string childName)
        {
            Transform found = FindDirectChildByName(rowRoot, childName) ?? FindDeep(rowRoot, childName);
            return found != null ? found.GetComponent<TextMeshProUGUI>() : null;
        }

        private static void SetText(TextMeshProUGUI label, string value)
        {
            if (label == null)
                return;

            label.gameObject.SetActive(!string.IsNullOrEmpty(value));
            label.text = value;
        }
    }
}
