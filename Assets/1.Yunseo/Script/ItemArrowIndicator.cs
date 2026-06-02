using UnityEngine;

public enum ExclusionRadiusMode
{
    FromCircleCollider,
    ManualCentimeters
}

/// <summary>
/// 플레이어 기준으로 가장 가까운 Item 태그 오브젝트를 가리키는 화살표 컨트롤러입니다.
/// 플레이어에 붙여서 사용하거나, arrowVisual을 가진 오브젝트에 붙여 사용할 수 있습니다.
/// </summary>
public class ItemArrowIndicator : MonoBehaviour
{
    [Header("=== 대상 ===")]
    [SerializeField] private Transform playerTransform;
    [SerializeField] private Transform arrowVisual;

    [Header("=== 검색 설정 ===")]
    [SerializeField] private string itemTag = "Item";
    [SerializeField] private float detectionRange = 999f;
    [SerializeField] private float hideDistance = 1.25f;

    [Header("=== 표시 설정 ===")]
    [Tooltip("FromCircleCollider: 플레이어(또는 지정 콜라이더) 원형 반지름 사용\nManualCentimeters: cm 값 직접 사용")]
    [SerializeField] private ExclusionRadiusMode exclusionRadiusMode = ExclusionRadiusMode.FromCircleCollider;
    [Tooltip("비워두면 playerTransform의 CircleCollider2D를 사용합니다.")]
    [SerializeField] private CircleCollider2D exclusionCollider;
    [Tooltip("ManualCentimeters 모드일 때만 사용됩니다.")]
    [SerializeField] private float playerExclusionRadiusCm = 5f;
    [Tooltip("콜라이더/수동 반지름에 더할 추가 거리(cm). Arrow가 플레이어에서 더 멀리 뜹니다.")]
    [SerializeField] private float exclusionRadiusBonusCm = 15f;
    [Tooltip("제외 구 표면에서 바깥으로 추가로 띄울 거리(cm). 0이면 구 표면에 붙습니다.")]
    [SerializeField] private float arrowPaddingCm = 0f;
    [SerializeField] private float rotationLerpSpeed = 12f;
    [SerializeField] private float angleOffset = -90f; // 스프라이트가 +Y를 향하는 기준 보정
    [SerializeField] private bool useNearestItemSprite = true;
    [Tooltip("아이템 스프라이트를 표시할 때 화살표 아이콘 최대 크기(cm)")]
    [SerializeField] private float arrowDisplaySizeCm = 3f;
    [SerializeField] private string autoArrowVisualName = "Arrow";

    [Header("=== 렌더 순서 ===")]
    [SerializeField] private bool copySortingFromPlayer = true;
    [Tooltip("플레이어 Sorting Order에 더할 값. Arrow를 플레이어/맵 위에 그립니다.")]
    [SerializeField] private int sortingOrderOffset = 10;

    [Header("=== 디버그 ===")]
    [SerializeField] private string currentTargetName;
    [SerializeField] private float currentDistance;
    [SerializeField] private float currentExclusionRadiusWorld;

    private Renderer arrowRenderer;
    private SpriteRenderer arrowSpriteRenderer;
    private Sprite defaultArrowSprite;
    private Vector3 defaultArrowLocalScale;
    private Transform currentTarget;

    private void Awake()
    {
        if (playerTransform == null)
            playerTransform = transform;

        if (playerTransform == arrowVisual)
        {
            Debug.LogError("[ItemArrowIndicator] playerTransform과 arrowVisual이 같습니다. 스크립트는 플레이어에 붙이고 Arrow를 arrowVisual에 연결하세요.");
        }

        // 스크립트를 플레이어 본체에 붙였을 때 플레이어 렌더러가 꺼지는 문제를 방지합니다.
        if (arrowVisual == null || arrowVisual == playerTransform)
            arrowVisual = GetOrCreateArrowVisual(playerTransform);

        arrowRenderer = arrowVisual.GetComponent<Renderer>();
        arrowSpriteRenderer = arrowVisual.GetComponent<SpriteRenderer>();
        if (arrowSpriteRenderer != null)
            defaultArrowSprite = arrowSpriteRenderer.sprite;

        defaultArrowLocalScale = arrowVisual.localScale;
        ApplyArrowSortingFromPlayer();
    }

    private void ApplyArrowSortingFromPlayer()
    {
        if (!copySortingFromPlayer || arrowSpriteRenderer == null || playerTransform == null)
            return;

        SpriteRenderer playerRenderer = playerTransform.GetComponent<SpriteRenderer>();
        if (playerRenderer == null)
            return;

        arrowSpriteRenderer.sortingLayerID = playerRenderer.sortingLayerID;
        arrowSpriteRenderer.sortingOrder = playerRenderer.sortingOrder + sortingOrderOffset;
    }

    private void Update()
    {
        if (playerTransform == null || arrowVisual == null)
            return;

        currentTarget = FindNearestItem();

        if (currentTarget == null)
        {
            currentTargetName = "없음";
            currentDistance = 0f;
            SetArrowVisible(false);
            return;
        }

        currentTargetName = currentTarget.name;
        Vector3 direction = currentTarget.position - playerTransform.position;
        currentDistance = direction.magnitude;

        if (currentDistance <= hideDistance)
        {
            SetArrowVisible(false);
            return;
        }

        SetArrowVisible(true);
        SyncArrowSprite();

        Vector3 directionNormalized = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.up;
        PlaceArrowOnExclusionSphere(directionNormalized);

        float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + angleOffset;
        Quaternion targetRotation = Quaternion.Euler(0f, 0f, targetAngle);
        arrowVisual.rotation = Quaternion.Lerp(arrowVisual.rotation, targetRotation, rotationLerpSpeed * Time.deltaTime);
    }

    private Transform FindNearestItem()
    {
        GameObject[] items;
        try
        {
            items = GameObject.FindGameObjectsWithTag(itemTag);
        }
        catch (UnityException)
        {
            // 태그가 아직 등록되지 않았으면 조용히 실패 처리
            return null;
        }

        Transform nearest = null;
        float nearestDistance = detectionRange > 0f ? detectionRange : float.MaxValue;

        for (int i = 0; i < items.Length; i++)
        {
            GameObject item = items[i];
            if (item == null)
                continue;

            float dist = Vector3.Distance(playerTransform.position, item.transform.position);
            if (dist < nearestDistance)
            {
                nearestDistance = dist;
                nearest = item.transform;
            }
        }

        return nearest;
    }

    /// <summary>
    /// 플레이어 중심 제외 구 표면 바깥에 화살표를 배치합니다.
    /// </summary>
    private void PlaceArrowOnExclusionSphere(Vector3 directionNormalized)
    {
        float exclusionRadius = GetExclusionRadiusWorld();
        currentExclusionRadiusWorld = exclusionRadius;

        // 아이콘 중심이 아니라 스프라이트 가장자리가 구 밖에 오도록 반지름만큼 더 밀어냅니다.
        float spawnDistance = exclusionRadius + CmToWorld(arrowPaddingCm) + GetArrowHalfExtentWorld();
        Vector3 worldPos = playerTransform.position + directionNormalized * spawnDistance;
        worldPos.z = arrowVisual.position.z;
        arrowVisual.position = worldPos;
    }

    private float GetExclusionRadiusWorld()
    {
        float baseRadius;

        if (exclusionRadiusMode == ExclusionRadiusMode.FromCircleCollider)
        {
            CircleCollider2D circle = exclusionCollider;
            if (circle == null && playerTransform != null)
                circle = playerTransform.GetComponent<CircleCollider2D>();

            baseRadius = circle != null
                ? GetCircleColliderWorldRadius(circle)
                : CmToWorld(playerExclusionRadiusCm);
        }
        else
        {
            baseRadius = CmToWorld(playerExclusionRadiusCm);
        }

        return baseRadius + CmToWorld(exclusionRadiusBonusCm);
    }

    private static float GetCircleColliderWorldRadius(CircleCollider2D circle)
    {
        Transform circleTransform = circle.transform;
        float scale = Mathf.Max(
            Mathf.Abs(circleTransform.lossyScale.x),
            Mathf.Abs(circleTransform.lossyScale.y));

        Vector2 offsetWorld = Vector2.Scale(circle.offset, circleTransform.lossyScale);
        return circle.radius * scale + offsetWorld.magnitude;
    }

    private float GetArrowHalfExtentWorld()
    {
        if (arrowSpriteRenderer == null || !arrowSpriteRenderer.enabled)
            return 0f;

        return Mathf.Max(
            arrowSpriteRenderer.bounds.extents.x,
            arrowSpriteRenderer.bounds.extents.y);
    }

    private static float CmToWorld(float cm) => Mathf.Max(0f, cm) * 0.01f;

    private void OnDrawGizmosSelected()
    {
        Transform center = playerTransform != null ? playerTransform : transform;
        float radius = Application.isPlaying
            ? currentExclusionRadiusWorld
            : GetExclusionRadiusWorld();

        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.35f);
        Gizmos.DrawWireSphere(center.position, radius);
    }

    private void SetArrowVisible(bool visible)
    {
        if (arrowRenderer != null)
            arrowRenderer.enabled = visible;
    }

    private Transform GetOrCreateArrowVisual(Transform parent)
    {
        if (parent == null)
            return transform;

        Transform existing = parent.Find(autoArrowVisualName);
        if (existing != null)
            return existing;

        GameObject visualObject = new GameObject(autoArrowVisualName);
        visualObject.transform.SetParent(parent, false);
        visualObject.transform.localPosition = Vector3.zero;
        visualObject.transform.localRotation = Quaternion.identity;
        visualObject.transform.localScale = Vector3.one;

        // 시각 표현용 기본 렌더러를 보장합니다.
        visualObject.AddComponent<SpriteRenderer>();
        return visualObject.transform;
    }

    private void SyncArrowSprite()
    {
        if (!useNearestItemSprite || arrowSpriteRenderer == null)
            return;

        if (currentTarget == null)
        {
            arrowSpriteRenderer.sprite = defaultArrowSprite;
            arrowVisual.localScale = defaultArrowLocalScale;
            return;
        }

        SpriteRenderer itemSpriteRenderer = currentTarget.GetComponent<SpriteRenderer>();
        if (itemSpriteRenderer == null)
            itemSpriteRenderer = currentTarget.GetComponentInChildren<SpriteRenderer>();

        if (itemSpriteRenderer != null && itemSpriteRenderer.sprite != null)
        {
            arrowSpriteRenderer.sprite = itemSpriteRenderer.sprite;
            ApplyArrowDisplayScale(itemSpriteRenderer.sprite);
        }
        else
        {
            arrowSpriteRenderer.sprite = defaultArrowSprite;
            arrowVisual.localScale = defaultArrowLocalScale;
        }
    }

    private void ApplyArrowDisplayScale(Sprite sprite)
    {
        float targetWorldSize = CmToWorld(arrowDisplaySizeCm);
        Vector2 spriteSize = sprite.bounds.size;
        float maxDim = Mathf.Max(spriteSize.x, spriteSize.y);
        if (maxDim <= 0.0001f)
            return;

        float parentScale = arrowVisual.parent != null
            ? Mathf.Max(Mathf.Abs(arrowVisual.parent.lossyScale.x), Mathf.Abs(arrowVisual.parent.lossyScale.y))
            : 1f;

        float uniformScale = targetWorldSize / (maxDim * parentScale);
        arrowVisual.localScale = new Vector3(uniformScale, uniformScale, defaultArrowLocalScale.z);
    }
}
