using System.Collections;
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

    [Header("--- 아이템 획득 팝업 ---")]
    [SerializeField] private UIAcquisitionPopup acquisitionPopup;

    [Header("--- 정화 설정 ---")]
    [SerializeField] private int purifyDurabilityPerUse = 10;

    private bool isScanned;
    private bool isProcessing;
    private bool isEscaping;
    private string depletedItemIdThisBattle;

    private const string DefaultRewardItemId = "MI-101";

    private void Awake()
    {
        ResolveActionCanvasGroup();
    }

    private void Start()
    {
        uiManager = FindAnyObjectByType<UIBattleManager>();
        ResolveBattleUIController();

        if (uiManager == null)
            Debug.LogError("[UIButtonContainer] UIBattleManager를 찾을 수 없습니다!");
        else
            uiManager.OnContaminationEmpty += OnContaminationCleared;

        ResetButtonsState();
    }

    private void OnEnable()
    {
        ResolveActionCanvasGroup();
        ResetButtonsState();
        ApplyBattleKeyboardInputGuard();
        StartCoroutine(ApplyBattleKeyboardInputGuardNextFrame());
    }

    private IEnumerator ApplyBattleKeyboardInputGuardNextFrame()
    {
        yield return null;
        ApplyBattleKeyboardInputGuard();
    }

    private void OnDestroy()
    {
        if (uiManager != null)
            uiManager.OnContaminationEmpty -= OnContaminationCleared;
    }

    public void ResetButtonsState()
    {
        isScanned = false;
        isProcessing = false;
        isEscaping = false;
        depletedItemIdThisBattle = null;

        SetBattleActionsLocked(false);

        if (searchButton != null)
        {
            searchButton.gameObject.SetActive(true);
            searchButton.interactable = true;
        }

        if (purifyButton != null)
            purifyButton.gameObject.SetActive(false);

        if (escapeButton != null)
        {
            escapeButton.gameObject.SetActive(true);
            escapeButton.interactable = true;
        }

        ApplyBattleKeyboardInputGuard();
    }

    /// <summary>
    /// WASD/방향키 UI Navigate·Submit이 정화 버튼을 눌러버리는 것을 방지합니다. (마우스 클릭만 허용)
    /// </summary>
    private void ApplyBattleKeyboardInputGuard()
    {
        ClearUiSelection();
        DisableButtonKeyboardNavigation(searchButton);
        DisableButtonKeyboardNavigation(purifyButton);
        DisableButtonKeyboardNavigation(escapeButton);
    }

    private static void ClearUiSelection()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
            return;

        eventSystem.SetSelectedGameObject(null);
    }

    private static void DisableButtonKeyboardNavigation(Button button)
    {
        if (button == null)
            return;

        Navigation navigation = button.navigation;
        navigation.mode = Navigation.Mode.None;
        button.navigation = navigation;
    }

    public void OnSearchClick()
    {
        if (isProcessing || isEscaping)
            return;

        Debug.Log("[UIButtonContainer] OnSearchClick 호출됨.");

        if (isScanned)
            return;

        isScanned = true;

        if (searchButton != null)
            searchButton.gameObject.SetActive(false);

        if (purifyButton != null)
        {
            purifyButton.gameObject.SetActive(true);
            Debug.Log("[UIButtonContainer] 정화 버튼 활성화 완료.");
        }
        else
        {
            Debug.LogError("[UIButtonContainer] purifyButton이 null입니다! 인스펙터 확인 필요.");
        }

        if (uiManager != null)
            uiManager.RevealScannedInfo(GetInfectionTypeText(), GetDescriptionText(), GetInventoryStatusText());
    }

    public void OnPurifyClick()
    {
        if (!TryBeginBattleAction("정화"))
            return;

        if (!isScanned)
        {
            Debug.LogWarning("[UIButtonContainer] 탐색이 완료되지 않아 정화할 수 없습니다.");
            EndBattleActionProcessing();
            return;
        }

        string requiredItemId = GetRequiredInventoryItemId();
        if (!HasInventoryItem(requiredItemId))
        {
            string requiredItemName = GetItemName(requiredItemId);
            Debug.LogWarning($"[UIButtonContainer] 정화에 필요한 아이템이 없습니다: {requiredItemId}");

            if (uiManager != null)
                uiManager.RevealScannedInfo(GetInfectionTypeText(), GetDescriptionText(), $"{requiredItemName} 없음");

            EndBattleActionProcessing();
            return;
        }

        int consumeRequest = Mathf.Max(1, purifyDurabilityPerUse);
        if (!TryApplyDurabilityAndContamination(requiredItemId, consumeRequest, out int consumedDurability, out int durabilityBeforeUse))
        {
            EndBattleActionProcessing();
            return;
        }

        if (durabilityBeforeUse > 0 && consumedDurability >= durabilityBeforeUse)
            depletedItemIdThisBattle = requiredItemId;

        Debug.Log($"[UIButtonContainer] 정화 약제를 살포합니다. 소모 내구도: {consumedDurability}");

        if (uiManager != null)
            uiManager.RevealScannedInfo(GetInfectionTypeText(), GetDescriptionText(), GetInventoryStatusText());

        EndBattleActionProcessing();
    }

    public void OnEscapeClick()
    {
        if (isEscaping || isProcessing)
            return;

        if (uiManager == null)
            uiManager = FindAnyObjectByType<UIBattleManager>();

        if (uiManager != null)
        {
            if (!uiManager.TryBeginFleeExit())
                return;

            isEscaping = true;
            isProcessing = true;
            SetBattleActionsLocked(true);
            Debug.Log("[UIButtonContainer] 전투 이탈 시도.");

            ResolveBattleUIController();
            if (battleUIController != null)
                battleUIController.ApplyFleePenaltyOnly();
            else
                Debug.LogWarning("[UIButtonContainer] BattleUIController가 없어 산소 패널티를 생략합니다.");

            uiManager.CompleteFleeExit();
            return;
        }

        if (!TryBeginBattleAction("도망"))
            return;

        isEscaping = true;
        Debug.Log("[UIButtonContainer] 전투 이탈 시도 (UIBattleManager 없음).");
        BattleEncounterContext.MarkFleeExit();
        ExitBattleUI();
    }

    private bool TryBeginBattleAction(string actionName)
    {
        if (isEscaping)
        {
            Debug.Log($"[UIButtonContainer] 도망 처리 중이므로 {actionName}을(를) 실행하지 않습니다.");
            return false;
        }

        if (isProcessing)
        {
            Debug.Log($"[UIButtonContainer] 다른 배틀 액션이 처리 중이므로 {actionName}을(를) 실행하지 않습니다.");
            return false;
        }

        if (!IsBattleActive())
        {
            Debug.Log($"[UIButtonContainer] 전투가 활성 상태가 아니므로 {actionName}을(를) 실행하지 않습니다.");
            return false;
        }

        isProcessing = true;
        SetBattleActionsLocked(true);
        return true;
    }

    private void EndBattleActionProcessing()
    {
        if (isEscaping)
            return;

        isProcessing = false;
        SetBattleActionsLocked(false);
    }

    private bool TryApplyDurabilityAndContamination(
        string itemId,
        int requestedConsume,
        out int consumedDurability,
        out int durabilityBeforeUse)
    {
        consumedDurability = 0;
        durabilityBeforeUse = 0;

        if (InventoryManager.Instance == null || uiManager == null)
        {
            Debug.LogWarning("[UIButtonContainer] InventoryManager 또는 UIBattleManager가 없어 정화를 진행하지 않습니다.");
            return false;
        }

        durabilityBeforeUse = InventoryManager.Instance.GetItemRemainingDurability(itemId);
        consumedDurability = InventoryManager.Instance.ConsumeItemDurability(itemId, requestedConsume);

        if (consumedDurability <= 0)
        {
            Debug.LogWarning("[UIButtonContainer] 아이템 내구도 소모에 실패해 오염도를 변경하지 않습니다.");
            consumedDurability = 0;
            return false;
        }

        uiManager.ReduceContamination(consumedDurability);
        return true;
    }

    private void ExitBattleUI()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.ReturnToField();
        else if (UIManager.Instance != null)
            UIManager.Instance.CloseBattleUI();
    }

    private void OnContaminationCleared()
    {
        isEscaping = true;
        isProcessing = true;
        SetBattleActionsLocked(true);

        if (searchButton != null)
            searchButton.gameObject.SetActive(false);
        if (purifyButton != null)
            purifyButton.gameObject.SetActive(false);
        if (escapeButton != null)
            escapeButton.gameObject.SetActive(false);

        Debug.Log("[UIButtonContainer] 정화 완료 - 모든 버튼 비활성화.");

        if (acquisitionPopup != null)
        {
            string rewardItemId = GetRewardItemId();
            if (DataManager.Instance != null)
                rewardItemId = DataManager.Instance.GetFactoryItemIdForInventory(rewardItemId);

            bool shouldAddRewardToInventory = rewardItemId != depletedItemIdThisBattle;

            acquisitionPopup.gameObject.SetActive(true);
            acquisitionPopup.SetupPopup(rewardItemId, 1, shouldAddRewardToInventory);
        }
        else
        {
            Debug.LogWarning("[UIButtonContainer] acquisitionPopup이 연결되지 않았습니다!");
        }
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

        if (!string.IsNullOrEmpty(monster.infection_type))
            return monster.infection_type;

        return monster.name;
    }

    private string GetDescriptionText()
    {
        MonsterData monster = uiManager != null ? uiManager.GetCurrentMonsterData() : null;
        if (monster == null || string.IsNullOrEmpty(monster.description))
            return "정화 방법 설명";

        return monster.description;
    }

    private string GetInventoryStatusText()
    {
        MonsterData monster = uiManager != null ? uiManager.GetCurrentMonsterData() : null;
        if (monster == null)
            return "필요 아이템 정보 없음";

        string inventoryItemId = GetRequiredInventoryItemId();
        if (string.IsNullOrEmpty(inventoryItemId))
            return "필요 아이템 정보 없음";

        string requiredItemName = GetItemName(inventoryItemId);
        bool hasItem = HasInventoryItem(inventoryItemId);

        if (!hasItem)
            return $"{requiredItemName} 없음";

        int remainingDurability = InventoryManager.Instance != null
            ? InventoryManager.Instance.GetItemRemainingDurability(inventoryItemId)
            : 0;

        return $"{requiredItemName} 보유 {remainingDurability}";
    }

    private string GetRequiredInventoryItemId()
    {
        string rewardItemId = GetRewardItemId();
        if (string.IsNullOrEmpty(rewardItemId))
            return string.Empty;

        return rewardItemId;
    }

    private string GetRewardItemId()
    {
        MonsterData monster = uiManager != null ? uiManager.GetCurrentMonsterData() : null;
        if (monster == null)
            return DefaultRewardItemId;

        if (!string.IsNullOrEmpty(monster.drop_item_id))
            return monster.drop_item_id;

        if (monster.drop_items != null && monster.drop_items.Count > 0)
            return monster.drop_items[0].item_id;

        return DefaultRewardItemId;
    }

    private bool HasInventoryItem(string itemId)
    {
        if (InventoryManager.Instance == null || string.IsNullOrEmpty(itemId))
            return false;

        return InventoryManager.Instance.HasItem(itemId);
    }

    private string GetItemName(string itemId)
    {
        if (DataManager.Instance == null || string.IsNullOrEmpty(itemId))
            return itemId;

        ItemData data = DataManager.Instance.GetItemData(itemId);
        if (data == null || string.IsNullOrEmpty(data.name))
            return itemId;

        return data.name;
    }

    private bool IsBattleActive()
    {
        bool isBattleUiActive = gameObject.activeInHierarchy;

        if (GameManager.Instance == null)
            return isBattleUiActive;

        return GameManager.Instance.CurrentState == GameManager.GameState.Battle || isBattleUiActive;
    }
}
