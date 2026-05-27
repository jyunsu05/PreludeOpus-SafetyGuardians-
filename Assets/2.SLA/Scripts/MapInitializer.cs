using UnityEngine;
using UnityEngine.Tilemaps;

[ExecuteAlways]
public class MapInitializer : MonoBehaviour
{
    [Header("Tilemap Reference")]
    [SerializeField] private Tilemap obstacleTilemap;

    private void Start()
    {
        ValidateAndConfigureMap();
    }

    private void ValidateAndConfigureMap()
    {
        if (obstacleTilemap == null)
        {
            Debug.LogWarning("[지도 경고] Obstacle Tilemap 참조가 유실되었습니다. 인스펙터 창을 확인하세요.");
            return;
        }

        // 1. TilemapCollider2D 컴포넌트 검증 및 추가
        if (!obstacleTilemap.TryGetComponent<TilemapCollider2D>(out var tilemapCollider))
        {
            tilemapCollider = obstacleTilemap.gameObject.AddComponent<TilemapCollider2D>();
        }

        // 2. CompositeCollider2D 컴포넌트 검증 및 추가
        if (!obstacleTilemap.TryGetComponent<CompositeCollider2D>(out var compositeCollider))
        {
            compositeCollider = obstacleTilemap.gameObject.AddComponent<CompositeCollider2D>();
        }

        // 3. [유니티 6 반영] 부드러운 물리 병합을 위해 Composite Operation을 'Merge(합치기)'로 설정합니다.
        if (tilemapCollider.compositeOperation != Collider2D.CompositeOperation.Merge)
        {
            tilemapCollider.compositeOperation = Collider2D.CompositeOperation.Merge;
            Debug.Log("[지도 세팅] 유니티 6 대응 - Composite Operation이 'Merge'로 세팅되었습니다. (끼임 방지)");
        }

        // 4. Rigidbody2D 검증 및 Body Type 강제 고정
        if (obstacleTilemap.TryGetComponent<Rigidbody2D>(out var rb))
        {
            if (rb.bodyType != RigidbodyType2D.Static)
            {
                rb.bodyType = RigidbodyType2D.Static;
                Debug.Log("[지도 세팅] Rigidbody2D Body Type을 Static으로 안전하게 변경했습니다.");
            }
        }
    }
}