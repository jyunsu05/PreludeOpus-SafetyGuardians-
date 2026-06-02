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
    private Coroutine showStageResultRoutine;

    void Start()
    {
        TrySubscribeGameManager();
    }

    void Update()
    {
        if (!gameManagerSubscribed)
            TrySubscribeGameManager();
    }

    void OnDestroy()
    {
        UnsubscribeGameManager();
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
        UIResult panel = ResolveResultPanel();
        if (panel == null)
            return;

        EnsureResultPanelOnCanvas(panel);
        panel.ShowStageClearResult();
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
    }

    // --- 배틀 UI ---
    public void OpenBattleUI()
    {
        SetPanelActive(battleUIPanel, true);
        ResetBattleUIOnOpen();
    }

    public void CloseBattleUI() => SetPanelActive(battleUIPanel, false);

    private void ResetBattleUIOnOpen()
    {
        CloseAcquisitionPopup();

        if (battleUIPanel == null)
            return;

        UIBattleManager battleManager = battleUIPanel.GetComponentInChildren<UIBattleManager>(true);
        if (battleManager != null)
            battleManager.ResetBattleUIState();

        UIButtonContainer buttonContainer = battleUIPanel.GetComponentInChildren<UIButtonContainer>(true);
        if (buttonContainer != null)
            buttonContainer.ResetButtonsState();
    }

    // --- HUD ---
    public void UpdateOxygenGauge(float currentOxygen, float maxOxygen)
    {
        if (mainHUD != null)
            mainHUD.UpdateOxygenGauge(currentOxygen, maxOxygen);
    }

    // PollutionManager 등 외부에서 비율만 넘겨 호출 (UIManager는 PollutionManager를 직접 참조하지 않음)
    public void UpdatePollutionBar(float ratio)
    {
        if (pollutionSlider != null)
            pollutionSlider.value = Mathf.Clamp01(ratio);
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
