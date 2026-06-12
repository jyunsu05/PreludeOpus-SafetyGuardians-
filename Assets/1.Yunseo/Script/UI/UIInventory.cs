using System;
using System.Collections.Generic;
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
    private bool battleActionPanelHiddenByInventory;
    private readonly List<UIInventoryItemSceneView> spawnedSlots = new List<UIInventoryItemSceneView>();

    private readonly struct InventorySlotEntry
    {
        public readonly string useItemId;
        public readonly ItemData displayData;
        public readonly int count;

        public InventorySlotEntry(string useItemId, ItemData displayData, int count)
        {
            this.useItemId = useItemId;
            this.displayData = displayData;
            this.count = count;
        }
    }

    void Start()
    {
        EnsureReferences();
        ClearLegacyContentChildren();

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

        if (InventoryManager.Instance != null)
            RefreshUI();
    }

    void OnDisable()
    {
        RestoreBattleActionPanelIfHidden();
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

    public static void ClearBattleOverlayTracking()
    {
        UIInventory[] inventories = FindObjectsByType<UIInventory>(FindObjectsInactive.Include);
        for (int i = 0; i < inventories.Length; i++)
        {
            if (inventories[i] != null)
                inventories[i].battleActionPanelHiddenByInventory = false;
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
            HideAllSlots();
            if (InventoryManager.Instance == null)
                Debug.LogError("[UIInventory] InventoryManager가 씬에 없습니다. 캔버스 밖 빈 오브젝트에 InventoryManager 스크립트를 붙여주세요.");
            if (DataManager.Instance == null)
                Debug.LogError("[UIInventory] DataManager가 씬에 없습니다. 캔버스 밖 빈 오브젝트에 DataManager 스크립트를 붙이고 JSON 파일을 연결해주세요.");
            return;
        }

        HideAllSlots();

        int slotIndex = 0;
        foreach (InventorySlotEntry entry in BuildVisibleSlotEntries())
        {
            if (entry.displayData == null)
            {
                Debug.LogWarning($"[UIInventory] ID {entry.useItemId}에 해당하는 아이템 데이터가 없습니다.");
                continue;
            }

            BindSlot(slotIndex, entry.useItemId, entry.displayData, entry.count);
            slotIndex++;
        }

        if (UIButtonClickSoundPlayer.Instance != null)
            UIButtonClickSoundPlayer.Instance.RegisterButtonsInHierarchy(transform);
    }

    private IEnumerable<InventorySlotEntry> BuildVisibleSlotEntries()
    {
        IReadOnlyList<InventoryManager.StackedInventoryItem> stackedItems =
            InventoryManager.Instance.GetStackedItems();
        bool filterForBattle = IsBattleInventoryContext();

        for (int i = 0; i < stackedItems.Count; i++)
        {
            InventoryManager.StackedInventoryItem stackedItem = stackedItems[i];
            if (string.IsNullOrEmpty(stackedItem.itemId) || stackedItem.count <= 0)
                continue;

            if (filterForBattle && !DataManager.Instance.IsBattleInventoryItem(stackedItem.itemId))
                continue;

            ItemData data = DataManager.Instance.GetItemData(stackedItem.itemId);
            if (data == null)
                continue;

            yield return new InventorySlotEntry(stackedItem.itemId, data, stackedItem.count);
        }
    }

    private void BindSlot(int slotIndex, string itemId, ItemData data, int count)
    {
        UIInventoryItemSceneView slot = GetOrCreateSlot(slotIndex);
        if (slot == null)
            return;

        slot.Setup(itemId, data.name, data.description, GetItemTypeLabel(data), GetItemSprite(data), count);

        if (IsBattleInventoryContext())
            slot.ConfigureBattleUse(HandleBattleItemUseRequest, CanUseBattleItemNow);
        else
            slot.ClearBattleUse();
    }

    private UIInventoryItemSceneView GetOrCreateSlot(int slotIndex)
    {
        UIInventoryItemSceneView slotPrefab = GetSlotPrefab();
        if (slotPrefab == null || contentParent == null)
        {
            Debug.LogError($"[UIInventory] itemSceneViews 또는 contentParent가 연결되지 않았습니다! ({gameObject.name})");
            return null;
        }

        while (spawnedSlots.Count <= slotIndex)
        {
            UIInventoryItemSceneView slot = Instantiate(slotPrefab, contentParent);
            slot.gameObject.name = $"{slotPrefab.gameObject.name}_{spawnedSlots.Count + 1}";
            spawnedSlots.Add(slot);
        }

        UIInventoryItemSceneView existingSlot = spawnedSlots[slotIndex];
        existingSlot.gameObject.SetActive(true);
        return existingSlot;
    }

    private UIInventoryItemSceneView GetSlotPrefab()
    {
        UIInventoryItemSceneView[] prefabCandidates = GetItemViewCandidates();
        if (prefabCandidates == null || prefabCandidates.Length == 0)
            return null;

        for (int i = 0; i < prefabCandidates.Length; i++)
        {
            UIInventoryItemSceneView candidate = prefabCandidates[i];
            if (candidate == null)
                continue;

            if (contentParent != null && candidate.transform.IsChildOf(contentParent))
                continue;

            return candidate;
        }

        return null;
    }

    private void HideAllSlots()
    {
        for (int i = 0; i < spawnedSlots.Count; i++)
        {
            if (spawnedSlots[i] != null)
                spawnedSlots[i].gameObject.SetActive(false);
        }
    }

    private bool IsBattleInventoryContext()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsInBattle)
            return true;

        if (UIManager.Instance != null && UIManager.Instance.IsBattleUiVisible())
            return true;

        if (ResolveBattleManager() != null)
            return true;

        BattleTurnController turnController =
            FindAnyObjectByType<BattleTurnController>(FindObjectsInactive.Include);
        return turnController != null &&
               turnController.isActiveAndEnabled &&
               turnController.gameObject.activeInHierarchy;
    }

    private bool IsBattleItemUseEnabled() => IsBattleInventoryContext();

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

        bool isPurifyItem = IsMonsterPurifyBattleItem(itemId, battleManager);
        if (!isPurifyItem)
            BattleAutoManager.Instance?.BlockAutoTurnForManualAction();

        if (!battleManager.UseItem(itemId))
            return;

        if (isPurifyItem)
            BattleAutoManager.Instance?.EngageAutoBattleAfterManualPurify();

        if (battleManager.IsScanned)
        {
            battleManager.RevealScannedInfo(
                battleManager.GetInfectionTypeDisplayText(),
                battleManager.GetDescriptionDisplayText(),
                battleManager.BuildInventoryStatusText());
        }
    }

    private static bool IsMonsterPurifyBattleItem(string itemId, UIBattleManager battleManager)
    {
        if (string.IsNullOrEmpty(itemId) || battleManager == null || DataManager.Instance == null)
            return false;

        if (!DataManager.Instance.IsMonsterPurificationItem(itemId))
            return false;

        string requiredItemId = battleManager.GetRequiredPurifyItemId();
        if (InventoryManager.Instance != null)
            return InventoryManager.Instance.IsConsumableForRequirement(itemId, requiredItemId);

        return string.Equals(itemId, requiredItemId, StringComparison.Ordinal);
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
        if (!IsBattleInventoryContext())
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
            UIInventoryItemSceneView template = FindSlotTemplateOutsideContent();
            if (template != null)
            {
                itemSceneViews = new[] { template };
                itemPrefabs = itemSceneViews;
            }
        }
    }

    private UIInventoryItemSceneView FindSlotTemplateOutsideContent()
    {
        UIInventoryItemSceneView[] candidates = GetComponentsInChildren<UIInventoryItemSceneView>(true);
        if (candidates == null)
            return null;

        for (int i = 0; i < candidates.Length; i++)
        {
            UIInventoryItemSceneView candidate = candidates[i];
            if (candidate == null)
                continue;

            if (contentParent != null && candidate.transform.IsChildOf(contentParent))
                continue;

            if (spawnedSlots.Contains(candidate))
                continue;

            return candidate;
        }

        return null;
    }

    private void ClearLegacyContentChildren()
    {
        if (contentParent == null)
            return;

        for (int i = contentParent.childCount - 1; i >= 0; i--)
        {
            Transform child = contentParent.GetChild(i);
            UIInventoryItemSceneView slotView = child.GetComponent<UIInventoryItemSceneView>();
            if (slotView != null && spawnedSlots.Contains(slotView))
                continue;

            Destroy(child.gameObject);
        }
    }

    public void Open()
    {
        if (IsBattleInventoryContext())
        {
            UIButtonContainer.SetAllBattleActionPanelsVisible(false);
            battleActionPanelHiddenByInventory = true;
        }

        gameObject.SetActive(true);
        RefreshUI(); // 열 때마다 최신 상태 반영
    }

    public void Close()
    {
        RestoreBattleActionPanelIfHidden();
        gameObject.SetActive(false);
    }

    private void RestoreBattleActionPanelIfHidden()
    {
        if (!battleActionPanelHiddenByInventory)
            return;

        UIButtonContainer.SetAllBattleActionPanelsVisible(true);
        battleActionPanelHiddenByInventory = false;
    }
}
