using System.Collections.Generic;
using UnityEngine;

public class MonsterSpawner : MonoBehaviour
{
    private const int MonsterTypeCount = 3;

    [Header("Spawn Settings")]
    [SerializeField, Range(1, 7)] private int stageLevel = 1;
    [SerializeField] private Transform spawnPointParent;
    [SerializeField] private GameObject[] monsterPrefabs = new GameObject[MonsterTypeCount];
    [SerializeField] private int[] spawnCountsByStage = { 3, 4, 5, 6, 7, 9, 0 };

    private readonly List<GameObject> spawnedMonsters = new List<GameObject>();

    private void Start()
    {
        SpawnMonstersForCurrentStage(logResult: true);
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

    private void SpawnMonstersForCurrentStage(bool logResult)
    {
        if (spawnPointParent == null)
        {
            Debug.LogWarning("Spawn Point Parent is not assigned.");
            return;
        }

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
            Debug.LogWarning("Monster Prefabs must contain 3 assigned prefabs.");
            return;
        }

        int count = Mathf.Min(spawnCount, pointCount);
        List<Transform> spawnPoints = GetShuffledSpawnPoints();
        List<int> monsterTypeOrder = GetMonsterTypeOrder(count);
        int[] monsterTypeCounts = new int[MonsterTypeCount];

        for (int i = 0; i < count; i++)
        {
            int monsterType = monsterTypeOrder[i];
            Transform spawnPoint = spawnPoints[i];
            GameObject prefab = GetMonsterPrefab(monsterType);
            GameObject monster = Instantiate(prefab, spawnPoint.position, spawnPoint.rotation);
            spawnedMonsters.Add(monster);
            monsterTypeCounts[monsterType]++;
        }

        if (logResult)
            LogSpawnResult(count, monsterTypeCounts);
    }

    private int GetSpawnCountForStage()
    {
        EnsureSpawnCountsByStage();

        int index = Mathf.Clamp(stageLevel, 1, 7) - 1;
        return spawnCountsByStage[index];
    }

    private List<Transform> GetShuffledSpawnPoints()
    {
        List<Transform> spawnPoints = new List<Transform>();

        for (int i = 0; i < spawnPointParent.childCount; i++)
            spawnPoints.Add(spawnPointParent.GetChild(i));

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

        while (monsterTypes.Count < spawnCount)
            monsterTypes.Add(Random.Range(0, MonsterTypeCount));

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

    private void LogSpawnResult(int totalSpawned, int[] monsterTypeCounts)
    {
        Debug.Log($"Factory Stage {stageLevel} : Spawned {totalSpawned} monsters");

        for (int i = 0; i < MonsterTypeCount; i++)
            Debug.Log($"Monster Type {i}: {monsterTypeCounts[i]}");
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

    private void EnsureSpawnCountsByStage()
    {
        int[] fixedCounts = { 3, 4, 5, 6, 7, 9, 0 };

        if (spawnCountsByStage == null || spawnCountsByStage.Length != fixedCounts.Length)
            spawnCountsByStage = new int[fixedCounts.Length];

        for (int i = 0; i < fixedCounts.Length; i++)
            spawnCountsByStage[i] = fixedCounts[i];
    }
}