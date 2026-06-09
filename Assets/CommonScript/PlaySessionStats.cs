using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이 세션 통계 집계. Spec §4, §6, §10.4.
/// UI·게임플레이 훅 연결은 WP-2에서 수행합니다.
/// </summary>
public class PlaySessionStats : MonoBehaviour
{
    public const int ChapterSnapshotCount = 3;

    public static PlaySessionStats Instance { get; private set; }

    public static PlaySessionStats EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        PlaySessionStats[] existing =
            FindObjectsByType<PlaySessionStats>(FindObjectsInactive.Include);
        if (existing.Length > 0)
            return existing[0];

        var host = new GameObject(nameof(PlaySessionStats));
        return host.AddComponent<PlaySessionStats>();
    }

    [Header("--- Session Totals (Inspector debug) ---")]
    [SerializeField] private StatBlock sessionTotal = new StatBlock();
    [SerializeField] private StatBlock clearRun = new StatBlock();
    [SerializeField] private StatBlock currentChapterStats = new StatBlock();

    [SerializeField] private ChapterSnapshot[] chapterSnapshots = new ChapterSnapshot[ChapterSnapshotCount];

    [Header("--- Session Meta ---")]
    [SerializeField] private int clearedFactoryCount;
    [SerializeField] private int reachedFactory = 1;
    [SerializeField] private int gameOverCount;

    private readonly HashSet<string> sessionPurifiedIds = new HashSet<string>();
    private readonly HashSet<string> clearRunPurifiedIds = new HashSet<string>();
    private readonly HashSet<string> currentChapterPurifiedIds = new HashSet<string>();
    private readonly List<InventoryManager.StackedInventoryItem> sessionAcquiredItems =
        new List<InventoryManager.StackedInventoryItem>();

    private bool isChapterTrackingActive;

    public StatBlock SessionTotal => sessionTotal;
    public StatBlock ClearRun => clearRun;
    public StatBlock CurrentChapterStats => currentChapterStats;
    public IReadOnlyList<ChapterSnapshot> ChapterSnapshots => chapterSnapshots;
    public IReadOnlyList<InventoryManager.StackedInventoryItem> SessionAcquiredItems => sessionAcquiredItems;
    public int ClearedFactoryCount => clearedFactoryCount;
    public int ReachedFactory => reachedFactory;
    public int GameOverCount => gameOverCount;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureChapterSnapshotArray();
    }

    private void Update()
    {
        if (!isChapterTrackingActive)
            return;

        float delta = Time.deltaTime;
        currentChapterStats.playTimeSeconds += delta;
        sessionTotal.playTimeSeconds += delta;
        clearRun.playTimeSeconds += delta;
    }

    public void ResetAll()
    {
        sessionTotal.ResetCounters();
        clearRun.ResetCounters();
        currentChapterStats.ResetCounters();

        sessionPurifiedIds.Clear();
        clearRunPurifiedIds.Clear();
        currentChapterPurifiedIds.Clear();
        sessionAcquiredItems.Clear();

        clearedFactoryCount = 0;
        reachedFactory = 1;
        gameOverCount = 0;
        isChapterTrackingActive = false;

        for (int i = 0; i < chapterSnapshots.Length; i++)
            chapterSnapshots[i] = default;
    }

    public void BeginClearRun()
    {
        clearRun.ResetCounters();
        clearRunPurifiedIds.Clear();
    }

    public void BeginCurrentChapterStats()
    {
        currentChapterStats.ResetCounters();
        currentChapterPurifiedIds.Clear();
        isChapterTrackingActive = true;
    }

    public void NotifyFactoryEntered(int chapterIndex)
    {
        if (chapterIndex > reachedFactory)
            reachedFactory = chapterIndex;
    }

    public bool TryRecordPurification(string monsterId)
    {
        if (string.IsNullOrEmpty(monsterId))
            return false;

        if (!currentChapterPurifiedIds.Add(monsterId))
            return false;

        currentChapterStats.purifiedMonsters++;
        sessionPurifiedIds.Add(monsterId);
        sessionTotal.purifiedMonsters++;
        clearRunPurifiedIds.Add(monsterId);
        clearRun.purifiedMonsters++;
        return true;
    }

    public void RecordEscape()
    {
        float penalty = SessionGradeCalculator.EscapePenaltyPerCount;

        currentChapterStats.escapeCount++;
        currentChapterStats.escapePenaltyTotal += penalty;

        sessionTotal.escapeCount++;
        sessionTotal.escapePenaltyTotal += penalty;

        clearRun.escapeCount++;
        clearRun.escapePenaltyTotal += penalty;
    }

    public void RecordGameOver()
    {
        gameOverCount++;
    }

    public void RecordSessionItem(string itemId, int count = 1)
    {
        if (string.IsNullOrEmpty(itemId) || count <= 0)
            return;

        for (int i = 0; i < sessionAcquiredItems.Count; i++)
        {
            if (sessionAcquiredItems[i].itemId != itemId)
                continue;

            sessionAcquiredItems[i] = new InventoryManager.StackedInventoryItem(
                itemId,
                sessionAcquiredItems[i].count + count);
            return;
        }

        sessionAcquiredItems.Add(new InventoryManager.StackedInventoryItem(itemId, count));
    }

    public void SaveChapterSnapshot(int chapterIndex, float finalOxygenPercent)
    {
        if (!TryGetSnapshotIndex(chapterIndex, out int snapshotIndex))
        {
            Debug.LogWarning($"[PlaySessionStats] Invalid chapter index for snapshot: {chapterIndex}");
            return;
        }

        isChapterTrackingActive = false;

        currentChapterStats.finalOxygenPercent = Mathf.Clamp(finalOxygenPercent, 0f, 100f);

        int score = SessionGradeCalculator.CalculateScoreFromStatBlock(currentChapterStats);
        SessionGrade grade = SessionGradeCalculator.FromStatBlock(currentChapterStats);

        chapterSnapshots[snapshotIndex] = new ChapterSnapshot
        {
            chapterIndex = chapterIndex,
            isCleared = true,
            purifiedMonsters = currentChapterStats.purifiedMonsters,
            escapeCount = currentChapterStats.escapeCount,
            finalOxygenPercent = currentChapterStats.finalOxygenPercent,
            playTimeSeconds = currentChapterStats.playTimeSeconds,
            score = score,
            grade = grade
        };

        if (chapterIndex > clearedFactoryCount)
            clearedFactoryCount = chapterIndex;

        if (chapterIndex == ChapterSnapshotCount)
            clearRun.finalOxygenPercent = currentChapterStats.finalOxygenPercent;
    }

    public SessionGrade GetMainGrade()
    {
        return SessionGradeCalculator.FromStatBlock(clearRun);
    }

    public int GetMainScore()
    {
        return SessionGradeCalculator.CalculateScoreFromStatBlock(clearRun);
    }

    public string GetMainTitle()
    {
        return SessionGradeCalculator.GetTitle(GetMainGrade());
    }

    public ChapterSnapshot? GetChapterSnapshot(int chapterIndex)
    {
        if (!TryGetSnapshotIndex(chapterIndex, out int snapshotIndex))
            return null;

        ChapterSnapshot snapshot = chapterSnapshots[snapshotIndex];
        return snapshot.isCleared ? snapshot : (ChapterSnapshot?)null;
    }

    private void EnsureChapterSnapshotArray()
    {
        if (chapterSnapshots == null || chapterSnapshots.Length != ChapterSnapshotCount)
            chapterSnapshots = new ChapterSnapshot[ChapterSnapshotCount];
    }

    private static bool TryGetSnapshotIndex(int chapterIndex, out int snapshotIndex)
    {
        snapshotIndex = chapterIndex - 1;
        return chapterIndex >= 1 && chapterIndex <= ChapterSnapshotCount;
    }

#if UNITY_EDITOR
    [ContextMenu("Debug/Log Session Stats")]
    private void DebugLogSessionStats()
    {
        Debug.Log(
            $"[PlaySessionStats] sessionTotal: purified={sessionTotal.purifiedMonsters}, "
            + $"escape={sessionTotal.escapeCount}, oxygen={sessionTotal.finalOxygenPercent:F1}%, "
            + $"playTime={sessionTotal.playTimeSeconds:F1}s, gameOver={gameOverCount}");

        Debug.Log(
            $"[PlaySessionStats] clearRun: purified={clearRun.purifiedMonsters}, "
            + $"escape={clearRun.escapeCount}, oxygen={clearRun.finalOxygenPercent:F1}%, "
            + $"score={GetMainScore()}, grade={GetMainGrade()}, title={GetMainTitle()}");

        for (int i = 0; i < chapterSnapshots.Length; i++)
        {
            ChapterSnapshot snap = chapterSnapshots[i];
            if (!snap.isCleared)
                continue;

            Debug.Log(
                $"[PlaySessionStats] chapterSnapshots[{i + 1}]: score={snap.score}, "
                + $"grade={snap.grade}, purified={snap.purifiedMonsters}, escape={snap.escapeCount}");
        }
    }

    [ContextMenu("Debug/Simulate Sample Clear Run")]
    private void DebugSimulateSampleClearRun()
    {
        ResetAll();
        BeginClearRun();
        BeginCurrentChapterStats();

        TryRecordPurification("monster_a");
        TryRecordPurification("monster_b");
        TryRecordPurification("monster_a");
        RecordEscape();

        SaveChapterSnapshot(3, 65f);

        DebugLogSessionStats();
    }
#endif
}

[Serializable]
public class StatBlock
{
    public int purifiedMonsters;
    public int escapeCount;
    public float escapePenaltyTotal;
    public float playTimeSeconds;
    public float finalOxygenPercent;

    public void ResetCounters()
    {
        purifiedMonsters = 0;
        escapeCount = 0;
        escapePenaltyTotal = 0f;
        playTimeSeconds = 0f;
        finalOxygenPercent = 0f;
    }
}

[Serializable]
public struct ChapterSnapshot
{
    public int chapterIndex;
    public bool isCleared;
    public int purifiedMonsters;
    public int escapeCount;
    public float finalOxygenPercent;
    public float playTimeSeconds;
    public int score;
    public SessionGrade grade;
}
