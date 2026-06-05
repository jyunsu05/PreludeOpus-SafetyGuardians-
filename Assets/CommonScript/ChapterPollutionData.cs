using UnityEngine;

/// <summary>
/// 챕터별 공장 오염도 설정. Project 창에서 Create > SafetyGuardians > Chapter Pollution Data 로 생성하세요.
/// </summary>
[CreateAssetMenu(fileName = "ChapterPollutionData", menuName = "SafetyGuardians/Chapter Pollution Data")]
public class ChapterPollutionData : ScriptableObject
{
    public enum PollutionReductionMode
    {
        /// <summary>몬스터당 고정 수치만큼 감소합니다.</summary>
        FixedPerMonster,

        /// <summary>챕터 최대 오염도를 총 몬스터 수로 나눈 만큼(%) 감소합니다.</summary>
        ProportionalToMonsterCount
    }

    [Header("--- 챕터 식별 ---")]
    [Tooltip("1-based 챕터 번호 (ChapterManager.CurrentChapterIndex와 매칭)")]
    [SerializeField] private int chapterIndex = 1;

    [Header("--- 오염도 범위 ---")]
    [SerializeField] private float maxPollution = 100f;
    [SerializeField] private float initialPollution = 100f;

    [Header("--- 몬스터 정화 시 감소 규칙 ---")]
    [SerializeField] private PollutionReductionMode reductionMode = PollutionReductionMode.ProportionalToMonsterCount;

    [Tooltip("FixedPerMonster 모드에서 몬스터 1마리당 감소량")]
    [SerializeField] private float fixedReductionPerMonster = 10f;

    [Tooltip("0이면 런타임에 MonsterSpawner 스폰 설정을 합산해 자동 계산합니다.")]
    [SerializeField] private int totalMonsterCountOverride;

    public int ChapterIndex => Mathf.Max(1, chapterIndex);
    public float MaxPollution => Mathf.Max(1f, maxPollution);
    public float InitialPollution => Mathf.Clamp(initialPollution, 0f, MaxPollution);
    public PollutionReductionMode ReductionMode => reductionMode;
    public int TotalMonsterCountOverride => Mathf.Max(0, totalMonsterCountOverride);

    public float CalculateReductionPerMonster(int resolvedTotalMonsterCount)
    {
        if (reductionMode == PollutionReductionMode.FixedPerMonster)
            return Mathf.Max(0f, fixedReductionPerMonster);

        int totalMonsters = TotalMonsterCountOverride > 0
            ? TotalMonsterCountOverride
            : Mathf.Max(1, resolvedTotalMonsterCount);

        return MaxPollution / totalMonsters;
    }

    /// <summary>ScriptableObject 에셋이 없을 때 런타임 기본값을 만듭니다.</summary>
    public static ChapterPollutionData CreateRuntimeDefault(int chapterIndex)
    {
        ChapterPollutionData data = CreateInstance<ChapterPollutionData>();
        data.hideFlags = HideFlags.HideAndDontSave;
        data.name = $"RuntimeChapterPollution_Chapter{chapterIndex}";
        data.chapterIndex = Mathf.Max(1, chapterIndex);
        data.maxPollution = DefaultInitialPollution;
        data.initialPollution = DefaultInitialPollution;
        data.reductionMode = PollutionReductionMode.ProportionalToMonsterCount;
        return data;
    }

    private const float DefaultInitialPollution = 100f;
}
