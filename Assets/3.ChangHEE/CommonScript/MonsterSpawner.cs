using System.Collections.Generic;
using UnityEngine;

public class MonsterSpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    [SerializeField, Range(1, 7)] private int stageLevel = 1;
    [SerializeField] private Transform spawnPointParent;
    [SerializeField] private GameObject monsterPrefab;

    private static readonly int[] SpawnCountsByStage = { 3, 4, 5, 6, 7, 9, 0 };
    private readonly List<GameObject> spawnedMonsters = new List<GameObject>();

    private void Start()
    {
        SpawnMonstersForCurrentStage(logResult: true);
    }

    public void NextFactoryStage()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Next Factory Stage는 Play 모드에서만 테스트할 수 있습니다.");
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
            Debug.LogWarning("Spawn Point Parent가 연결되지 않았습니다.");
            return;
        }

        int pointCount = spawnPointParent.childCount;

        if (pointCount == 0)
        {
            Debug.LogWarning("Spawn Point가 없습니다.");
            return;
        }

        int spawnCount = GetSpawnCountForStage();
        if (spawnCount <= 0)
        {
            if (logResult)
                Debug.Log($"Factory Stage {stageLevel} : Cleared factory, spawned 0 monsters");

            return;
        }

        GameObject prefab = GetMonsterPrefab();
        if (prefab == null)
        {
            Debug.LogWarning("Monster Prefab이 연결되지 않았습니다.");
            return;
        }

        int count = Mathf.Min(spawnCount, pointCount);
        List<Transform> spawnPoints = GetShuffledSpawnPoints();

        for (int i = 0; i < count; i++)
        {
            Transform spawnPoint = spawnPoints[i];
            GameObject monster = Instantiate(prefab, spawnPoint.position, spawnPoint.rotation);
            spawnedMonsters.Add(monster);
        }

        if (logResult)
            Debug.Log($"Factory Stage {stageLevel} : Spawned {count} monsters");
    }

    private int GetSpawnCountForStage()
    {
        int index = Mathf.Clamp(stageLevel, 1, 7) - 1;
        return SpawnCountsByStage[index];
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

    private GameObject GetMonsterPrefab()
    {
        // Later this can select from multiple monster prefabs by stage or monster id.
        return monsterPrefab;
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
}