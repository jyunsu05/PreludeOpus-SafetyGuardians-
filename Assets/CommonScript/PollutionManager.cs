using System;
using UnityEngine;

public class PollutionManager : MonoBehaviour
{
    public const float DefaultInitialPollution = 100f;

    public static PollutionManager Instance { get; private set; }

    [Header("--- 기본 오염도 ---")]
    [SerializeField] private float currentPollution;
    [SerializeField] private float maxPollution = 100f;
    [SerializeField] private float defaultInitialPollution = DefaultInitialPollution;

    [Header("--- 챕터별 오염도 설정 (ScriptableObject) ---")]
    [Tooltip("챕터 1, 2, 3 … 순서 또는 ChapterIndex로 매칭됩니다.")]
    [SerializeField] private ChapterPollutionData[] chapterPollutionDataList;

    [Tooltip("목록에 없는 챕터에 적용할 기본 설정")]
    [SerializeField] private ChapterPollutionData fallbackChapterData;

    public event Action<float, float> OnPollutionChanged;

    public float CurrentPollution => currentPollution;
    public float MaxPollution => maxPollution;
    public int CurrentChapterIndex { get; private set; } = 1;
    public int PurifiedMonstersThisChapter { get; private set; }
    public int ResolvedTotalMonstersThisChapter { get; private set; } = 1;

    private ChapterPollutionData activeChapterData;
    private bool chapterManagerSubscribed;

    public static PollutionManager EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        PollutionManager[] managers =
            FindObjectsByType<PollutionManager>(FindObjectsInactive.Include);
        return managers.Length > 0 ? managers[0] : null;
    }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (currentPollution <= 0f)
                SetPollution(defaultInitialPollution);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void OnEnable()
    {
        if (Instance == this)
        {
            NotifyPollutionChanged();
            TrySubscribeChapterManager();
        }
    }

    void Start()
    {
        if (Instance == this)
            ApplyChapterConfiguration(ResolveCurrentChapterIndex(), isRestart: false, resetPollutionToInitial: false);
    }

    void Update()
    {
        if (Instance == this && !chapterManagerSubscribed)
            TrySubscribeChapterManager();
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            UnsubscribeChapterManager();
            Instance = null;
        }
    }

    public void AddPollution(float amount)
    {
        if (amount <= 0f)
            return;

        currentPollution = Mathf.Clamp(currentPollution + amount, 0f, maxPollution);
        NotifyPollutionChanged();
    }

    public void ReducePollution(float amount)
    {
        if (amount <= 0f)
            return;

        currentPollution = Mathf.Clamp(currentPollution - amount, 0f, maxPollution);
        NotifyPollutionChanged();
    }

    public void ResetPollution()
    {
        currentPollution = 0f;
        NotifyPollutionChanged();
    }

    /// <summary>새 게임·전체 리셋 시 목표 오염도를 적용합니다(0 이하이면 defaultInitialPollution 사용).</summary>
    public void ApplyInitialPollution(float value)
    {
        float target = value > 0f ? value : defaultInitialPollution;
        SetPollution(target);
    }

    public void SetPollution(float value)
    {
        currentPollution = Mathf.Clamp(value, 0f, maxPollution);
        NotifyPollutionChanged();
    }

    /// <summary>
    /// 몬스터 정화(처치) 성공 시 호출. 현재 챕터 설정에 따라 공장 오염도를 감소시킵니다.
    /// </summary>
    public void OnMonsterPurified()
    {
        ChapterPollutionData chapterData = ResolveActiveChapterData();
        float reduction = chapterData.CalculateReductionPerMonster(ResolvedTotalMonstersThisChapter);

        if (reduction <= 0f)
        {
            Debug.LogWarning("[PollutionManager] 몬스터 정화 감소량이 0입니다. ChapterPollutionData 설정을 확인하세요.");
            return;
        }

        float before = currentPollution;
        ReducePollution(reduction);
        PurifiedMonstersThisChapter++;

        Debug.Log(
            $"[PollutionManager] 챕터 {CurrentChapterIndex} 몬스터 정화 — " +
            $"오염도 {before:F1} → {currentPollution:F1} (-{reduction:F1}, " +
            $"정화 {PurifiedMonstersThisChapter}/{ResolvedTotalMonstersThisChapter})");
    }

    /// <summary>챕터 전환·세션 시작 시 챕터별 오염도 설정을 적용합니다.</summary>
    public void ApplyChapterConfiguration(int chapterIndex, bool isRestart, bool resetPollutionToInitial)
    {
        CurrentChapterIndex = Mathf.Max(1, chapterIndex);
        activeChapterData = ResolveChapterData(CurrentChapterIndex);
        ResolvedTotalMonstersThisChapter = ResolveTotalMonsterCount(activeChapterData);
        maxPollution = activeChapterData.MaxPollution;

        if (resetPollutionToInitial)
            SetPollution(activeChapterData.InitialPollution);
        else
            SetPollution(currentPollution);

        Debug.Log(
            $"[PollutionManager] 챕터 {CurrentChapterIndex} 오염도 설정 적용 — " +
            $"최대 {maxPollution:F0}, 시작 {currentPollution:F0}, " +
            $"몬스터당 감소 {activeChapterData.CalculateReductionPerMonster(ResolvedTotalMonstersThisChapter):F1}");
    }

    private void HandleChapterLoaded(ChapterLoadedEventArgs args)
    {
        ApplyChapterConfiguration(args.ChapterIndex, args.IsRestart, resetPollutionToInitial: false);

        if (!args.IsRestart)
            PurifiedMonstersThisChapter = 0;
    }

    private void TrySubscribeChapterManager()
    {
        if (chapterManagerSubscribed)
            return;

        ChapterManager chapterManager = ChapterManager.Instance;
        if (chapterManager == null)
            return;

        chapterManager.OnChapterLoaded -= HandleChapterLoaded;
        chapterManager.OnChapterLoaded += HandleChapterLoaded;
        chapterManagerSubscribed = true;
    }

    private void UnsubscribeChapterManager()
    {
        if (!chapterManagerSubscribed)
            return;

        if (ChapterManager.Instance != null)
            ChapterManager.Instance.OnChapterLoaded -= HandleChapterLoaded;

        chapterManagerSubscribed = false;
    }

    private ChapterPollutionData ResolveActiveChapterData()
    {
        if (activeChapterData != null)
            return activeChapterData;

        activeChapterData = ResolveChapterData(ResolveCurrentChapterIndex());
        ResolvedTotalMonstersThisChapter = ResolveTotalMonsterCount(activeChapterData);
        return activeChapterData;
    }

    private ChapterPollutionData ResolveChapterData(int chapterIndex)
    {
        if (chapterPollutionDataList != null)
        {
            for (int i = 0; i < chapterPollutionDataList.Length; i++)
            {
                ChapterPollutionData data = chapterPollutionDataList[i];
                if (data != null && data.ChapterIndex == chapterIndex)
                    return data;
            }
        }

        if (fallbackChapterData != null)
            return fallbackChapterData;

        return ChapterPollutionData.CreateRuntimeDefault(chapterIndex);
    }

    private int ResolveTotalMonsterCount(ChapterPollutionData chapterData)
    {
        if (chapterData.TotalMonsterCountOverride > 0)
            return chapterData.TotalMonsterCountOverride;

        int fromSpawners = CountMonsterCapacityInActiveChapter();
        return Mathf.Max(1, fromSpawners);
    }

    private static int CountMonsterCapacityInActiveChapter()
    {
        MonsterSpawner[] spawners =
            FindObjectsByType<MonsterSpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        int total = 0;
        for (int i = 0; i < spawners.Length; i++)
        {
            MonsterSpawner spawner = spawners[i];
            if (spawner == null || !spawner.isActiveAndEnabled)
                continue;

            total += spawner.GetTotalSpawnCapacityAcrossStages();
        }

        return total > 0 ? total : 1;
    }

    private static int ResolveCurrentChapterIndex()
    {
        if (ChapterManager.Instance != null)
            return ChapterManager.Instance.CurrentChapterIndex;

        if (FactoryChapterController.Instance != null)
            return FactoryChapterController.Instance.CurrentChapter;

        return 1;
    }

    private void NotifyPollutionChanged()
    {
        OnPollutionChanged?.Invoke(currentPollution, maxPollution);
    }
}
