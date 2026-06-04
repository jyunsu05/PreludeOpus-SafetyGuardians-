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
    private string depletedItemIdThisBattle;

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
            uiManager = FindAnyObjectByType<UIBattleManager>();

        if (uiManager == null)
        {
            Debug.LogError("[UIButtonContainer] UIBattleManager를 찾을 수 없습니다!");
            return;
        }

        uiManager.OnContaminationEmpty -= OnContaminationCleared;
        uiManager.OnContaminationEmpty += OnContaminationCleared;
        uiManager.OnEscapeLockChanged -= ApplyEscapeLock;
        uiManager.OnEscapeLockChanged += ApplyEscapeLock;
    }

    private void UnsubscribeUiManager()
    {
        if (uiManager == null)
            return;

        uiManager.OnContaminationEmpty -= OnContaminationCleared;
        uiManager.OnEscapeLockChanged -= ApplyEscapeLock;
    }

    /// <summary>씬에 있는 모든 UIButtonContainer의 배틀 버튼 상태를 초기화합니다.</summary>
    public static void ResetAllRuntimeButtonState()
    {
        UIButtonContainer[] containers =
            FindObjectsByType<UIButtonContainer>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < containers.Length; i++)
        {
            if (containers[i] != null)
                containers[i].ResetButtonsState();
        }
    }

    public void ResetButtonsState()
    {
        isEscaping = false;
        depletedItemIdThisBattle = null;
        SetBattleActionsLocked(false);

        if (searchButton != null)
        {
            searchButton.gameObject.SetActive(true);
            searchButton.interactable = true;
        }

        if (purifyButton != null)
        {
            purifyButton.gameObject.SetActive(false);
            purifyButton.interactable = true;
        }

        wasEscapeUiActive = true;
        SetEscapeUiVisible(true);
        ResetEscapeButtonInteractable();

        uiManager?.ExitBattle();
        ApplyBattleKeyboardInputGuard();
    }

    public void OnSearchClick()
    {
        if (isEscaping || uiManager == null)
            return;

        if (uiManager.IsScanned)
            return;

        uiManager.NotifySearchCompleted();

        if (searchButton != null)
            searchButton.interactable = true;

        if (purifyButton != null)
        {
            purifyButton.gameObject.SetActive(true);
            UpdatePurifyButtonInteractable();
        }

        uiManager.RevealScannedInfo(
            GetInfectionTypeText(),
            GetDescriptionText(),
            uiManager.BuildInventoryStatusText());
    }

    public void OnPurifyClick()
    {
        if (isEscaping || uiManager == null)
            return;

        string itemId = uiManager.GetRequiredPurifyItemId();
        if (!uiManager.CanPurifyWithInventory(itemId))
        {
            string itemName = GetItemName(itemId);
            Debug.LogWarning($"[UIButtonContainer] 정화 아이템 없음: {itemId}");
            uiManager.RevealScannedInfo(
                GetInfectionTypeText(),
                GetDescriptionText(),
                $"{itemName} 없음");
            UpdatePurifyButtonInteractable();
            return;
        }

        int durabilityBeforeUse = InventoryManager.Instance.GetItemRemainingDurability(itemId);

        if (!uiManager.OnClickPurify(out int consumedDurability))
        {
            UpdatePurifyButtonInteractable();
            return;
        }

        if (durabilityBeforeUse > 0 && consumedDurability >= durabilityBeforeUse)
            depletedItemIdThisBattle = itemId;

        if (purifyButton != null)
            purifyButton.interactable = uiManager.CanPurifyWithInventory(itemId);

        uiManager.RevealScannedInfo(
            GetInfectionTypeText(),
            GetDescriptionText(),
            uiManager.BuildInventoryStatusText());
    }

    public void OnEscapeClick()
    {
        if (isEscaping || uiManager == null)
            return;

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
            Debug.LogWarning("[UIButtonContainer] BattleUIController가 없어 산소 패널티를 생략합니다.");

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

        bool shouldAddRewardToInventory = rewardItemId != depletedItemIdThisBattle;
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
        if (escapeButton != null)
            escapeButton.interactable = true;
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

    /// <summary>아이템 획득 팝업이 뜰 때만 탐색·정화·도망 UI를 전부 끕니다.</summary>
    private void HideAllActionButtonsForAcquisitionPopup()
    {
        SetBattleActionsLocked(true);

        if (searchButton != null)
            searchButton.gameObject.SetActive(false);

        if (purifyButton != null)
            purifyButton.gameObject.SetActive(false);

        SetEscapeUiVisible(false);
    }

    private void UpdatePurifyButtonInteractable()
    {
        if (purifyButton == null || uiManager == null)
            return;

        purifyButton.interactable = uiManager.CanPurifyWithInventory();
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
        if (monster == null)
            return "감염물질 이름";

        return !string.IsNullOrEmpty(monster.infection_type) ? monster.infection_type : monster.name;
    }

    private string GetDescriptionText()
    {
        MonsterData monster = uiManager != null ? uiManager.GetCurrentMonsterData() : null;
        if (monster == null || string.IsNullOrEmpty(monster.description))
            return "정화 방법 설명";

        return monster.description;
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
