using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UIButtonContainer : MonoBehaviour
{
    private UIBattleManager uiManager;

    [Header("--- 배틀 액션 UI 잠금 ---")]
    [Tooltip("비어 있으면 이 오브젝트에서 CanvasGroup을 찾습니다.")]
    [SerializeField] private CanvasGroup actionCanvasGroup;

    [Header("--- SLA 도망 로직 (선택) ---")]
    [SerializeField] private BattleUIController battleUIController;

    [Header("--- 자식 액션 버튼들 ---")]
    [SerializeField] private Button searchButton;
    [SerializeField] private Button purifyButton;
    [SerializeField] private Button escapeButton;

    [Tooltip("비어 있으면 escapeButton 오브젝트(및 자식)를 숨깁니다. 도망 UI 전체 래퍼가 있으면 여기에 연결하세요.")]
    [SerializeField] private GameObject escapeUiRoot;

    [Header("--- 아이템 획득 팝업 ---")]
    [SerializeField] private UIAcquisitionPopup acquisitionPopup;

    private bool isEscaping;
    private bool wasEscapeUiActive = true;
    private bool battleActionPanelHiddenByOverlay;
    private bool overlayHideUsedDeactivate;
    private bool storedActiveBeforeOverlayHide = true;
    private string depletedItemIdThisBattle;
    private BattleTurnController turnController;

    private void Awake()
    {
        ResolveActionCanvasGroup();
    }

    private void Start()
    {
        ResolveBattleUIController();
        EnsureUiManagerSubscribed();
    }

    private void OnEnable()
    {
        ResolveActionCanvasGroup();
        EnsureUiManagerSubscribed();
        ResetButtonsState();
        ApplyBattleKeyboardInputGuard();
    }

    private void OnDisable()
    {
        UnsubscribeUiManager();
        ResetEscapeButtonInteractable();
        SetBattleActionsLocked(false);
    }

    private void EnsureUiManagerSubscribed()
    {
        if (uiManager == null)
            uiManager = GetComponentInParent<UIBattleManager>();

        if (uiManager == null)
        {
            Transform battleRoot = transform.parent;
            if (battleRoot != null)
                uiManager = UIBattleManager.TryGetPrimaryInHierarchy(battleRoot);
        }

        if (uiManager == null || !uiManager.enabled)
            uiManager = UIBattleManager.TryGetPrimaryActive();

        if (uiManager == null)
        {
            Debug.LogError("[UIButtonContainer] UIBattleManager를 찾을 수 없습니다!");
            return;
        }

        uiManager.OnContaminationEmpty -= OnContaminationCleared;
        uiManager.OnContaminationEmpty += OnContaminationCleared;
        uiManager.OnEscapeLockChanged -= ApplyEscapeLock;
        uiManager.OnEscapeLockChanged += ApplyEscapeLock;

        ResolveTurnController();
        if (turnController != null)
        {
            turnController.OnTurnPhaseChanged -= HandleTurnPhaseChanged;
            turnController.OnTurnPhaseChanged += HandleTurnPhaseChanged;
            HandleTurnPhaseChanged(turnController.CurrentPhase);
        }
    }

    private void UnsubscribeUiManager()
    {
        if (uiManager == null)
            return;

        uiManager.OnContaminationEmpty -= OnContaminationCleared;
        uiManager.OnEscapeLockChanged -= ApplyEscapeLock;

        if (turnController != null)
            turnController.OnTurnPhaseChanged -= HandleTurnPhaseChanged;
    }

    /// <summary>턴 전환 후 모든 배틀 액션 버튼 상태를 갱신합니다.</summary>
    public static void RefreshAllPlayerTurnButtons()
    {
        UIButtonContainer[] containers =
            FindObjectsByType<UIButtonContainer>(FindObjectsInactive.Include);

        for (int i = 0; i < containers.Length; i++)
        {
            if (containers[i] != null && containers[i].isActiveAndEnabled)
                containers[i].UpdateActionButtonsForPlayerTurn();
        }
    }

    public static void SetAllBattleInputBlocked(bool blocked)
    {
        UIButtonContainer[] containers =
            FindObjectsByType<UIButtonContainer>(FindObjectsInactive.Include);

        for (int i = 0; i < containers.Length; i++)
        {
            if (containers[i] != null)
                containers[i].SetBattleActionsLocked(blocked);
        }
    }

    /// <summary>배틀 중 오버레이(인벤토리 등)가 열릴 때 액션 버튼 패널 전체를 숨기거나 복구합니다.</summary>
    public static void SetAllBattleActionPanelsVisible(bool visible)
    {
        UIButtonContainer[] containers =
            FindObjectsByType<UIButtonContainer>(FindObjectsInactive.Include);

        for (int i = 0; i < containers.Length; i++)
        {
            if (containers[i] != null)
                containers[i].SetBattleActionPanelVisible(visible);
        }
    }

    public void SetBattleActionPanelVisible(bool visible)
    {
        if (!visible)
        {
            if (!battleActionPanelHiddenByOverlay)
            {
                storedActiveBeforeOverlayHide = gameObject.activeSelf;
                battleActionPanelHiddenByOverlay = true;
            }

            ResolveActionCanvasGroup();
            if (actionCanvasGroup != null)
            {
                overlayHideUsedDeactivate = false;
                actionCanvasGroup.alpha = 0f;
                actionCanvasGroup.interactable = false;
                actionCanvasGroup.blocksRaycasts = false;
            }
            else
            {
                overlayHideUsedDeactivate = true;
                if (gameObject.activeSelf)
                    gameObject.SetActive(false);
            }

            return;
        }

        if (!battleActionPanelHiddenByOverlay)
            return;

        battleActionPanelHiddenByOverlay = false;

        if (overlayHideUsedDeactivate)
            gameObject.SetActive(storedActiveBeforeOverlayHide);
        else if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        ResolveActionCanvasGroup();
        if (actionCanvasGroup != null)
        {
            actionCanvasGroup.alpha = 1f;
            actionCanvasGroup.interactable = true;
            actionCanvasGroup.blocksRaycasts = true;
        }

        RestoreBattleActionPanelState();
    }

    private void RestoreBattleActionPanelState()
    {
        EnsureUiManagerSubscribed();
        ResetButtonsState();

        ResolveTurnController();
        if (turnController != null)
            HandleTurnPhaseChanged(turnController.CurrentPhase);
        else
            UpdateActionButtonsForPlayerTurn();
    }

    /// <summary>씬에 있는 모든 UIButtonContainer의 배틀 버튼 상태를 초기화합니다.</summary>
    public static void ResetAllRuntimeButtonState()
    {
        UIButtonContainer[] containers =
            FindObjectsByType<UIButtonContainer>(FindObjectsInactive.Include);

        for (int i = 0; i < containers.Length; i++)
        {
            if (containers[i] != null)
                containers[i].ResetButtonsState();
        }
    }

    public void ResetButtonsState()
    {
        ResetOverlayHideVisualState();

        isEscaping = false;
        depletedItemIdThisBattle = null;
        SetBattleActionsLocked(false);

        if (searchButton != null)
        {
            searchButton.gameObject.SetActive(true);
            searchButton.interactable = true;
        }

        SyncPurifyButtonVisibility();

        wasEscapeUiActive = true;
        SetEscapeUiVisible(true);
        ResetEscapeButtonInteractable();
        ApplyBattleKeyboardInputGuard();
    }

    private void ResetOverlayHideVisualState()
    {
        battleActionPanelHiddenByOverlay = false;
        overlayHideUsedDeactivate = false;

        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        ResolveActionCanvasGroup();
        if (actionCanvasGroup == null)
            return;

        actionCanvasGroup.alpha = 1f;
        actionCanvasGroup.interactable = true;
        actionCanvasGroup.blocksRaycasts = true;
    }

    public void OnSearchClick()
    {
        if (isEscaping || uiManager == null)
            return;

        if (uiManager.IsScanned || uiManager.IsSearching)
            return;

        if (!uiManager.CanBeginSearch())
            return;

        BattleAutoManager.Instance?.BlockAutoTurnForManualAction();
        SetBattleActionsLocked(true);
        uiManager.PrepareSearchLensForPlayback();

        if (!uiManager.TryBeginSearch(CompleteSearchAfterAnimation))
        {
            uiManager.CancelSearchLensPresentation();
            SetBattleActionsLocked(false);
        }
    }

    private void CompleteSearchAfterAnimation()
    {
        ApplySearchScanResults();
        SetBattleActionsLocked(false);
        UpdateActionButtonsForPlayerTurn();
    }

    /// <summary>탐색 연출 종료 후 스캔 정보·정화 버튼 상태를 갱신합니다. (아이템 보유는 정화 버튼에만 영향)</summary>
    private void ApplySearchScanResults()
    {
        uiManager.NotifySearchCompleted();

        SyncPurifyButtonVisibility();

        uiManager.RevealScannedInfo(
            GetInfectionTypeText(),
            GetDescriptionText(),
            uiManager.BuildInventoryStatusText());
    }

    public void OnPurifyClick()
    {
        if (isEscaping || uiManager == null || !CanUsePlayerTurnAction())
            return;

        string itemId = uiManager.GetRequiredPurifyItemId();
        if (!uiManager.CanPurifyWithInventory(itemId))
        {
            Debug.LogWarning($"[UIButtonContainer] 정화 아이템 없음: {itemId}");
            uiManager.RevealScannedInfo(
                GetInfectionTypeText(),
                GetDescriptionText(),
                uiManager.BuildInventoryStatusText());
            UpdatePurifyButtonInteractable();
            return;
        }

        if (!uiManager.OnClickPurify(out _))
        {
            UpdatePurifyButtonInteractable();
            return;
        }

        BattleAutoManager.Instance?.EngageAutoBattleAfterManualPurify();

        if (purifyButton != null)
            purifyButton.interactable = uiManager.CanPurifyWithInventory(itemId);

        uiManager.RevealScannedInfo(
            GetInfectionTypeText(),
            GetDescriptionText(),
            uiManager.BuildInventoryStatusText());
    }

    public void OnEscapeClick()
    {
        if (isEscaping || uiManager == null || !CanUsePlayerTurnAction())
            return;

        BattleAutoManager.Instance?.BlockAutoTurnForManualAction();

        if (!uiManager.CanAttemptEscape)
            return;

        if (!uiManager.TryBeginFleeExit())
            return;

        isEscaping = true;
        SetBattleActionsLocked(true);
        Debug.Log("[UIButtonContainer] 전투 이탈 시도.");

        ResolveBattleUIController();
        if (battleUIController != null)
            battleUIController.ApplyFleePenaltyOnly();
        else
        {
            PlaySessionStats.EnsureInstance()?.RecordEscape();
            Debug.LogWarning("[UIButtonContainer] BattleUIController가 없어 산소 패널티를 생략합니다.");
        }

        uiManager.CompleteFleeExit();
    }

    private void OnContaminationCleared()
    {
        isEscaping = true;
        HideAllActionButtonsForAcquisitionPopup();

        Debug.Log("[UIButtonContainer] 정화 완료 — 획득 팝업 표시, 배틀 액션 버튼 전부 숨김.");

        if (acquisitionPopup == null)
        {
            Debug.LogWarning("[UIButtonContainer] acquisitionPopup이 연결되지 않았습니다!");
            return;
        }

        string rewardItemId = GetRewardItemId();
        if (DataManager.Instance != null)
            rewardItemId = DataManager.Instance.GetFactoryItemIdForInventory(rewardItemId);

        string consumedItemId = uiManager != null ? uiManager.LastConsumedBattleItemId : null;
        depletedItemIdThisBattle = consumedItemId;

        bool shouldAddRewardToInventory = string.IsNullOrEmpty(consumedItemId) ||
                                          rewardItemId != consumedItemId;
        acquisitionPopup.gameObject.SetActive(true);
        acquisitionPopup.SetupPopup(rewardItemId, 1, shouldAddRewardToInventory);
    }

    /// <summary>locked=true면 도망 UI 루트·자식을 SetActive(false)로 배틀씬에서 숨깁니다.</summary>
    private void ApplyEscapeLock(bool locked)
    {
        SetEscapeUiVisible(!locked);
    }

    private void ResetEscapeButtonInteractable()
    {
        if (escapeButton == null || uiManager == null)
            return;

        escapeButton.interactable = CanUsePlayerTurnAction() && uiManager.CanAttemptEscape;
    }

    private GameObject ResolveEscapeUiRoot()
    {
        if (escapeUiRoot != null)
            return escapeUiRoot;

        if (escapeButton == null)
            return null;

        return escapeButton.gameObject;
    }

    private void SetEscapeUiVisible(bool visible)
    {
        GameObject root = ResolveEscapeUiRoot();
        if (root == null)
            return;

        if (!visible)
        {
            wasEscapeUiActive = root.activeSelf;
            root.SetActive(false);
            return;
        }

        root.SetActive(wasEscapeUiActive);
    }

    /// <summary>아이템 획득 팝업이 뜰 때 배틀 액션 버튼 패널 전체를 숨깁니다.</summary>
    private void HideAllActionButtonsForAcquisitionPopup()
    {
        SetBattleActionPanelVisible(false);
        SetBattleActionsLocked(true);
    }

    private void SyncPurifyButtonVisibility()
    {
        if (purifyButton == null)
            return;

        bool shouldShow = uiManager != null && uiManager.IsScanned;
        if (purifyButton.gameObject.activeSelf != shouldShow)
            purifyButton.gameObject.SetActive(shouldShow);

        if (!shouldShow)
            purifyButton.interactable = true;
        else
            UpdatePurifyButtonInteractable();
    }

    private void UpdatePurifyButtonInteractable()
    {
        if (purifyButton == null || uiManager == null)
            return;

        if (!purifyButton.gameObject.activeSelf)
            return;

        purifyButton.interactable = CanUsePlayerTurnAction() && uiManager.CanPurifyWithInventory();
    }

    private void HandleTurnPhaseChanged(BattleTurnController.BattleTurnPhase phase)
    {
        bool isPlayerTurn = phase == BattleTurnController.BattleTurnPhase.PlayerTurn;
        bool isResolving = turnController != null && turnController.IsResolvingTurn;
        SetBattleActionsLocked(!isPlayerTurn || isResolving);
        UIInventory.RefreshAllVisible();
        UpdateActionButtonsForPlayerTurn();
    }

    public void UpdateActionButtonsForPlayerTurn()
    {
        if (uiManager == null)
            return;

        bool canAct = CanUsePlayerTurnAction();

        if (searchButton != null)
            searchButton.interactable = canAct && !uiManager.IsScanned && !uiManager.IsSearching;

        SyncPurifyButtonVisibility();
        ResetEscapeButtonInteractable();
    }

    private bool CanUsePlayerTurnAction()
    {
        if (uiManager == null)
            return false;

        if (uiManager.IsSearching)
            return false;

        ResolveTurnController();
        if (turnController != null && (!turnController.IsPlayerTurn || turnController.IsResolvingTurn))
            return false;

        return uiManager.IsPlayerTurnActive();
    }

    private void ResolveTurnController()
    {
        if (turnController != null)
            return;

        if (uiManager != null && uiManager.TurnController != null)
        {
            turnController = uiManager.TurnController;
            return;
        }

        turnController = FindAnyObjectByType<BattleTurnController>(FindObjectsInactive.Include);
    }

    private void ApplyBattleKeyboardInputGuard()
    {
        ClearUiSelection();
        DisableButtonKeyboardNavigation(searchButton);
        DisableButtonKeyboardNavigation(purifyButton);
        DisableButtonKeyboardNavigation(escapeButton);
    }

    private static void ClearUiSelection()
    {
        if (EventSystem.current == null)
            return;

        EventSystem.current.SetSelectedGameObject(null);
    }

    private static void DisableButtonKeyboardNavigation(Button button)
    {
        if (button == null)
            return;

        Navigation navigation = button.navigation;
        navigation.mode = Navigation.Mode.None;
        button.navigation = navigation;
    }

    private void ResolveActionCanvasGroup()
    {
        if (actionCanvasGroup != null)
            return;

        actionCanvasGroup = GetComponent<CanvasGroup>();
        if (actionCanvasGroup == null)
            actionCanvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    private void SetBattleActionsLocked(bool locked)
    {
        ResolveActionCanvasGroup();
        if (actionCanvasGroup == null)
            return;

        actionCanvasGroup.interactable = !locked;
        actionCanvasGroup.blocksRaycasts = !locked;
    }

    private void ResolveBattleUIController()
    {
        if (battleUIController != null && battleUIController.isActiveAndEnabled)
            return;

        battleUIController = FindAnyObjectByType<BattleUIController>(FindObjectsInactive.Include);
    }

    private string GetInfectionTypeText()
    {
        MonsterData monster = uiManager != null ? uiManager.GetCurrentMonsterData() : null;
        if (monster == null || string.IsNullOrEmpty(monster.description))
            return "감염물질 이름";

        return monster.description;
    }

    private string GetDescriptionText()
    {
        MonsterData monster = uiManager != null ? uiManager.GetCurrentMonsterData() : null;
        if (monster == null || string.IsNullOrEmpty(monster.purification_method))
            return "정화 방법 설명";

        return monster.purification_method;
    }

    private string GetRewardItemId()
    {
        if (uiManager == null)
            return "MI-101";

        return uiManager.GetRequiredPurifyItemId();
    }

    private static string GetItemName(string itemId)
    {
        if (DataManager.Instance == null || string.IsNullOrEmpty(itemId))
            return itemId;

        ItemData data = DataManager.Instance.GetItemData(itemId);
        if (data == null || string.IsNullOrEmpty(data.name))
            return itemId;

        return data.name;
    }
}
