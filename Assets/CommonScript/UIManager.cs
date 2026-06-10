using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("--- UI 패널 ---")]
    [SerializeField] private UIInventory inventory;
    [SerializeField] private UIAcquisitionPopup acquisitionPopup;
    [SerializeField] private GameObject battleUIPanel;
    [SerializeField] private UIMainHUD mainHUD;
    [SerializeField] private UIResult resultPanel;
    [SerializeField] private UIGameClearStats gameClearStatsPanel;

    [Header("--- 선택: 오염도 UI ---")]
    [SerializeField] private Slider pollutionSlider;

    [Header("--- 선택: UIResult를 붙일 Canvas ---")]
    [SerializeField] private Canvas uiRootCanvas;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        CloseAllPanels();
    }

    private bool gameManagerSubscribed;
    private bool pollutionSubscribed;
    private Coroutine showStageResultRoutine;

    void OnEnable()
    {
        TrySubscribePollutionManager();
    }

    void Start()
    {
        TrySubscribeGameManager();
        TrySubscribePollutionManager();
    }

    void Update()
    {
        if (!gameManagerSubscribed)
            TrySubscribeGameManager();

        if (!pollutionSubscribed)
            TrySubscribePollutionManager();
    }

    void OnDestroy()
    {
        UnsubscribeGameManager();
        UnsubscribePollutionManager();
    }

    private void TrySubscribePollutionManager()
    {
        if (pollutionSubscribed || pollutionSlider == null)
            return;

        PollutionManager manager = PollutionManager.EnsureInstance();
        if (manager == null)
            return;

        manager.OnPollutionChanged -= HandlePollutionChanged;
        manager.OnPollutionChanged += HandlePollutionChanged;
        pollutionSubscribed = true;

        UpdatePollutionBar(manager.CurrentPollution, manager.MaxPollution);
    }

    private void UnsubscribePollutionManager()
    {
        if (!pollutionSubscribed)
            return;

        if (PollutionManager.Instance != null)
            PollutionManager.Instance.OnPollutionChanged -= HandlePollutionChanged;

        pollutionSubscribed = false;
    }

    private void TrySubscribeGameManager()
    {
        if (gameManagerSubscribed || GameManager.Instance == null)
            return;

        GameManager.Instance.OnBattleStarted += OpenBattleUI;
        GameManager.Instance.OnBattleEnded += HandleBattleEnded;
        GameManager.Instance.OnStageMonstersSpawned += ResetStageResult;
        gameManagerSubscribed = true;
    }

    private void UnsubscribeGameManager()
    {
        if (!gameManagerSubscribed || GameManager.Instance == null)
            return;

        GameManager.Instance.OnBattleStarted -= OpenBattleUI;
        GameManager.Instance.OnBattleEnded -= HandleBattleEnded;
        GameManager.Instance.OnStageMonstersSpawned -= ResetStageResult;
        gameManagerSubscribed = false;
    }

    private void HandleBattleEnded()
    {
        CloseBattleUI();

        if (showStageResultRoutine != null)
            StopCoroutine(showStageResultRoutine);

        showStageResultRoutine = StartCoroutine(TryShowStageResultAfterBattleEnd());
    }

    private IEnumerator TryShowStageResultAfterBattleEnd()
    {
        yield return null;

        UIResult panel = ResolveResultPanel();
        if (panel != null)
            EnsureResultPanelOnCanvas(panel);

        ForceCloseBattleLayers();

        if (GameManager.Instance != null && GameManager.Instance.ConsumeStageClearPending())
            ShowStageResultImmediate();

        showStageResultRoutine = null;
    }

    // --- 인벤토리 ---
    public void OpenInventory()
    {
        if (inventory != null)
            inventory.Open();
        else
            Debug.LogWarning("[UIManager] inventory가 연결되지 않았습니다.");
    }

    public void CloseInventory()
    {
        if (inventory != null)
            inventory.Close();
    }

    public void ToggleInventory()
    {
        if (inventory == null) return;
        bool isOpen = inventory.gameObject.activeSelf;
        if (isOpen) inventory.Close();
        else inventory.Open();
    }

    // --- 아이템 획득 팝업 (데이터 전달 + 화면 갱신) ---
    public void ShowAcquisitionPopup(string itemId, int count)
    {
        if (acquisitionPopup == null)
        {
            Debug.LogWarning("[UIManager] acquisitionPopup이 연결되지 않았습니다.");
            return;
        }

        acquisitionPopup.SetupPopup(itemId, count);
        acquisitionPopup.gameObject.SetActive(true);
    }

    public void CloseAcquisitionPopup()
    {
        SetPanelActive(acquisitionPopup != null ? acquisitionPopup.gameObject : null, false);
    }

    // --- 스테이지 결과 ---
    public void ShowStageResult()
    {
        if (showStageResultRoutine != null)
            StopCoroutine(showStageResultRoutine);

        showStageResultRoutine = StartCoroutine(ShowStageResultDeferred());
    }

    private IEnumerator ShowStageResultDeferred()
    {
        yield return null;

        UIResult panel = ResolveResultPanel();
        if (panel != null)
            EnsureResultPanelOnCanvas(panel);

        ForceCloseBattleLayers();
        ShowStageResultImmediate();
        showStageResultRoutine = null;
    }

    private void ShowStageResultImmediate()
    {
        SaveCurrentChapterSnapshot();

        if (TryShowFinalChapterClearStats())
            return;

        UIResult panel = ResolveResultPanel();
        if (panel == null)
            return;

        EnsureResultPanelOnCanvas(panel);
        panel.ShowStageClearResult();
    }

    private static void SaveCurrentChapterSnapshot()
    {
        ChapterManager chapterManager = ChapterManager.Instance;
        PlaySessionStats stats = PlaySessionStats.EnsureInstance();
        if (chapterManager == null || stats == null)
            return;

        stats.SaveSnapshotForCurrentChapter(chapterManager.CurrentChapterIndex);
    }

    private bool TryShowFinalChapterClearStats()
    {
        ChapterManager chapterManager = ChapterManager.Instance;
        if (chapterManager == null
            || chapterManager.ChapterCount <= 0
            || chapterManager.CurrentChapterIndex < chapterManager.ChapterCount)
            return false;

        UIGameClearStats panel = ResolveClearStatsPanel();
        if (panel == null)
            return false;

        EnsureClearStatsPanelOnCanvas(panel);
        panel.ShowMain();
        Debug.Log("[UIManager] 마지막 공장 클리어 — UIGameClearStats 표시");
        return true;
    }

    private void EnsureResultPanelOnCanvas(UIResult panel)
    {
        if (panel == null)
            return;

        Canvas rootCanvas = ResolveRootCanvas(panel);
        if (rootCanvas == null)
        {
            Debug.LogWarning("[UIManager] UIResult를 붙일 Canvas를 찾지 못했습니다.");
            return;
        }

        Transform canvasTransform = rootCanvas.transform;
        if (panel.transform.parent != canvasTransform)
            panel.transform.SetParent(canvasTransform, false);

        if (!rootCanvas.gameObject.activeSelf)
            rootCanvas.gameObject.SetActive(true);

        panel.transform.SetAsLastSibling();
    }

    private Canvas ResolveRootCanvas(UIResult panel)
    {
        if (uiRootCanvas != null)
            return uiRootCanvas;

        if (mainHUD != null)
        {
            Canvas canvas = mainHUD.GetComponentInParent<Canvas>(true);
            if (canvas != null)
                return canvas;
        }

        if (inventory != null)
        {
            Canvas canvas = inventory.GetComponentInParent<Canvas>(true);
            if (canvas != null)
                return canvas;
        }

        if (panel != null)
        {
            Canvas[] canvases = panel.GetComponentsInParent<Canvas>(true);
            if (canvases != null && canvases.Length > 0)
                return canvases[canvases.Length - 1];
        }

        return FindAnyObjectByType<Canvas>(FindObjectsInactive.Include);
    }

    private UIResult ResolveResultPanel()
    {
        if (resultPanel != null)
            return resultPanel;

        UIResult found = FindAnyObjectByType<UIResult>(FindObjectsInactive.Include);
        if (found == null)
            Debug.LogWarning("[UIManager] resultPanel이 연결되지 않았습니다.");

        return found;
    }

    private UIGameClearStats ResolveClearStatsPanel()
    {
        if (gameClearStatsPanel != null)
            return gameClearStatsPanel;

        return FindAnyObjectByType<UIGameClearStats>(FindObjectsInactive.Include);
    }

    private void EnsureClearStatsPanelOnCanvas(UIGameClearStats panel)
    {
        if (panel == null)
            return;

        Canvas rootCanvas = ResolveRootCanvas(null);
        if (rootCanvas == null)
        {
            Debug.LogWarning("[UIManager] UIGameClearStats를 붙일 Canvas를 찾지 못했습니다.");
            return;
        }

        Transform canvasTransform = rootCanvas.transform;
        if (panel.transform.parent != canvasTransform)
            panel.transform.SetParent(canvasTransform, false);

        if (!rootCanvas.gameObject.activeSelf)
            rootCanvas.gameObject.SetActive(true);

        panel.transform.SetAsLastSibling();
    }

    private void ForceCloseBattleLayers()
    {
        CloseBattleUI();
        CloseAcquisitionPopup();

        GameObject battleRoot = GameObject.Find("UIBattlescene");
        if (battleRoot != null && battleRoot.activeSelf)
            battleRoot.SetActive(false);
    }

    public void ResetStageResult()
    {
        if (resultPanel != null)
            resultPanel.ResetStageResultState();

        UIGameClearStats clearStats = ResolveClearStatsPanel();
        if (clearStats != null)
            clearStats.ResetShowState();
    }

    // --- 배틀 UI ---
    public void OpenBattleUI()
    {
        SetPanelActive(battleUIPanel, true);
        ResetBattleUIOnOpen();
    }

    public void CloseBattleUI()
    {
        CloseInventory();
        UIInventory.ClearBattleOverlayTracking();
        UIButtonContainer.ResetAllRuntimeButtonState();
        SetPanelActive(battleUIPanel, false);
    }

    public bool IsBattleUiVisible()
    {
        return battleUIPanel != null && battleUIPanel.activeInHierarchy;
    }

    private void ResetBattleUIOnOpen()
    {
        CloseInventory();
        CloseAcquisitionPopup();
        UIInventory.ClearBattleOverlayTracking();

        if (battleUIPanel == null)
            return;

        UIBattleManager battleManager = battleUIPanel.GetComponentInChildren<UIBattleManager>(true);
        if (battleManager != null)
        {
            battleManager.ResetBattleUIState();
            battleManager.ResetMonsterBattleStatus();
        }

        UIButtonContainer.ResetAllRuntimeButtonState();
    }

    // --- HUD ---
    public void UpdateOxygenGauge(float currentOxygen, float maxOxygen)
    {
        if (mainHUD != null)
            mainHUD.UpdateOxygenGauge(currentOxygen, maxOxygen);
    }

    private void HandlePollutionChanged(float currentPollution, float maxPollution)
    {
        UpdatePollutionBar(currentPollution, maxPollution);
    }

    /// <summary>PollutionSlider는 0~100 절대값으로 맞춥니다(비율 0~1 아님).</summary>
    public void UpdatePollutionBar(float currentPollution, float maxPollution)
    {
        if (pollutionSlider == null)
            return;

        float max = maxPollution > 0f ? maxPollution : PollutionManager.DefaultInitialPollution;
        pollutionSlider.maxValue = max;
        pollutionSlider.value = Mathf.Clamp(currentPollution, pollutionSlider.minValue, max);
    }

    // --- 범용 ---
    public void SetPanelActive(GameObject panel, bool active)
    {
        if (panel != null)
            panel.SetActive(active);
    }

    public void CloseAllPanels()
    {
        CloseInventory();
        CloseAcquisitionPopup();
        CloseBattleUI();
    }
}
