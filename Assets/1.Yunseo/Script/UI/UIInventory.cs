using UnityEngine;
using UnityEngine.UI;

public class UIInventory : MonoBehaviour
{
    [Header("--- 스크롤뷰 설정 ---")]
    [SerializeField] private Transform contentParent;
    [SerializeField] private UIInventoryItemSceneView[] itemPrefabs;

    [Header("--- 닫기 버튼 ---")]
    [SerializeField] private Button closeButton;

    void Start()
    {
        if (closeButton != null)
            closeButton.onClick.AddListener(Close);

        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.OnInventoryChanged += RefreshUI;
            RefreshUI(); // 시작 시 현재 인벤토리 상태 반영
        }
        else
        {
            Debug.LogError("[UIInventory] InventoryManager를 찾을 수 없습니다!");
        }
    }

    void OnDestroy()
    {
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnInventoryChanged -= RefreshUI;
    }

    // InventoryManager 이벤트 수신 시 전체 슬롯 갱신
    private void RefreshUI()
    {
        if (InventoryManager.Instance == null || DataManager.Instance == null)
        {
            if (InventoryManager.Instance == null)
                Debug.LogError("[UIInventory] InventoryManager가 씬에 없습니다. 캔버스 밖 빈 오브젝트에 InventoryManager 스크립트를 붙여주세요.");
            if (DataManager.Instance == null)
                Debug.LogError("[UIInventory] DataManager가 씬에 없습니다. 캔버스 밖 빈 오브젝트에 DataManager 스크립트를 붙이고 JSON 파일을 연결해주세요.");
            return;
        }

        ClearSlots();

        foreach (string id in InventoryManager.Instance.GetItemIds())
        {
            ItemData data = DataManager.Instance.GetItemData(id);

            if (data == null)
            {
                Debug.LogWarning($"[UIInventory] ID {id}에 해당하는 아이템 데이터가 없습니다.");
                continue;
            }

            SpawnSlot(data);
        }
    }

    // 슬롯 1개 생성
    private void SpawnSlot(ItemData data)
    {
        if (itemPrefabs == null || itemPrefabs.Length == 0 || contentParent == null)
        {
            Debug.LogError("[UIInventory] itemPrefabs 또는 contentParent가 연결되지 않았습니다!");
            return;
        }

        // TODO: 나중에 아이템 타입별로 프리팹 선택 로직 추가 예정
        UIInventoryItemSceneView prefab = itemPrefabs[0];
        UIInventoryItemSceneView slot = Instantiate(prefab, contentParent);
        slot.Setup(data.name, data.description, "아이템");
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
