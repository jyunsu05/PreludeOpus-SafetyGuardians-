using UnityEngine;
using UnityEngine.Tilemaps;

[ExecuteAlways]
public class MapInitializer : MonoBehaviour
{
    [Header("Tilemap Reference")]
    [SerializeField] private Tilemap obstacleTilemap;

    // OnEnable → Start 순서로 둘 다 실행되는 유니티 라이프사이클 특성상,
    // Start 이전의 첫 OnEnable 호출을 구분해 geometry 재생성을 중복 실행하지 않도록 한다.
    private bool hasStarted;

    private void Start()
    {
        ValidateAndConfigureMap();
        hasStarted = true;

        if (Application.isPlaying && !IsUnderFactoryStage())
            RefreshCompositeColliderGeometry();
    }

    private void OnEnable()
    {
        if (!Application.isPlaying || IsUnderFactoryStage())
            return;

        // Start 이전 첫 활성화는 Start에서 처리하므로 여기서는 건너뜀.
        // 이후 비활성화 → 재활성화 시에만 실행.
        if (!hasStarted)
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

        // Start() 이전(Instantiate 직후 같은 프레임)에는 geometry를 갱신하지 않는다.
        // TilemapCollider2D가 물리 엔진에 완전히 등록되기 전에 enabled 토글을 하면
        // CompositeCollider2D에 프리팹의 베이크된 경로가 그대로 남는 버그가 발생한다.
        // Start()에서 타일맵 초기화 완료 후 RefreshCompositeColliderGeometry()가 호출된다.
        if (Application.isPlaying && hasStarted)
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

        // 4. Rigidbody2D 보장 및 Body Type 강제 고정
        // CompositeCollider2D 추가 시 Unity가 자동으로 Rigidbody2D를 붙이지만
        // 기본 bodyType이 Dynamic이므로, 없으면 직접 추가해 Static을 명시한다.
        if (!obstacleTilemap.TryGetComponent<Rigidbody2D>(out var rb))
        {
            rb = obstacleTilemap.gameObject.AddComponent<Rigidbody2D>();
            Debug.Log("[지도 세팅] Rigidbody2D가 없어 자동으로 추가했습니다.");
        }

        if (rb.bodyType != RigidbodyType2D.Static)
        {
            rb.bodyType = RigidbodyType2D.Static;
            Debug.Log("[지도 세팅] Rigidbody2D Body Type을 Static으로 안전하게 변경했습니다.");
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

        // 핵심: Instantiate()는 씬 로드와 달리 TilemapCollider2D의 타일 데이터 재읽기를
        // 자동으로 수행하지 않는다. RefreshAllTiles()로 Tilemap → TilemapCollider2D 경로를
        // 강제 동기화해야 이후 GenerateGeometry()가 실제 타일 도형으로 CompositeCollider2D를
        // 재빌드한다. 이 호출이 없으면 프리팹에 베이크된 m_ColliderPaths가 그대로 남는다.
        obstacleTilemap.RefreshAllTiles();

        compositeCollider.generationType = CompositeCollider2D.GenerationType.Manual;
        tilemapCollider.enabled = false;
        tilemapCollider.enabled = true;
        compositeCollider.GenerateGeometry();
        Physics2D.SyncTransforms();
        compositeCollider.generationType = CompositeCollider2D.GenerationType.Synchronous;
    }
}
