using UnityEngine;

/// <summary>
/// 맵에 배치된 아이템 오브젝트에 붙이는 스크립트.
/// 플레이어가 접촉하면 인벤토리에 아이템을 추가하고 오브젝트를 제거합니다.
/// 
/// [Inspector 설정 방법]
/// 1. Item Id : JSON에 정의된 아이템 ID 입력 (예: "MI-101", "MI-102", "MI-201")
/// 2. 이 스크립트가 붙은 오브젝트에 CircleCollider2D (또는 BoxCollider2D) 추가 → Is Trigger 체크
/// </summary>
public class ItemPickup : MonoBehaviour
{
    [Header("아이템 데이터")]
    [Tooltip("monster_items.json 에 정의된 아이템 ID (예: MI-101)")]
    [SerializeField] private string itemId;

    private void Start()
    {
        // 씬 시작 시 ID가 유효한지 미리 검증 (잘못된 설정을 빠르게 발견하기 위함)
        if (string.IsNullOrEmpty(itemId))
        {
            Debug.LogError($"[ItemPickup] '{gameObject.name}' 에 Item Id가 설정되지 않았습니다!", gameObject);
            return;
        }

        if (DataManager.Instance != null && !DataManager.Instance.HasItem(itemId))
        {
            Debug.LogError($"[ItemPickup] '{itemId}' 는 DataManager에 존재하지 않는 ID입니다. JSON 파일을 확인하세요!", gameObject);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 플레이어 태그를 가진 오브젝트와 충돌했을 때만 처리
        if (!other.CompareTag("Player")) return;

        if (InventoryManager.Instance == null)
        {
            Debug.LogError("[ItemPickup] InventoryManager를 찾을 수 없습니다!");
            return;
        }

        // 인벤토리에 아이템 추가
        InventoryManager.Instance.AddItem(itemId);

        // 맵에서 아이템 오브젝트 제거 (먹힘 처리)
        Destroy(gameObject);
    }
}
