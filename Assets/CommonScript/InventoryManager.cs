using UnityEngine;
using System.Collections.Generic;
using System;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance { get; private set; }

    // 현재 인벤토리에 들어있는 아이템 ID 리스트
    private List<string> itemIds = new List<string>();

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

        itemIds.Add(id);
        Debug.Log($"[InventoryManager] 아이템 추가됨: {id}");
        OnInventoryChanged?.Invoke();
    }

    // 아이템 제거
    public void RemoveItem(string id)
    {
        if (itemIds.Remove(id))
        {
            Debug.Log($"[InventoryManager] 아이템 제거됨: {id}");
            OnInventoryChanged?.Invoke();
        }
        else
        {
            Debug.LogWarning($"[InventoryManager] 제거할 아이템이 없습니다: {id}");
        }
    }

    // 현재 아이템 ID 리스트 반환 (읽기 전용)
    public IReadOnlyList<string> GetItemIds() => itemIds;
}
