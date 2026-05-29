using System.Collections.Generic;
using UnityEngine;

public class ItemSpawner : MonoBehaviour
{
    private const int ItemTypeCount = 3;

    [Header("Spawn Settings")]
    [SerializeField, Range(1, 7)] private int stageLevel = 1;
    [SerializeField] private Transform itemSpawnPointParent;
    [SerializeField] private GameObject[] itemPrefabs = new GameObject[ItemTypeCount];
    [SerializeField] private int[] spawnCountsByStage = { 3, 4, 5, 6, 7, 9, 0 };

    private readonly List<GameObject> spawnedItems = new List<GameObject>();

    private void Start()
    {
        SpawnItemsForCurrentStage(logResult: true);
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

        ClearSpawnedItems();
        SpawnItemsForCurrentStage(logResult: true);
    }

    private void SpawnItemsForCurrentStage(bool logResult)
    {
        if (itemSpawnPointParent == null)
        {
            Debug.LogWarning("Item Spawn Point Parent is not assigned.");
            return;
        }

        int pointCount = itemSpawnPointParent.childCount;
        if (pointCount == 0)
        {
            Debug.LogWarning("No Item Spawn Points found.");
            return;
        }

        int spawnCount = GetSpawnCountForStage();
        if (spawnCount <= 0)
        {
            if (logResult)
                Debug.Log($"ItemSpawner Stage {stageLevel} : Cleared factory, spawned 0 items");

            return;
        }

        if (!HasRequiredItemPrefabs())
        {
            Debug.LogWarning("Item Prefabs must contain 3 assigned prefabs.");
            return;
        }

        int count = Mathf.Min(spawnCount, pointCount);
        List<Transform> spawnPoints = GetShuffledSpawnPoints();
        List<int> itemTypeOrder = GetItemTypeOrder(count);
        int[] itemTypeCounts = new int[ItemTypeCount];

        for (int i = 0; i < count; i++)
        {
            Transform spawnPoint = spawnPoints[i];
            int itemType = itemTypeOrder[i];
            GameObject prefab = GetItemPrefab(itemType);
            GameObject item = Instantiate(prefab, spawnPoint.position, spawnPoint.rotation);
            spawnedItems.Add(item);
            itemTypeCounts[itemType]++;
        }

        if (logResult)
            LogSpawnResult(count, itemTypeCounts);
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

        for (int i = 0; i < itemSpawnPointParent.childCount; i++)
            spawnPoints.Add(itemSpawnPointParent.GetChild(i));

        for (int i = 0; i < spawnPoints.Count; i++)
        {
            int randomIndex = Random.Range(i, spawnPoints.Count);
            Transform temp = spawnPoints[i];
            spawnPoints[i] = spawnPoints[randomIndex];
            spawnPoints[randomIndex] = temp;
        }

        return spawnPoints;
    }

    private List<int> GetItemTypeOrder(int spawnCount)
    {
        List<int> itemTypes = new List<int>();

        for (int i = 0; i < ItemTypeCount && itemTypes.Count < spawnCount; i++)
            itemTypes.Add(i);

        while (itemTypes.Count < spawnCount)
            itemTypes.Add(Random.Range(0, ItemTypeCount));

        for (int i = 0; i < itemTypes.Count; i++)
        {
            int randomIndex = Random.Range(i, itemTypes.Count);
            int temp = itemTypes[i];
            itemTypes[i] = itemTypes[randomIndex];
            itemTypes[randomIndex] = temp;
        }

        return itemTypes;
    }

    private GameObject GetItemPrefab(int itemType)
    {
        return itemPrefabs[itemType];
    }

    private void LogSpawnResult(int totalSpawned, int[] itemTypeCounts)
    {
        Debug.Log($"ItemSpawner Stage {stageLevel} : Spawned {totalSpawned} items");

        for (int i = 0; i < ItemTypeCount; i++)
            Debug.Log($"Item Type {i}: {itemTypeCounts[i]}");
    }

    private bool HasRequiredItemPrefabs()
    {
        if (itemPrefabs == null || itemPrefabs.Length < ItemTypeCount)
            return false;

        for (int i = 0; i < ItemTypeCount; i++)
        {
            if (itemPrefabs[i] == null)
                return false;
        }

        return true;
    }

    private void OnValidate()
    {
        EnsureSpawnCountsByStage();

        if (itemPrefabs == null || itemPrefabs.Length != ItemTypeCount)
        {
            GameObject[] fixedPrefabs = new GameObject[ItemTypeCount];
            if (itemPrefabs != null)
            {
                int copyCount = Mathf.Min(itemPrefabs.Length, fixedPrefabs.Length);
                for (int i = 0; i < copyCount; i++)
                    fixedPrefabs[i] = itemPrefabs[i];
            }

            itemPrefabs = fixedPrefabs;
        }
    }

    private void ClearSpawnedItems()
    {
        for (int i = spawnedItems.Count - 1; i >= 0; i--)
        {
            if (spawnedItems[i] != null)
                Destroy(spawnedItems[i]);
        }

        spawnedItems.Clear();
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
