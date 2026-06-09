using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 공장 오염도를 관리하는 싱글톤. 챕터 몬스터 수에 비례해 정화 시 오염도를 감소시킵니다.
/// </summary>
public class PollutionManager : MonoBehaviour
{
    public const float DefaultInitialPollution = 100f;

    public static PollutionManager Instance { get; private set; }

    [SerializeField] private float currentPollution;
    [SerializeField] private float maxPollution = DefaultInitialPollution;
    [SerializeField] private float defaultInitialPollution = DefaultInitialPollution;

    public event Action<float, float> OnPollutionChanged;

    public float CurrentPollution => currentPollution;
    public float MaxPollution => maxPollution;
    public int TotalMonstersThisChapter { get; private set; }
    public int PurifiedMonstersThisChapter { get; private set; }
    public float ReductionPerMonster { get; private set; }
    public float ReductionPercentPerMonster =>
        maxPollution > 0f ? ReductionPerMonster / maxPollution * 100f : 0f;

    private bool chapterManagerSubscribed;
    private bool gameManagerSubscribed;
    private Coroutine refreshQuotaRoutine;

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
        if (Instance != this)
            return;

        NotifyPollutionChanged();
        TrySubscribeChapterManager();
        TrySubscribeGameManager();
    }

    void Start()
    {
        if (Instance != this)
            return;

        BeginChapterPollutionTracking(resetPollutionToMax: false);
    }

    void Update()
    {
        if (Instance != this)
            return;

        if (!chapterManagerSubscribed)
            TrySubscribeChapterManager();

        if (!gameManagerSubscribed)
            TrySubscribeGameManager();
    }

    void OnDestroy()
    {
        if (Instance != this)
            return;

        UnsubscribeChapterManager();
        UnsubscribeGameManager();
        Instance = null;
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
    /// 몬스터 정화(처치) 시 호출. (100% / 총 몬스터 수)만큼 오염도를 줄입니다.
    /// 마지막 몬스터 정화 시 오염도가 정확히 0%가 되도록 남은 몬스터 수로 분배합니다.
    /// </summary>
    public void OnMonsterPurified()
    {
        if (TotalMonstersThisChapter <= 0)
            RefreshChapterMonsterQuota();

        int remainingMonsters = TotalMonstersThisChapter - PurifiedMonstersThisChapter;
        if (remainingMonsters <= 0)
        {
            Debug.LogWarning("[PollutionManager] 남은 몬스터 쿼터가 없습니다. 챕터 몬스터 수를 확인하세요.");
            return;
        }

        float reduction = currentPollution / remainingMonsters;
        float reductionPercent = maxPollution > 0f ? reduction / maxPollution * 100f : 0f;
        float before = currentPollution;

        ReducePollution(reduction);
        PurifiedMonstersThisChapter++;

        Debug.Log(
            $"[PollutionManager] 몬스터 정화 — 오염도 {before:F1}% → {currentPollution:F1}% " +
            $"(-{reductionPercent:F1}%, {PurifiedMonstersThisChapter}/{TotalMonstersThisChapter})");

        PlaySessionStats stats = PlaySessionStats.EnsureInstance();
        if (stats != null)
            stats.TryRecordPurification(ResolvePurifiedMonsterId());
    }

    private static string ResolvePurifiedMonsterId()
    {
        UIBattleManager battleManager =
            FindAnyObjectByType<UIBattleManager>(FindObjectsInactive.Include);
        MonsterData data = battleManager != null ? battleManager.GetCurrentMonsterData() : null;
        if (data != null && !string.IsNullOrEmpty(data.id))
            return data.id;

        string contextId = BattleEncounterContext.PeekEncounteredMonsterId();
        if (!string.IsNullOrEmpty(contextId))
            return contextId;

        GameObject monster = BattleEncounterContext.PeekEncounteredMonsterObject();
        return monster != null ? monster.name : "unknown_monster";
    }

    private void BeginChapterPollutionTracking(bool resetPollutionToMax)
    {
        PurifiedMonstersThisChapter = 0;
        TotalMonstersThisChapter = 0;
        ReductionPerMonster = 0f;

        if (resetPollutionToMax)
            SetPollution(maxPollution);

        ScheduleRefreshChapterMonsterQuota();
    }

    private void ScheduleRefreshChapterMonsterQuota()
    {
        if (refreshQuotaRoutine != null)
            StopCoroutine(refreshQuotaRoutine);

        refreshQuotaRoutine = StartCoroutine(RefreshChapterMonsterQuotaRoutine());
    }

    private IEnumerator RefreshChapterMonsterQuotaRoutine()
    {
        yield return null;
        yield return null;

        RefreshChapterMonsterQuota();
        refreshQuotaRoutine = null;
    }

    /// <summary>
    /// 챕터 내 살아 있는 몬스터 + 이미 정화한 몬스터 수로 총 몬스터 수를 갱신합니다.
    /// 감소율 = 100% / 총 몬스터 수
    /// </summary>
    private void RefreshChapterMonsterQuota()
    {
        int aliveMonsters = CountActiveFieldMonsters();
        if (aliveMonsters <= 0)
            aliveMonsters = CountMonstersFromSpawners();

        int quota = PurifiedMonstersThisChapter + aliveMonsters;
        if (quota > TotalMonstersThisChapter)
            TotalMonstersThisChapter = Mathf.Max(quota, 1);

        if (TotalMonstersThisChapter <= 0)
            TotalMonstersThisChapter = 1;

        ReductionPerMonster = maxPollution / TotalMonstersThisChapter;

        Debug.Log(
            $"[PollutionManager] 챕터 몬스터 쿼터 갱신 — 총 {TotalMonstersThisChapter}마리, " +
            $"마리당 {ReductionPercentPerMonster:F2}% ({ReductionPerMonster:F2}) 감소");
    }

    private static int CountActiveFieldMonsters()
    {
        MonsterController[] monsters =
            FindObjectsByType<MonsterController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        int count = 0;
        for (int i = 0; i < monsters.Length; i++)
        {
            MonsterController monster = monsters[i];
            if (monster != null && monster.isActiveAndEnabled)
                count++;
        }

        return count;
    }

    private static int CountMonstersFromSpawners()
    {
        MonsterSpawner[] spawners =
            FindObjectsByType<MonsterSpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        int total = 0;
        for (int i = 0; i < spawners.Length; i++)
        {
            MonsterSpawner spawner = spawners[i];
            if (spawner == null || !spawner.isActiveAndEnabled)
                continue;

            total += spawner.RemainingMonsterCount;
        }

        return total;
    }

    private void HandleChapterLoaded(ChapterLoadedEventArgs args)
    {
        BeginChapterPollutionTracking(resetPollutionToMax: true);
    }

    private void HandleStageMonstersSpawned()
    {
        RefreshChapterMonsterQuota();
        NotifyPollutionChanged();
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

    private void TrySubscribeGameManager()
    {
        if (gameManagerSubscribed || GameManager.Instance == null)
            return;

        GameManager.Instance.OnStageMonstersSpawned -= HandleStageMonstersSpawned;
        GameManager.Instance.OnStageMonstersSpawned += HandleStageMonstersSpawned;
        gameManagerSubscribed = true;
    }

    private void UnsubscribeGameManager()
    {
        if (!gameManagerSubscribed || GameManager.Instance == null)
            return;

        GameManager.Instance.OnStageMonstersSpawned -= HandleStageMonstersSpawned;
        gameManagerSubscribed = false;
    }

    private void NotifyPollutionChanged()
    {
        OnPollutionChanged?.Invoke(currentPollution, maxPollution);

        if (UIManager.Instance != null)
            UIManager.Instance.UpdatePollutionBar(currentPollution, maxPollution);
    }
}
