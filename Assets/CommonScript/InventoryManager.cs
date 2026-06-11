using UnityEngine;
using System.Collections.Generic;
using System;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance { get; private set; }

    public readonly struct StackedInventoryItem
    {
        public readonly string itemId;
        public readonly int count;

        public StackedInventoryItem(string itemId, int count)
        {
            this.itemId = itemId;
            this.count = count;
        }
    }

    private class InventoryItemInstance
    {
        public string id;
        public int remainingDurability;
    }

    // 현재 인벤토리에 들어있는 아이템 인스턴스 리스트
    private readonly List<InventoryItemInstance> items = new List<InventoryItemInstance>();
    private readonly List<string> itemIdSnapshot = new List<string>();
    private readonly List<StackedInventoryItem> stackedSnapshot = new List<StackedInventoryItem>();

    // 인벤토리 변화 시 UI에 알리는 이벤트
    public event Action OnInventoryChanged;

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
        }
    }

    // 아이템 추가
    public void AddItem(string id)
    {
        if (DataManager.Instance == null)
        {
            Debug.LogError("[InventoryManager] DataManager가 초기화되지 않았습니다!");
            return;
        }

        if (!DataManager.Instance.HasItem(id))
        {
            Debug.LogError($"[InventoryManager] 존재하지 않는 아이템 ID: {id}");
            return;
        }

        ItemData data = DataManager.Instance.GetItemData(id);
        int durability = 1;
        if (data != null && data.durability > 0)
            durability = data.durability;

        items.Add(new InventoryItemInstance
        {
            id = id,
            remainingDurability = durability
        });

        Debug.Log($"[InventoryManager] 아이템 추가됨: {id} (내구도 {durability})");
        PlaySessionStats.EnsureInstance()?.RecordSessionItem(id);
        OnInventoryChanged?.Invoke();
    }

    // 아이템 제거
    public void RemoveItem(string id)
    {
        int index = items.FindIndex(x => x.id == id);
        if (index >= 0)
        {
            items.RemoveAt(index);
            Debug.Log($"[InventoryManager] 아이템 제거됨: {id}");
            OnInventoryChanged?.Invoke();
        }
        else
        {
            Debug.LogWarning($"[InventoryManager] 제거할 아이템이 없습니다: {id}");
        }
    }

    public bool HasItem(string id)
    {
        if (string.IsNullOrEmpty(id))
            return false;

        if (items.Exists(x => x.id == id))
            return true;

        return GetEquivalentInventoryCount(id) > 0;
    }

    public int GetItemRemainingDurability(string id)
    {
        int index = items.FindIndex(x => x.id == id);
        if (index < 0)
            return 0;

        return items[index].remainingDurability;
    }

    public int GetItemCount(string id)
    {
        if (string.IsNullOrEmpty(id))
            return 0;

        int count = 0;
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i].id == id)
                count++;
        }

        return count;
    }

    public bool HasBattleConsumableForRequirement(string requiredMonsterItemId)
    {
        return FindBattleConsumableIndex(requiredMonsterItemId) >= 0;
    }

    public int GetBattleConsumableCount(string requiredMonsterItemId)
        => GetEquivalentInventoryCount(requiredMonsterItemId);

    /// <summary>배틀 탐색 UI용 — 몬스터 정화 아이템(MI) 보유 수만 집계합니다. 공장 정화(FI)는 제외합니다.</summary>
    public int GetMonsterPurificationItemCount(string monsterItemId = null)
    {
        if (DataManager.Instance == null)
            return 0;

        if (!string.IsNullOrEmpty(monsterItemId))
        {
            if (!DataManager.Instance.IsMonsterPurificationItem(monsterItemId))
                return 0;

            return GetItemCount(monsterItemId);
        }

        int total = 0;
        for (int i = 0; i < items.Count; i++)
        {
            if (DataManager.Instance.IsMonsterPurificationItem(items[i].id))
                total++;
        }

        return total;
    }

    public bool IsConsumableForRequirement(string consumableItemId, string requiredMonsterItemId)
    {
        if (string.IsNullOrEmpty(consumableItemId) || string.IsNullOrEmpty(requiredMonsterItemId))
            return false;

        if (!IsSameInventoryItemGroup(consumableItemId, requiredMonsterItemId))
            return false;

        return GetEquivalentInventoryCount(requiredMonsterItemId) > 0;
    }

    /// <summary>
    /// 몬스터 요구 아이템(MI) 또는 대응 공장 아이템(FI) 중 인벤토리 첫 번째 1개만 소모합니다.
    /// </summary>
    public bool TryConsumeBattleItemForRequirement(string requiredMonsterItemId, out int effectPower, out string consumedItemId)
    {
        effectPower = 0;
        consumedItemId = null;

        int index = FindBattleConsumableIndex(requiredMonsterItemId);
        if (index < 0)
            return false;

        int countBefore = items.Count;
        InventoryItemInstance item = items[index];
        consumedItemId = item.id;
        effectPower = Mathf.Max(1, ResolveBattleEffectPower(item));
        items.RemoveAt(index);

        Debug.Log(
            $"[InventoryManager] 배틀 아이템 1개 소모: {consumedItemId} " +
            $"(요구 {requiredMonsterItemId}, 효과 {effectPower}, 남은 슬롯 {items.Count}/{countBefore - 1})");
        OnInventoryChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// 배틀씬 1회성 소모. 지정 ID의 첫 번째 인스턴스 1개만 제거합니다.
    /// </summary>
    public bool TryConsumeBattleItem(string id, out int effectPower)
    {
        effectPower = 0;

        if (string.IsNullOrEmpty(id))
            return false;

        int index = items.FindIndex(x => x.id == id);
        if (index < 0)
            return false;

        int countBefore = items.Count;
        InventoryItemInstance item = items[index];
        effectPower = Mathf.Max(1, ResolveBattleEffectPower(item));
        items.RemoveAt(index);

        Debug.Log(
            $"[InventoryManager] 배틀 아이템 1개 소모: {id} " +
            $"(효과 {effectPower}, 남은 슬롯 {items.Count}/{countBefore - 1})");
        OnInventoryChanged?.Invoke();
        return true;
    }

    private static bool IsSameInventoryItemGroup(string leftId, string rightId)
    {
        if (string.IsNullOrEmpty(leftId) || string.IsNullOrEmpty(rightId))
            return false;

        if (string.Equals(leftId, rightId, StringComparison.Ordinal))
            return true;

        if (DataManager.Instance == null)
            return false;

        string leftCanonical = DataManager.Instance.GetCanonicalInventoryItemId(leftId);
        string rightCanonical = DataManager.Instance.GetCanonicalInventoryItemId(rightId);
        return string.Equals(leftCanonical, rightCanonical, StringComparison.Ordinal);
    }

    private int GetEquivalentInventoryCount(string itemId)
    {
        if (string.IsNullOrEmpty(itemId))
            return 0;

        int count = 0;
        for (int i = 0; i < items.Count; i++)
        {
            if (IsSameInventoryItemGroup(items[i].id, itemId))
                count++;
        }

        return count;
    }

    private int FindBattleConsumableIndex(string requiredMonsterItemId)
    {
        if (string.IsNullOrEmpty(requiredMonsterItemId))
            return -1;

        for (int i = 0; i < items.Count; i++)
        {
            if (IsSameInventoryItemGroup(items[i].id, requiredMonsterItemId))
                return i;
        }

        return -1;
    }

    private int ResolveBattleEffectPower(InventoryItemInstance item)
    {
        if (item == null)
            return 0;

        if (item.remainingDurability > 0)
            return item.remainingDurability;

        if (DataManager.Instance == null || string.IsNullOrEmpty(item.id))
            return 0;

        ItemData data = DataManager.Instance.GetItemData(item.id);
        if (data != null && data.durability > 0)
            return data.durability;

        return 1;
    }

    // amount만큼 내구도를 소모하고 실제 소모량을 반환합니다.
    public int ConsumeItemDurability(string id, int amount)
    {
        if (string.IsNullOrEmpty(id) || amount <= 0)
            return 0;

        int index = items.FindIndex(x => x.id == id);
        if (index < 0)
            return 0;

        InventoryItemInstance item = items[index];
        int consumed = Mathf.Min(amount, item.remainingDurability);
        item.remainingDurability -= consumed;

        if (item.remainingDurability <= 0)
        {
            items.RemoveAt(index);
            Debug.Log($"[InventoryManager] 아이템 소모 완료: {id}");
        }
        else
        {
            Debug.Log($"[InventoryManager] 아이템 내구도 소모: {id} (-{consumed}), 남은 내구도 {item.remainingDurability}");
        }

        OnInventoryChanged?.Invoke();
        return consumed;
    }

    // 현재 아이템 ID 리스트 반환 (읽기 전용)
    public IReadOnlyList<string> GetItemIds()
    {
        itemIdSnapshot.Clear();
        foreach (InventoryItemInstance item in items)
            itemIdSnapshot.Add(item.id);

        return itemIdSnapshot;
    }

    /// <summary>동일 Item ID를 하나로 묶은 스택 목록을 반환합니다.</summary>
    public IReadOnlyList<StackedInventoryItem> GetStackedItems()
    {
        stackedSnapshot.Clear();

        if (items.Count == 0)
            return stackedSnapshot;

        var countsById = new Dictionary<string, int>();
        var orderedIds = new List<string>();

        foreach (InventoryItemInstance item in items)
        {
            if (countsById.ContainsKey(item.id))
            {
                countsById[item.id]++;
                continue;
            }

            countsById[item.id] = 1;
            orderedIds.Add(item.id);
        }

        foreach (string id in orderedIds)
            stackedSnapshot.Add(new StackedInventoryItem(id, countsById[id]));

        return stackedSnapshot;
    }

    /// <summary>인벤토리의 모든 아이템을 비우고 UI를 갱신합니다.</summary>
    public void ClearInventory()
    {
        int removedCount = items.Count;
        items.Clear();
        itemIdSnapshot.Clear();

        if (removedCount > 0)
            Debug.Log($"[InventoryManager] 인벤토리 초기화 — {removedCount}개 아이템 제거");

        OnInventoryChanged?.Invoke();
    }

    public void ResetAll()
    {
        ClearInventory();
    }
}
