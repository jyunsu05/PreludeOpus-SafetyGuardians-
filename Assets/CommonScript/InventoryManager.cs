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

        return items.Exists(x => x.id == id);
    }

    public int GetItemRemainingDurability(string id)
    {
        int index = items.FindIndex(x => x.id == id);
        if (index < 0)
            return 0;

        return items[index].remainingDurability;
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
