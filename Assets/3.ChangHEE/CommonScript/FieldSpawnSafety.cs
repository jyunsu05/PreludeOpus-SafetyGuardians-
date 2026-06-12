using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 필드 몬스터·아이템 스폰 위치가 벽 타일·콜라이더와 겹치지 않도록 보정합니다.
/// </summary>
public static class FieldSpawnSafety
{
    private const float DefaultBodyRadius = 0.85f;
    private const float ClearancePadding = 0.08f;
    private const int MaxSearchRings = 14;
    private const float MinSpawnSeparationMultiplier = 1.75f;

    private const float DefaultItemPickupRadius = 0.45f;

    public static float GetItemPickupRadius(GameObject prefab)
    {
        if (prefab == null)
            return DefaultItemPickupRadius;

        float scale = Mathf.Max(
            Mathf.Abs(prefab.transform.lossyScale.x),
            Mathf.Abs(prefab.transform.lossyScale.y));

        CircleCollider2D circle = prefab.GetComponent<CircleCollider2D>();
        if (circle != null && circle.enabled && circle.isTrigger)
            return Mathf.Max(circle.radius * scale, DefaultItemPickupRadius);

        CapsuleCollider2D capsule = prefab.GetComponent<CapsuleCollider2D>();
        if (capsule != null && capsule.enabled && capsule.isTrigger)
        {
            float halfExtent = Mathf.Max(capsule.size.x, capsule.size.y) * 0.5f * scale;
            return Mathf.Max(halfExtent, DefaultItemPickupRadius);
        }

        return DefaultItemPickupRadius;
    }

    public static float GetMonsterBodyRadius(GameObject prefab)
    {
        if (prefab == null)
            return DefaultBodyRadius;

        CircleCollider2D[] colliders = prefab.GetComponents<CircleCollider2D>();
        for (int i = 0; i < colliders.Length; i++)
        {
            CircleCollider2D col = colliders[i];
            if (col != null && col.enabled && !col.isTrigger)
            {
                float scale = Mathf.Max(
                    Mathf.Abs(prefab.transform.lossyScale.x),
                    Mathf.Abs(prefab.transform.lossyScale.y));
                return col.radius * scale;
            }
        }

        return DefaultBodyRadius;
    }

    public static Vector3 ResolveSpawnPosition(
        Vector3 desiredWorldPosition,
        float bodyRadius,
        IReadOnlyList<Vector2> reservedPositions = null)
    {
        if (bodyRadius <= 0f)
            bodyRadius = DefaultBodyRadius;

        float checkRadius = bodyRadius + ClearancePadding;
        float minSeparation = bodyRadius * MinSpawnSeparationMultiplier;

        Physics2D.SyncTransforms();

        if (IsPositionUsable(desiredWorldPosition, checkRadius, reservedPositions, minSeparation))
            return desiredWorldPosition;

        Tilemap obstacleTilemap = FindActiveObstacleTilemap();
        Grid grid = obstacleTilemap != null ? obstacleTilemap.layoutGrid : null;

        if (grid != null)
        {
            Vector3Int originCell = grid.WorldToCell(desiredWorldPosition);
            for (int ring = 0; ring <= MaxSearchRings; ring++)
            {
                for (int x = -ring; x <= ring; x++)
                {
                    for (int y = -ring; y <= ring; y++)
                    {
                        if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(y)) != ring)
                            continue;

                        Vector3Int cell = new Vector3Int(originCell.x + x, originCell.y + y, originCell.z);
                        Vector3 candidate = grid.GetCellCenterWorld(cell);

                        if (!IsPositionUsable(candidate, checkRadius, reservedPositions, minSeparation))
                            continue;

                        if (ring > 0)
                        {
                            Debug.Log(
                                $"[FieldSpawnSafety] 스폰 위치 보정: {desiredWorldPosition} -> {candidate}");
                        }

                        return candidate;
                    }
                }
            }
        }
        else
        {
            const int stepCount = 16;
            float step = 1f;

            for (int ring = 1; ring <= MaxSearchRings; ring++)
            {
                float distance = step * ring;
                for (int i = 0; i < stepCount; i++)
                {
                    float angle = i * Mathf.PI * 2f / stepCount;
                    Vector3 candidate = desiredWorldPosition + new Vector3(
                        Mathf.Cos(angle) * distance,
                        Mathf.Sin(angle) * distance,
                        0f);

                    if (!IsPositionUsable(candidate, checkRadius, reservedPositions, minSeparation))
                        continue;

                    Debug.Log(
                        $"[FieldSpawnSafety] 스폰 위치 보정: {desiredWorldPosition} -> {candidate}");
                    return candidate;
                }
            }
        }

        Debug.LogWarning(
            $"[FieldSpawnSafety] 안전한 스폰 위치를 찾지 못했습니다. 원래 위치를 사용합니다: {desiredWorldPosition}");
        return desiredWorldPosition;
    }

    private static bool IsPositionUsable(
        Vector3 worldPosition,
        float checkRadius,
        IReadOnlyList<Vector2> reservedPositions,
        float minSeparation)
    {
        if (IsBlockedByPhysics(worldPosition, checkRadius))
            return false;

        if (IsBlockedByObstacleTilemap(worldPosition, checkRadius))
            return false;

        if (reservedPositions == null || reservedPositions.Count == 0)
            return true;

        Vector2 flat = worldPosition;
        for (int i = 0; i < reservedPositions.Count; i++)
        {
            if (Vector2.Distance(flat, reservedPositions[i]) < minSeparation)
                return false;
        }

        return true;
    }

    private static bool IsBlockedByPhysics(Vector3 worldPosition, float radius)
    {
        Collider2D[] overlaps = Physics2D.OverlapCircleAll(worldPosition, radius);
        for (int i = 0; i < overlaps.Length; i++)
        {
            if (IsBlockingCollider(overlaps[i]))
                return true;
        }

        return false;
    }

    private static bool IsBlockingCollider(Collider2D col)
    {
        if (col == null || !col.enabled || col.isTrigger)
            return false;

        Rigidbody2D rb = col.attachedRigidbody;
        if (rb != null && rb.bodyType == RigidbodyType2D.Dynamic)
            return false;

        GameObject owner = col.gameObject;
        if (owner.CompareTag("Monster") || owner.CompareTag("Item"))
            return false;

        try
        {
            if (owner.CompareTag("Player"))
                return false;
        }
        catch (UnityException)
        {
            // Player 태그가 없을 수 있음.
        }

        return true;
    }

    private static bool IsBlockedByObstacleTilemap(Vector3 worldPosition, float radius)
    {
        Tilemap tilemap = FindActiveObstacleTilemap();
        if (tilemap == null)
            return false;

        Bounds bounds = new Bounds(worldPosition, Vector3.one * radius * 2f);
        Vector3Int minCell = tilemap.WorldToCell(bounds.min);
        Vector3Int maxCell = tilemap.WorldToCell(bounds.max);
        Vector2 center = worldPosition;
        float halfCell = tilemap.cellSize.x * 0.5f;

        for (int x = minCell.x; x <= maxCell.x; x++)
        {
            for (int y = minCell.y; y <= maxCell.y; y++)
            {
                Vector3Int cell = new Vector3Int(x, y, 0);
                if (!tilemap.HasTile(cell))
                    continue;

                Vector3 cellCenter = tilemap.GetCellCenterWorld(cell);
                Vector2 closest = new Vector2(
                    Mathf.Clamp(center.x, cellCenter.x - halfCell, cellCenter.x + halfCell),
                    Mathf.Clamp(center.y, cellCenter.y - halfCell, cellCenter.y + halfCell));

                if (Vector2.Distance(center, closest) < radius)
                    return true;
            }
        }

        return false;
    }

    private static Tilemap FindActiveObstacleTilemap()
    {
        Tilemap[] tilemaps = Object.FindObjectsByType<Tilemap>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        for (int i = 0; i < tilemaps.Length; i++)
        {
            Tilemap tilemap = tilemaps[i];
            if (tilemap == null || !tilemap.gameObject.activeInHierarchy)
                continue;

            if (tilemap.name == "InvisibleWalls")
                return tilemap;
        }

        return null;
    }
}
