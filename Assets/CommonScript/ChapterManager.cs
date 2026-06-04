using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 씬 로드 없이 챕터 프리팹 활성/비활성으로 맵을 전환합니다.
/// Inspector에 챕터 루트 오브젝트를 순서대로 등록하세요.
/// </summary>
public class ChapterManager : MonoBehaviour
{
    public static ChapterManager Instance { get; private set; }

    public const string SpawnObjectName = "PlayerSpawn";
    public const string AlternateSpawnObjectName = "SpawnPoint";

    private const string CurrentChapterPrefsKey = "SG_CurrentFactoryChapter";

    [Header("--- 챕터 프리팹 (순서 = 챕터 1, 2, 3 …) ---")]
    [SerializeField] private List<GameObject> chapterPrefabs = new List<GameObject>();

    [Header("--- 저장 ---")]
    [SerializeField] private bool persistChapterIndex = true;

    [Tooltip("씬 진입·에디터 Play 시 항상 챕터 1. (플레이 중 진행은 저장, 씬 재로드 이어하기는 ApplySavedChapter)")]
    [SerializeField] private bool startFromFirstChapterOnSceneLoad = true;

    /// <summary>1-based 현재 챕터 번호.</summary>
    public int CurrentChapterIndex { get; private set; } = 1;

    public int ChapterCount => chapterPrefabs != null ? chapterPrefabs.Count : 0;

    public bool IsTransitionInProgress { get; private set; }

    public event Action<ChapterLoadedEventArgs> OnChapterLoaded;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        InitializeChapters();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        RemoveNullChapterPrefabSlots();
    }

    private void RemoveNullChapterPrefabSlots()
    {
        if (chapterPrefabs == null)
            return;

        for (int i = chapterPrefabs.Count - 1; i >= 0; i--)
        {
            if (chapterPrefabs[i] == null)
                chapterPrefabs.RemoveAt(i);
        }
    }
#endif

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public static ChapterManager EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        ChapterManager existing =
            FindAnyObjectByType<ChapterManager>(FindObjectsInactive.Include);
        if (existing != null)
            return existing;

        GameObject host = GameObject.Find("Managers") ?? new GameObject("ChapterManager");
        return host.AddComponent<ChapterManager>();
    }

    /// <summary>저장된 챕터 인덱스를 적용합니다. 씬 진입·전체 리셋 후 호출.</summary>
    public void ApplySavedChapter()
    {
        int chapter = persistChapterIndex && PlayerPrefs.HasKey(CurrentChapterPrefsKey)
            ? PlayerPrefs.GetInt(CurrentChapterPrefsKey, 1)
            : DetectActiveChapterIndex();

        ActivateChapter(chapter, savePrefs: false, isRestart: false, refreshGameplay: false);
    }

    /// <summary>새 플레이 세션: 저장 챕터를 지우고 챕터 1을 활성화합니다.</summary>
    public void BeginNewPlaySession()
    {
        ClearSavedChapter();
        DeactivateAllChapterPrefabs();
        ActivateChapter(1, savePrefs: persistChapterIndex, isRestart: false, refreshGameplay: false);
        Debug.Log("[ChapterManager] 새 플레이 — 챕터 1부터 시작");
    }

    public void ResetToFirstChapter()
    {
        ClearSavedChapter();
        DeactivateAllChapterPrefabs();
        ActivateChapter(1, savePrefs: true, isRestart: false, refreshGameplay: true);
    }

    /// <summary>다음 챕터로 전환. 성공 시 true.</summary>
    public bool LoadNextChapter(out string resultMessage)
    {
        if (IsTransitionInProgress)
        {
            resultMessage = "챕터 전환이 이미 진행 중입니다.";
            return false;
        }

        if (CurrentChapterIndex >= ChapterCount)
        {
            resultMessage = "마지막 챕터입니다.";
            return false;
        }

        int nextIndex = CurrentChapterIndex + 1;
        if (!IsValidChapterSlot(nextIndex))
        {
            resultMessage = $"챕터 {nextIndex} 프리팹이 할당되지 않았습니다.";
            return false;
        }

        GameManager.Instance?.SaveFactoryCheckpoint();

        IsTransitionInProgress = true;
        try
        {
            ActivateChapter(nextIndex, savePrefs: true, isRestart: false, refreshGameplay: true);
            resultMessage = $"챕터 {nextIndex}(으)로 이동했습니다.";
            return true;
        }
        finally
        {
            IsTransitionInProgress = false;
        }
    }

    /// <summary>씬 재로드 없이 현재 챕터만 초기화. 오염도 등 데이터는 호출 전 GameManager에서 처리하세요.</summary>
    public void RestartCurrentChapter()
    {
        if (IsTransitionInProgress)
        {
            Debug.LogWarning("[ChapterManager] 챕터 전환 중에는 재시작할 수 없습니다.");
            return;
        }

        if (!IsValidChapterSlot(CurrentChapterIndex))
        {
            Debug.LogError($"[ChapterManager] 현재 챕터 {CurrentChapterIndex} 프리팹이 없습니다.");
            return;
        }

        IsTransitionInProgress = true;
        try
        {
            GameObject chapterRoot = chapterPrefabs[CurrentChapterIndex - 1];
            if (chapterRoot != null && chapterRoot.activeSelf)
                chapterRoot.SetActive(false);

            ActivateChapter(CurrentChapterIndex, savePrefs: false, isRestart: true, refreshGameplay: true);
        }
        finally
        {
            IsTransitionInProgress = false;
        }
    }

    public static void ClearSavedChapter()
    {
        PlayerPrefs.DeleteKey(CurrentChapterPrefsKey);
        PlayerPrefs.Save();
    }

    public GameObject GetActiveChapterRoot()
    {
        if (!IsValidChapterSlot(CurrentChapterIndex))
            return null;

        return chapterPrefabs[CurrentChapterIndex - 1];
    }

    private void InitializeChapters()
    {
        if (chapterPrefabs == null || chapterPrefabs.Count == 0)
        {
            Debug.LogWarning("[ChapterManager] chapterPrefabs가 비어 있습니다. Inspector에 챕터 루트를 등록하세요.");
            return;
        }

        if (startFromFirstChapterOnSceneLoad)
        {
            BeginNewPlaySession();
            return;
        }

        DeactivateAllChapterPrefabs();

        int startChapter = persistChapterIndex && PlayerPrefs.HasKey(CurrentChapterPrefsKey)
            ? PlayerPrefs.GetInt(CurrentChapterPrefsKey, 1)
            : DetectActiveChapterIndex();

        ActivateChapter(startChapter, savePrefs: !PlayerPrefs.HasKey(CurrentChapterPrefsKey), isRestart: false,
            refreshGameplay: false);
    }

    private void DeactivateAllChapterPrefabs()
    {
        if (chapterPrefabs == null)
            return;

        for (int i = 0; i < chapterPrefabs.Count; i++)
        {
            GameObject chapter = chapterPrefabs[i];
            if (chapter != null && chapter.activeSelf)
                chapter.SetActive(false);
        }
    }

    private void ActivateChapter(int chapterIndex, bool savePrefs, bool isRestart, bool refreshGameplay)
    {
        if (chapterPrefabs == null || chapterPrefabs.Count == 0)
            return;

        CurrentChapterIndex = Mathf.Clamp(chapterIndex, 1, chapterPrefabs.Count);

        for (int i = 0; i < chapterPrefabs.Count; i++)
        {
            GameObject chapter = chapterPrefabs[i];
            if (chapter == null)
                continue;

            bool shouldActivate = i + 1 == CurrentChapterIndex;
            if (chapter.activeSelf != shouldActivate)
                chapter.SetActive(shouldActivate);
        }

        if (savePrefs && persistChapterIndex)
        {
            PlayerPrefs.SetInt(CurrentChapterPrefsKey, CurrentChapterIndex);
            PlayerPrefs.Save();
        }

        if (refreshGameplay)
            RefreshChapterGameplay(includeSpawnerReset: true);

        TeleportPlayerToActiveChapterSpawn();
        PublishChapterLoaded(isRestart);

        Debug.Log($"[ChapterManager] 활성 챕터: {CurrentChapterIndex} (restart={isRestart})");
    }

    private void RefreshChapterGameplay(bool includeSpawnerReset)
    {
        GameManager.Instance?.ResetToField();

        if (UIManager.Instance != null)
            UIManager.Instance.CloseBattleUI();

        UIBattleManager.ResetSavedContaminationProgress();

        UIResult[] resultPanels = FindObjectsByType<UIResult>(FindObjectsInactive.Include);
        for (int i = 0; i < resultPanels.Length; i++)
            resultPanels[i]?.ResetStageResultState();

        if (includeSpawnerReset)
            RespawnSpawnersInActiveChapter();
    }

    private void RespawnSpawnersInActiveChapter()
    {
        GameObject activeChapter = GetActiveChapterRoot();
        if (activeChapter == null)
            return;

        MonsterSpawner[] monsterSpawners =
            activeChapter.GetComponentsInChildren<MonsterSpawner>(true);
        for (int i = 0; i < monsterSpawners.Length; i++)
            monsterSpawners[i]?.RespawnCurrentStage();

        ItemSpawner[] itemSpawners =
            activeChapter.GetComponentsInChildren<ItemSpawner>(true);
        for (int i = 0; i < itemSpawners.Length; i++)
            itemSpawners[i]?.RespawnCurrentStage();
    }

    private void TeleportPlayerToActiveChapterSpawn()
    {
        GameObject activeChapter = GetActiveChapterRoot();
        if (activeChapter == null)
            return;

        Transform spawn = FindSpawnPoint(activeChapter.transform);
        if (spawn == null)
        {
            Debug.LogWarning(
                $"[ChapterManager] 챕터 {CurrentChapterIndex}에서 스폰 포인트를 찾지 못했습니다. " +
                $"하위에 '{SpawnObjectName}' 또는 '{AlternateSpawnObjectName}' 이름의 빈 오브젝트를 추가하세요.");
            return;
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
            return;

        TeleportWithPhysicsSafeguard(player, spawn.position);
    }

    private static void TeleportWithPhysicsSafeguard(GameObject player, Vector3 worldPosition)
    {
        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        bool restoreSimulated = rb == null || rb.simulated;

        if (rb != null)
        {
            rb.simulated = false;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        player.transform.position = worldPosition;

        if (rb != null)
            rb.simulated = restoreSimulated;
    }

    private static Transform FindSpawnPoint(Transform chapterRoot)
    {
        Transform byName = FindChildByName(chapterRoot, SpawnObjectName);
        if (byName != null)
            return byName;

        return FindChildByName(chapterRoot, AlternateSpawnObjectName);
    }

    private static Transform FindChildByName(Transform parent, string childName)
    {
        if (parent == null)
            return null;

        if (parent.name == childName)
            return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindChildByName(parent.GetChild(i), childName);
            if (found != null)
                return found;
        }

        return null;
    }

    private int DetectActiveChapterIndex()
    {
        if (chapterPrefabs == null)
            return 1;

        for (int i = 0; i < chapterPrefabs.Count; i++)
        {
            GameObject chapter = chapterPrefabs[i];
            if (chapter != null && chapter.activeSelf)
                return i + 1;
        }

        return 1;
    }

    private bool IsValidChapterSlot(int oneBasedIndex)
    {
        if (chapterPrefabs == null || oneBasedIndex < 1 || oneBasedIndex > chapterPrefabs.Count)
            return false;

        return chapterPrefabs[oneBasedIndex - 1] != null;
    }

    private void PublishChapterLoaded(bool isRestart)
    {
        var args = new ChapterLoadedEventArgs(CurrentChapterIndex, GetActiveChapterRoot(), isRestart);
        OnChapterLoaded?.Invoke(args);
    }
}

public readonly struct ChapterLoadedEventArgs
{
    public int ChapterIndex { get; }
    public GameObject ChapterRoot { get; }
    public bool IsRestart { get; }

    public ChapterLoadedEventArgs(int chapterIndex, GameObject chapterRoot, bool isRestart)
    {
        ChapterIndex = chapterIndex;
        ChapterRoot = chapterRoot;
        IsRestart = isRestart;
    }
}
