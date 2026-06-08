using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;

public class UIInventory : MonoBehaviour
{
    [Header("--- 스크롤뷰 설정 ---")]
    [FormerlySerializedAs("content")]
    [SerializeField] private Transform contentParent;
    [FormerlySerializedAs("itemPrefabs")]
    [SerializeField] private UIInventoryItemSceneView[] itemSceneViews;
    [SerializeField] private UIInventoryItemSceneView[] itemPrefabs; // Legacy scene/prefab compatibility

    [Header("--- 닫기 버튼 ---")]
    [SerializeField] private Button closeButton;

    private BattleTurnController subscribedTurnController;

    void Start()
    {
        EnsureReferences();

        if (closeButton != null)
            closeButton.onClick.AddListener(Close);

        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.OnInventoryChanged += RefreshUI;
            RefreshUI();
        }
        else
        {
            Debug.LogError("[UIInventory] InventoryManager를 찾을 수 없습니다!");
        }

        SubscribeBattleTurnEvents();
    }

    void OnEnable()
    {
        SubscribeBattleTurnEvents();
    }

    void OnDestroy()
    {
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnInventoryChanged -= RefreshUI;

        UnsubscribeBattleTurnEvents();
    }

    public static void RefreshAllVisible()
    {
        UIInventory[] inventories = FindObjectsByType<UIInventory>(FindObjectsInactive.Include);
        for (int i = 0; i < inventories.Length; i++)
        {
            UIInventory inventory = inventories[i];
            if (inventory != null && inventory.isActiveAndEnabled)
                inventory.RefreshUI();
        }
    }

    // InventoryManager 이벤트 수신 시 전체 슬롯 갱신
    public void RefreshUI()
    {
        EnsureReferences();

        if (contentParent == null)
            return;

        if (InventoryManager.Instance == null || DataManager.Instance == null)
        {
            ClearSlots();
            if (InventoryManager.Instance == null)
                Debug.LogError("[UIInventory] InventoryManager가 씬에 없습니다. 캔버스 밖 빈 오브젝트에 InventoryManager 스크립트를 붙여주세요.");
            if (DataManager.Instance == null)
                Debug.LogError("[UIInventory] DataManager가 씬에 없습니다. 캔버스 밖 빈 오브젝트에 DataManager 스크립트를 붙이고 JSON 파일을 연결해주세요.");
            return;
        }

        ClearSlots();

        foreach (string id in InventoryManager.Instance.GetItemIds())
        {
            if (string.IsNullOrEmpty(id))
                continue;

            ItemData data = DataManager.Instance.GetItemData(id);
            if (data == null)
            {
                Debug.LogWarning($"[UIInventory] ID {id}에 해당하는 아이템 데이터가 없습니다.");
                continue;
            }

            SpawnSlot(id, data);
        }
    }

    // 슬롯 1개 생성
    private void SpawnSlot(string itemId, ItemData data)
    {
        UIInventoryItemSceneView[] prefabCandidates = GetItemViewCandidates();

        if (prefabCandidates == null || prefabCandidates.Length == 0 || contentParent == null)
        {
            Debug.LogError($"[UIInventory] itemSceneViews 또는 contentParent가 연결되지 않았습니다! ({gameObject.name})");
            return;
        }

        // TODO: 나중에 아이템 타입별로 프리팹 선택 로직 추가 예정
        UIInventoryItemSceneView prefab = prefabCandidates[0];
        UIInventoryItemSceneView slot = Instantiate(prefab, contentParent);
        slot.Setup(itemId, data.name, data.description, GetItemTypeLabel(data), GetItemSprite(data));

        if (IsBattleItemUseEnabled())
            slot.ConfigureBattleUse(HandleBattleItemUseRequest, CanUseBattleItemNow);
        else
            slot.ClearBattleUse();
    }

    private bool IsBattleItemUseEnabled()
    {
        return GameManager.Instance != null && GameManager.Instance.IsInBattle;
    }

    private bool CanUseBattleItemNow()
    {
        UIBattleManager battleManager = ResolveBattleManager();
        return battleManager != null && battleManager.CanAcceptPlayerBattleAction();
    }

    private void HandleBattleItemUseRequest(string itemId)
    {
        if (string.IsNullOrEmpty(itemId))
            return;

        UIBattleManager battleManager = ResolveBattleManager();
        if (battleManager == null)
        {
            Debug.LogWarning("[UIInventory] 배틀 중 아이템 사용 — UIBattleManager를 찾지 못했습니다.");
            return;
        }

        if (!battleManager.UseItem(itemId))
            return;

        if (battleManager.IsScanned)
        {
            battleManager.RevealScannedInfo(
                battleManager.GetInfectionTypeDisplayText(),
                battleManager.GetDescriptionDisplayText(),
                battleManager.BuildInventoryStatusText());
        }
    }

    private UIBattleManager ResolveBattleManager()
    {
        UIBattleManager primary = UIBattleManager.TryGetPrimaryActive();
        if (primary != null)
            return primary;

        UIButtonContainer buttonContainer = FindAnyObjectByType<UIButtonContainer>(FindObjectsInactive.Include);
        if (buttonContainer != null)
        {
            UIBattleManager fromContainer = buttonContainer.GetComponentInParent<UIBattleManager>();
            if (fromContainer != null && fromContainer.enabled)
                return fromContainer;
        }

        return null;
    }

    private void SubscribeBattleTurnEvents()
    {
        if (!IsBattleItemUseEnabled())
            return;

        UIBattleManager battleManager = ResolveBattleManager();
        BattleTurnController turnController = battleManager != null ? battleManager.TurnController : null;
        if (turnController == null)
            turnController = FindAnyObjectByType<BattleTurnController>(FindObjectsInactive.Include);

        if (turnController == null || turnController == subscribedTurnController)
            return;

        UnsubscribeBattleTurnEvents();
        subscribedTurnController = turnController;
        subscribedTurnController.OnTurnPhaseChanged += HandleBattleTurnPhaseChanged;
    }

    private void UnsubscribeBattleTurnEvents()
    {
        if (subscribedTurnController == null)
            return;

        subscribedTurnController.OnTurnPhaseChanged -= HandleBattleTurnPhaseChanged;
        subscribedTurnController = null;
    }

    private void HandleBattleTurnPhaseChanged(BattleTurnController.BattleTurnPhase phase)
    {
        if (!isActiveAndEnabled)
            return;

        RefreshUI();
    }

    private UIInventoryItemSceneView[] GetItemViewCandidates()
    {
        if (itemSceneViews != null && itemSceneViews.Length > 0)
            return itemSceneViews;

        if (itemPrefabs != null && itemPrefabs.Length > 0)
            return itemPrefabs;

        return null;
    }

    private string GetItemTypeLabel(ItemData data)
    {
        if (data != null && !string.IsNullOrEmpty(data.item_type))
            return data.item_type;

        return "아이템";
    }

    private Sprite GetItemSprite(ItemData data)
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

    private void EnsureReferences()
    {
        if ((itemSceneViews == null || itemSceneViews.Length == 0) && itemPrefabs != null && itemPrefabs.Length > 0)
            itemSceneViews = itemPrefabs;

        if ((itemPrefabs == null || itemPrefabs.Length == 0) && itemSceneViews != null && itemSceneViews.Length > 0)
            itemPrefabs = itemSceneViews;

        if (contentParent == null)
        {
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Content")
                {
                    contentParent = t;
                    break;
                }
            }
        }

        if ((itemSceneViews == null || itemSceneViews.Length == 0) && (itemPrefabs == null || itemPrefabs.Length == 0))
        {
            UIInventoryItemSceneView[] candidates = GetComponentsInChildren<UIInventoryItemSceneView>(true);
            if (candidates != null && candidates.Length > 0)
            {
                itemSceneViews = new[] { candidates[0] };
                itemPrefabs = itemSceneViews;
            }
        }
    }

    // 모든 슬롯 제거
    private void ClearSlots()
    {
        if (contentParent == null) return;

        foreach (Transform child in contentParent)
            Destroy(child.gameObject);
    }

    public void Open()
    {
        gameObject.SetActive(true);
        RefreshUI(); // 열 때마다 최신 상태 반영
    }

    public void Close() => gameObject.SetActive(false);
}
