using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class MonsterSpawner : MonoBehaviour
{
    private const int MonsterTypeCount = 3;

    [Header("Spawn Settings")]
    [SerializeField, Range(1, 7)] private int stageLevel = 1;
    [SerializeField] private Transform spawnPointParent;
    [SerializeField] private GameObject[] monsterPrefabs = new GameObject[MonsterTypeCount];
    [SerializeField] private int[] spawnCountsByStage = { 3, 4, 5, 6, 7, 9, 0 };

    private readonly List<GameObject> spawnedMonsters = new List<GameObject>();

    public event System.Action OnAllMonstersCleared;
    public event System.Action OnMonstersSpawned;

    public int RemainingMonsterCount
    {
        get
        {
            PruneDestroyedMonsters();
            return spawnedMonsters.Count;
        }
    }

    public void NextFactoryStage()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Next Factory Stage can only be used in Play Mode.");
            return;
        }

        stageLevel++;
        if (stageLevel > 7)
            stageLevel = 1;

        ClearSpawnedMonsters();
        SpawnMonstersForCurrentStage(logResult: true);
    }

    public void RespawnCurrentStage()
    {
        if (!Application.isPlaying)
            return;

        ClearSpawnedMonsters();
        SpawnMonstersForCurrentStage(logResult: true);
    }

    public void SetStageLevel(int oneBasedStageLevel)
    {
        stageLevel = Mathf.Clamp(oneBasedStageLevel, 1, 7);
    }

    /// <summary>챕터 리셋·처음부터 다시 시작 시 목록에 있는 스폰 몬스터를 제거합니다.</summary>
    public void ForceClearAllSpawned()
    {
        ClearSpawnedMonsters();
    }

    /// <summary>새 게임 세션: 스테이지 1부터 다시 스폰합니다.</summary>
    public void ResetToFirstStageAndRespawn()
    {
        if (!Application.isPlaying)
            return;

        stageLevel = 1;
        ClearSpawnedMonsters();
        SpawnMonstersForCurrentStage(logResult: true);
    }

    /// <summary>FactoryStage 재생성 시 씬 인스턴스에 연결된 몬스터 프리팹 참조를 복사합니다.</summary>
    public GameObject[] ExportMonsterPrefabReferences()
    {
        if (monsterPrefabs == null || monsterPrefabs.Length == 0)
            return null;

        var copy = new GameObject[monsterPrefabs.Length];
        for (int i = 0; i < monsterPrefabs.Length; i++)
            copy[i] = monsterPrefabs[i];

        return copy;
    }

    /// <summary>프리팹 재생성 후 끊긴 몬스터 프리팹 슬롯을 복구합니다.</summary>
    public void ImportMonsterPrefabReferences(GameObject[] sourcePrefabs)
    {
        if (sourcePrefabs == null || sourcePrefabs.Length == 0)
            return;

        if (HasRequiredMonsterPrefabs())
            return;

        if (monsterPrefabs == null || monsterPrefabs.Length != MonsterTypeCount)
            monsterPrefabs = new GameObject[MonsterTypeCount];

        int copyCount = Mathf.Min(MonsterTypeCount, sourcePrefabs.Length);
        for (int i = 0; i < copyCount; i++)
        {
            if (monsterPrefabs[i] == null && sourcePrefabs[i] != null)
                monsterPrefabs[i] = sourcePrefabs[i];
        }
    }

    private void SpawnMonstersForCurrentStage(bool logResult)
    {
        PruneDestroyedMonsters();
        EnsureMonsterPrefabsResolved(logResult);

        if (spawnPointParent == null)
        {
            Debug.LogWarning("Spawn Point Parent is not assigned.");
            return;
        }

        GameManager.EnsureActiveInHierarchy(spawnPointParent.gameObject);

        int pointCount = spawnPointParent.childCount;

        if (pointCount == 0)
        {
            Debug.LogWarning("No Spawn Points found.");
            return;
        }

        int spawnCount = GetSpawnCountForStage();
        if (spawnCount <= 0)
        {
            if (logResult)
                Debug.Log($"Factory Stage {stageLevel} : Cleared factory, spawned 0 monsters");

            return;
        }

        if (!HasRequiredMonsterPrefabs())
        {
            Debug.LogWarning(
                $"[MonsterSpawner] {name}: 몬스터 프리팹 3종이 비어 있습니다. " +
                "FactoryStage_01_PrefabRoot의 Monster Prefabs 또는 Factory Stage Prefab Sources를 확인하세요.");
            return;
        }

        if (Application.isPlaying)
        {
            MapInitializer.RefreshActiveMapColliders();
            Physics2D.SyncTransforms();
        }

        List<Transform> eligibleSpawnPoints = GetEligibleSpawnPoints();
        if (eligibleSpawnPoints.Count == 0)
        {
            Debug.LogWarning(
                $"[MonsterSpawner] {name}: 플레이어 스폰 근처를 제외하면 사용 가능한 몬스터 스폰 위치가 없습니다.");
            return;
        }

        int count = Mathf.Min(spawnCount, eligibleSpawnPoints.Count);
        List<Transform> spawnPoints = GetShuffledSpawnPoints(eligibleSpawnPoints);
        List<int> monsterTypeOrder = GetMonsterTypeOrder(count);
        int[] monsterTypeCounts = new int[MonsterTypeCount];
        List<Vector2> usedSpawnPositions = new List<Vector2>(count);
        int spawnedCount = 0;

        for (int i = 0; i < count; i++)
        {
            int monsterType = monsterTypeOrder[i];
            Transform spawnPoint = spawnPoints[i];
            if (spawnPoint == null)
                continue;

            GameObject prefab = GetMonsterPrefab(monsterType);
            if (prefab == null)
                continue;

            float bodyRadius = FieldSpawnSafety.GetMonsterBodyRadius(prefab);
            Vector3 spawnPosition = FieldSpawnSafety.ResolveSpawnPosition(
                spawnPoint.position,
                bodyRadius,
                usedSpawnPositions);

            if (FieldSpawnSafety.IsTooCloseToPlayerSpawn(spawnPosition))
                continue;

            GameObject monster = Instantiate(prefab, spawnPosition, spawnPoint.rotation);
            usedSpawnPositions.Add(spawnPosition);
            if (monster == null)
                continue;

            GameManager.EnsureFieldEntityVisible(monster);
            EnsureBattleRegistration(monster);
            spawnedMonsters.Add(monster);
            monsterTypeCounts[monsterType]++;
            spawnedCount++;
        }

        if (logResult)
            LogSpawnResult(spawnedCount, monsterTypeCounts);

        OnMonstersSpawned?.Invoke();

        if (GameManager.Instance != null)
            GameManager.Instance.NotifyStageMonstersSpawned();
    }

    private int GetSpawnCountForStage()
    {
        EnsureSpawnCountsByStage();

        int index = Mathf.Clamp(stageLevel, 1, 7) - 1;
        return spawnCountsByStage[index];
    }

    private List<Transform> GetEligibleSpawnPoints()
    {
        List<Transform> spawnPoints = new List<Transform>();

        for (int i = 0; i < spawnPointParent.childCount; i++)
            spawnPoints.Add(spawnPointParent.GetChild(i));

        return FieldSpawnSafety.FilterMonsterSpawnPointsAwayFromPlayer(spawnPoints);
    }

    private static List<Transform> GetShuffledSpawnPoints(IReadOnlyList<Transform> sourcePoints)
    {
        List<Transform> spawnPoints = new List<Transform>(sourcePoints.Count);
        for (int i = 0; i < sourcePoints.Count; i++)
            spawnPoints.Add(sourcePoints[i]);

        for (int i = 0; i < spawnPoints.Count; i++)
        {
            int randomIndex = Random.Range(i, spawnPoints.Count);
            Transform temp = spawnPoints[i];
            spawnPoints[i] = spawnPoints[randomIndex];
            spawnPoints[randomIndex] = temp;
        }

        return spawnPoints;
    }

    private List<int> GetMonsterTypeOrder(int spawnCount)
    {
        List<int> monsterTypes = new List<int>();

        for (int i = 0; i < MonsterTypeCount && monsterTypes.Count < spawnCount; i++)
            monsterTypes.Add(i);

        int nextType = 0;
        while (monsterTypes.Count < spawnCount)
        {
            monsterTypes.Add(nextType);
            nextType = (nextType + 1) % MonsterTypeCount;
        }

        for (int i = 0; i < monsterTypes.Count; i++)
        {
            int randomIndex = Random.Range(i, monsterTypes.Count);
            int temp = monsterTypes[i];
            monsterTypes[i] = monsterTypes[randomIndex];
            monsterTypes[randomIndex] = temp;
        }

        return monsterTypes;
    }

    private GameObject GetMonsterPrefab(int monsterType)
    {
        // Later this can select by monster id, stage, or JSON data.
        return monsterPrefabs[monsterType];
    }

    private bool HasRequiredMonsterPrefabs()
    {
        if (monsterPrefabs == null || monsterPrefabs.Length < MonsterTypeCount)
            return false;

        for (int i = 0; i < MonsterTypeCount; i++)
        {
            if (monsterPrefabs[i] == null)
                return false;
        }

        return true;
    }

    private void EnsureMonsterPrefabsResolved(bool logResult)
    {
        if (HasRequiredMonsterPrefabs())
            return;

#if UNITY_EDITOR
        string[] defaultPaths =
        {
            "Assets/2.SLA/Prefabs/Monsters/Monster_M001_Slime.prefab",
            "Assets/2.SLA/Prefabs/Monsters/Monster_M002_Mold.prefab",
            "Assets/2.SLA/Prefabs/Monsters/Monster_M003_Fire.prefab",
        };

        if (monsterPrefabs == null || monsterPrefabs.Length != MonsterTypeCount)
            monsterPrefabs = new GameObject[MonsterTypeCount];

        for (int i = 0; i < MonsterTypeCount; i++)
        {
            if (monsterPrefabs[i] != null)
                continue;

            GameObject loaded = AssetDatabase.LoadAssetAtPath<GameObject>(defaultPaths[i]);
            if (loaded != null)
                monsterPrefabs[i] = loaded;
        }

        if (logResult && HasRequiredMonsterPrefabs())
            Debug.Log($"[MonsterSpawner] {name}: 끊긴 몬스터 프리팹 참조를 기본 경로에서 복구했습니다.");
#endif
    }

    private void LogSpawnResult(int totalSpawned, int[] monsterTypeCounts)
    {
        Debug.Log($"Factory Stage {stageLevel} : Spawned {totalSpawned} monsters");

        for (int i = 0; i < MonsterTypeCount; i++)
            Debug.Log($"Monster Type {i}: {monsterTypeCounts[i]}");
    }

    public void RemoveSpawnedMonster(GameObject monster)
    {
        if (monster == null)
            return;

        spawnedMonsters.Remove(monster);
        PruneDestroyedMonsters();
        TryNotifyAllMonstersCleared();
    }

    private void PruneDestroyedMonsters()
    {
        spawnedMonsters.RemoveAll(monster => monster == null);
    }

    private void TryNotifyAllMonstersCleared()
    {
        if (spawnedMonsters.Count != 0)
            return;

        OnAllMonstersCleared?.Invoke();

        if (GameManager.Instance != null)
            GameManager.Instance.NotifyStageCleared();
    }

    private void ClearSpawnedMonsters()
    {
        for (int i = spawnedMonsters.Count - 1; i >= 0; i--)
        {
            if (spawnedMonsters[i] != null)
                Destroy(spawnedMonsters[i]);
        }

        spawnedMonsters.Clear();
    }

    private void OnValidate()
    {
        EnsureSpawnCountsByStage();

        if (monsterPrefabs == null || monsterPrefabs.Length != MonsterTypeCount)
        {
            GameObject[] fixedPrefabs = new GameObject[MonsterTypeCount];
            if (monsterPrefabs != null)
            {
                int copyCount = Mathf.Min(monsterPrefabs.Length, fixedPrefabs.Length);
                for (int i = 0; i < copyCount; i++)
                    fixedPrefabs[i] = monsterPrefabs[i];
            }

            monsterPrefabs = fixedPrefabs;
        }
    }

    private static void EnsureBattleRegistration(GameObject monster)
    {
        if (monster == null)
            return;

        if (monster.GetComponent<MonsterBattleRegistration>() == null)
            monster.AddComponent<MonsterBattleRegistration>();
    }

    private void EnsureSpawnCountsByStage()
    {
        int[] fixedCounts = { 3, 5, 7, 0, 0, 0, 0 };

        if (spawnCountsByStage == null || spawnCountsByStage.Length != fixedCounts.Length)
            spawnCountsByStage = new int[fixedCounts.Length];

        for (int i = 0; i < fixedCounts.Length; i++)
            spawnCountsByStage[i] = fixedCounts[i];
    }
}
