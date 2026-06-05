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

        if (Application.isPlaying && !IsUnderFactoryStage())
            RefreshCompositeColliderGeometry();
    }

    private void OnEnable()
    {
        if (!Application.isPlaying || IsUnderFactoryStage())
            return;

        ValidateAndConfigureMap();
        RefreshCompositeColliderGeometry();
    }

    /// <summary>
    /// 타일맵 타일 위치만 반영하도록 CompositeCollider2D를 다시 만듭니다.
    /// 프리팹에 베이크된 m_ColliderPaths(맵 전체 박스 등)가 재시작 Instantiate 시 그대로 적용되는 문제를 막습니다.
    /// </summary>
    public void RefreshMapCollision()
    {
        ValidateAndConfigureMap();

        if (Application.isPlaying)
            RefreshCompositeColliderGeometry();
    }

    public void SetObstacleCollidersEnabled(bool enabled)
    {
        if (obstacleTilemap == null)
            return;

        if (obstacleTilemap.TryGetComponent<TilemapCollider2D>(out var tilemapCollider))
            tilemapCollider.enabled = enabled;

        if (obstacleTilemap.TryGetComponent<CompositeCollider2D>(out var compositeCollider))
            compositeCollider.enabled = enabled;
    }

    public bool IsUnderFactoryStage()
    {
        Transform node = transform;
        while (node != null)
        {
            if (GameManager.IsFactoryStageSceneRootName(node.name))
                return true;

            node = node.parent;
        }

        return false;
    }

    /// <summary>활성 챕터 맵 벽을 타일 기준으로 갱신하고, FactoryStage 벽은 챕터 1에서만 켭니다.</summary>
    public static void RefreshActiveMapColliders()
    {
        ChapterManager chapterManager = ChapterManager.Instance;
        int chapterIndex = chapterManager != null ? chapterManager.CurrentChapterIndex : 1;
        SyncFactoryStageObstacleWallsEnabled(chapterIndex == 1);

        MapInitializer[] initializers =
            FindObjectsByType<MapInitializer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        for (int i = 0; i < initializers.Length; i++)
        {
            MapInitializer initializer = initializers[i];
            if (initializer == null || initializer.IsUnderFactoryStage())
                continue;

            initializer.RefreshMapCollision();
        }

        Physics2D.SyncTransforms();
    }

    /// <summary>FactoryStage InvisibleWalls는 1챕터 레이아웃용이라 2·3챕터에서는 끕니다.</summary>
    public static void SyncFactoryStageObstacleWallsEnabled(bool enabled)
    {
        MapInitializer[] initializers =
            FindObjectsByType<MapInitializer>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < initializers.Length; i++)
        {
            MapInitializer initializer = initializers[i];
            if (initializer == null || !initializer.IsUnderFactoryStage())
                continue;

            if (enabled)
                initializer.RefreshMapCollision();
            else
                initializer.SetObstacleCollidersEnabled(false);
        }
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

    private void RefreshCompositeColliderGeometry()
    {
        if (obstacleTilemap == null)
            return;

        if (!obstacleTilemap.TryGetComponent<TilemapCollider2D>(out var tilemapCollider))
            return;

        if (!obstacleTilemap.TryGetComponent<CompositeCollider2D>(out var compositeCollider))
            return;

        compositeCollider.generationType = CompositeCollider2D.GenerationType.Synchronous;
        tilemapCollider.enabled = false;
        tilemapCollider.enabled = true;
        compositeCollider.GenerateGeometry();
    }
}
