using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class UIInventory : MonoBehaviour
{
    [Header("--- 스크롤뷰 설정 ---")]
    [SerializeField] private Transform contentParent;               // ScrollView > Viewport > Content
    [SerializeField] private UIInventoryItemSceneView itemPrefab;   // 아이템 슬롯 프리팹

    [Header("--- 닫기 버튼 ---")]
    [SerializeField] private Button closeButton;

    private List<ItemData> itemDataList = new List<ItemData>();
    private List<UIInventoryItemSceneView> spawnedItems = new List<UIInventoryItemSceneView>();

    void Start()
    {
        if (closeButton != null)
            closeButton.onClick.AddListener(Close);
    }

    // 아이템 획득 시 호출 - 리스트에 저장 후 슬롯 생성
    public void AddItem(string itemName, string description, string itemType, Sprite icon = null)
    {
        itemDataList.Add(new ItemData(itemName, description, itemType, icon));
        SpawnItemSlot(itemDataList[itemDataList.Count - 1]);
    }

    // 슬롯 1개 생성
    private void SpawnItemSlot(ItemData data)
    {
        if (itemPrefab == null || contentParent == null)
        {
            Debug.LogError("[UIInventory] itemPrefab 또는 contentParent가 연결되지 않았습니다!");
            return;
        }

        UIInventoryItemSceneView slot = Instantiate(itemPrefab, contentParent);
        slot.Setup(data.itemName, data.description, data.itemType, data.icon);
        spawnedItems.Add(slot);
    }

    // 인벤토리 열기
    public void Open()
    {
        gameObject.SetActive(true);
    }

    // 인벤토리 닫기
    public void Close()
    {
        gameObject.SetActive(false);
    }

    // 아이템 데이터 구조체
    private class ItemData
    {
        public string itemName;
        public string description;
        public string itemType;
        public Sprite icon;

        public ItemData(string itemName, string description, string itemType, Sprite icon)
        {
            this.itemName = itemName;
            this.description = description;
            this.itemType = itemType;
            this.icon = icon;
        }
    }
}
