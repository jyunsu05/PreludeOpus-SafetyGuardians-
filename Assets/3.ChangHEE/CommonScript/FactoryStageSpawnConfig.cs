using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 공장 챕터별 몬스터·몬스터 정화 아이템 스폰 규칙(총량·종류 분배)을 한곳에서 관리합니다.
/// </summary>
public static class FactoryStageSpawnConfig
{
    public const int EntityTypeCount = 3;
    public const int MaxStageLevel = 7;

    private static readonly int[] SpawnCountsByStage = { 3, 5, 7, 0, 0, 0, 0 };

    public static int GetSpawnCountForStage(int oneBasedStageLevel)
    {
        int index = Mathf.Clamp(oneBasedStageLevel, 1, MaxStageLevel) - 1;
        return SpawnCountsByStage[index];
    }

    public static void EnsureSpawnCountsByStage(ref int[] spawnCountsByStage)
    {
        if (spawnCountsByStage == null || spawnCountsByStage.Length != SpawnCountsByStage.Length)
            spawnCountsByStage = new int[SpawnCountsByStage.Length];

        for (int i = 0; i < SpawnCountsByStage.Length; i++)
            spawnCountsByStage[i] = SpawnCountsByStage[i];
    }

    /// <summary>
    /// 각 종류를 최소 1개씩 포함한 뒤 0→1→2 순으로 채우고, 순서를 섞습니다.
    /// </summary>
    public static List<int> BuildShuffledTypeOrder(int spawnCount)
    {
        var typeOrder = new List<int>();

        for (int i = 0; i < EntityTypeCount && typeOrder.Count < spawnCount; i++)
            typeOrder.Add(i);

        int nextType = 0;
        while (typeOrder.Count < spawnCount)
        {
            typeOrder.Add(nextType);
            nextType = (nextType + 1) % EntityTypeCount;
        }

        Shuffle(typeOrder);
        return typeOrder;
    }

    public static int[] BuildTypeCountsFromOrder(IReadOnlyList<int> typeOrder)
    {
        var typeCounts = new int[EntityTypeCount];
        if (typeOrder == null)
            return typeCounts;

        for (int i = 0; i < typeOrder.Count; i++)
        {
            int type = typeOrder[i];
            if (type >= 0 && type < EntityTypeCount)
                typeCounts[type]++;
        }

        return typeCounts;
    }

    public static int SumTypeCounts(IReadOnlyList<int> typeCounts)
    {
        if (typeCounts == null)
            return 0;

        int total = 0;
        for (int i = 0; i < typeCounts.Count; i++)
            total += typeCounts[i];

        return total;
    }

    private static void Shuffle<T>(IList<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int randomIndex = Random.Range(i, list.Count);
            T temp = list[i];
            list[i] = list[randomIndex];
            list[randomIndex] = temp;
        }
    }
}
