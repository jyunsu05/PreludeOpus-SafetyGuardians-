using UnityEngine;
using UnityEngine.UI;

public class UIButtonContainer : MonoBehaviour
{
    // Canvas에 붙어있는 전체 UI 매니저를 참조하기 위한 변수
    private UIBattleManager uiManager;

    [Header("--- 자식 액션 버튼들 ---")]
    [SerializeField] private Button searchButton;   // 탐색 버튼
    [SerializeField] private Button purifyButton;   // 정화 버튼
    [SerializeField] private Button escapeButton;   // 도망 버튼

    [Header("--- 아이템 획득 팝업 ---")]
    [SerializeField] private UIAcquisitionPopup acquisitionPopup;

    [Header("--- 정화 설정 ---")]
    [SerializeField] private int purifyDurabilityPerUse = 10;

    private bool isScanned = false; // 탐색 완료 여부 판별
    private string depletedItemIdThisBattle;

    private const string DefaultRewardItemId = "MI-101";

    void Start()
    {
        uiManager = FindAnyObjectByType<UIBattleManager>();

        if (uiManager == null)
        {
            Debug.LogError("[UIButtonContainer] 상위 오브젝트에서 BattleUIManager를 찾을 수 없습니다!");
        }
        else
        {
            uiManager.OnContaminationEmpty += OnContaminationCleared;
        }

        ResetButtonsState();
    }

    void OnEnable()
    {
        ResetButtonsState();
    }

    void OnDestroy()
    {
        if (uiManager != null)
            uiManager.OnContaminationEmpty -= OnContaminationCleared;
    }

    // 배틀 시작 시 버튼 상태를 초기화하는 함수
    public void ResetButtonsState()
    {
        isScanned = false;
        depletedItemIdThisBattle = null;

        if (searchButton != null)
        {
            searchButton.gameObject.SetActive(true);
            searchButton.interactable = true;
        }

        if (purifyButton != null)
            purifyButton.gameObject.SetActive(false);

        if (escapeButton != null)
            escapeButton.gameObject.SetActive(true);
    }

    // [탐색] 버튼 클릭 이벤트
    public void OnSearchClick()
    {
        Debug.Log("[UIButtonContainer] OnSearchClick 호출됨.");

        if (isScanned) return; // 중복 실행 방지

        isScanned = true;

        // 버튼 상태 전환
        if (searchButton != null) searchButton.interactable = false;

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

    // [정화] 버튼 클릭 이벤트
    public void OnPurifyClick()
    {
        if (!isScanned)
        {
            Debug.LogWarning("[UIButtonContainer] 탐색이 완료되지 않아 정화할 수 없습니다.");
            return;
        }

        string requiredItemId = GetRequiredInventoryItemId();
        if (!HasInventoryItem(requiredItemId))
        {
            string requiredItemName = GetItemName(requiredItemId);
            Debug.LogWarning($"[UIButtonContainer] 정화에 필요한 아이템이 없습니다: {requiredItemId}");

            if (uiManager != null)
                uiManager.RevealScannedInfo(GetInfectionTypeText(), GetDescriptionText(), $"{requiredItemName} 없음");

            return;
        }

        int consumeRequest = Mathf.Max(1, purifyDurabilityPerUse);
        int durabilityBeforeUse = InventoryManager.Instance != null
            ? InventoryManager.Instance.GetItemRemainingDurability(requiredItemId)
            : 0;

        int consumedDurability = InventoryManager.Instance != null
            ? InventoryManager.Instance.ConsumeItemDurability(requiredItemId, consumeRequest)
            : 0;

        if (consumedDurability <= 0)
        {
            Debug.LogWarning("[UIButtonContainer] 아이템 내구도 소모에 실패해 정화를 진행하지 않습니다.");
            return;
        }

        if (durabilityBeforeUse > 0 && consumedDurability >= durabilityBeforeUse)
            depletedItemIdThisBattle = requiredItemId;

        Debug.Log($"[UIButtonContainer] 정화 약제를 살포합니다. 소모 내구도: {consumedDurability}");

        if (uiManager != null)
        {
            uiManager.ReduceContamination(consumedDurability);
            uiManager.RevealScannedInfo(GetInfectionTypeText(), GetDescriptionText(), GetInventoryStatusText());
        }
    }

    // [도망] 버튼 클릭 이벤트
    public void OnEscapeClick()
    {
        Debug.Log("[UIButtonContainer] 전투 이탈 시도.");

        ExitBattleUI();
    }

    private void ExitBattleUI()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.ReturnToField();
        else if (UIManager.Instance != null)
            UIManager.Instance.CloseBattleUI();
    }

    // 오염도 0 도달 시 호출 - 모든 버튼 비활성화
    private void OnContaminationCleared()
    {
        if (searchButton != null) searchButton.gameObject.SetActive(false);
        if (purifyButton != null) purifyButton.gameObject.SetActive(false);
        if (escapeButton != null) escapeButton.gameObject.SetActive(false);

        Debug.Log("[UIButtonContainer] 정화 완료 - 모든 버튼 비활성화.");

        if (acquisitionPopup != null)
        {
            string rewardItemId = GetRewardItemId();
            if (DataManager.Instance != null)
                rewardItemId = DataManager.Instance.GetFactoryItemIdForInventory(rewardItemId);

            bool shouldAddRewardToInventory = rewardItemId != depletedItemIdThisBattle;

            acquisitionPopup.gameObject.SetActive(true);
            acquisitionPopup.SetupPopup(rewardItemId, 1, shouldAddRewardToInventory); // TODO: 실제 몬스터 드롭 데이터로 교체
        }
        else
        {
            Debug.LogWarning("[UIButtonContainer] acquisitionPopup이 연결되지 않았습니다!");
        }
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

        // 배틀 정화 소모는 몬스터 정화 아이템(MI) 기준으로 처리합니다.
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
}