using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 씬 로드 없이 챕터 프리팹 활성/비활성으로 맵을 전환합니다.
/// Inspector에 챕터 루트 오브젝트를 순서대로 등록하세요.
/// </summary>
public class ChapterManager : MonoBehaviour
{
    public static ChapterManager Instance { get; private set; }

    public const string SpawnObjectName = "PlayerSpawn";
    public const string AlternateSpawnObjectName = "SpawnPoint";
    public const string ChapterObjectTag = "Chapter";

    public const string CurrentChapterPrefsKey = "SG_CurrentFactoryChapter";

    [Header("--- 챕터 프리팹 (순서 = 챕터 1, 2, 3 …) ---")]
    [SerializeField] private List<GameObject> chapterPrefabs = new List<GameObject>();

    [Tooltip("재시작 시 Instantiate할 프로젝트 프리팹. 비어 있으면 에디터에서 씬 인스턴스로 자동 채웁니다.")]
    [SerializeField] private List<GameObject> chapterPrefabSources = new List<GameObject>();

    [Tooltip("챕터 인스턴스가 생성될 부모. 비어 있으면 첫 챕터 슬롯의 부모(또는 씬 루트)를 사용합니다.")]
    [SerializeField] private Transform chapterInstancesParent;

    [Header("--- FactoryStage (챕터 재시작 Destroy/Instantiate) ---")]
    [Tooltip("현재 챕터에서 시작 시 FactoryStage_* 루트를 재생성할 프로젝트 프리팹. 비어 있으면 씬 인스턴스에서 추론합니다.")]
    [SerializeField] private List<GameObject> factoryStagePrefabSources = new List<GameObject>();

    [Header("--- 저장 ---")]
    [SerializeField] private bool persistChapterIndex = true;

    [Tooltip("씬 진입·에디터 Play 시 항상 챕터 1. (플레이 중 진행은 저장, 씬 재로드 이어하기는 ApplySavedChapter)")]
    [SerializeField] private bool startFromFirstChapterOnSceneLoad = true;

    /// <summary>1-based 현재 챕터 번호.</summary>
    public int CurrentChapterIndex { get; private set; } = 1;

    public int ChapterCount => chapterPrefabs != null ? chapterPrefabs.Count : 0;

    public bool IsTransitionInProgress { get; private set; }

    public event Action<ChapterLoadedEventArgs> OnChapterLoaded;

    private Coroutine restartChapterCoroutine;
    private Transform cachedChapterInstancesParent;
    private GameObject[] chapterTemplateCache;
    private int lastBeginNewPlaySessionFrame = -1;
    private readonly List<FactoryStageRebuildData> pendingFactoryStageRebuild = new List<FactoryStageRebuildData>();
    private readonly List<ChapterInstanceSnapshot> pendingChapterRebuildSnapshots = new List<ChapterInstanceSnapshot>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        CacheChapterInstancesParent();
        RefreshChapterTemplateCache();
        InitializeChapters();
    }

    private void CacheChapterInstancesParent()
    {
        if (chapterInstancesParent != null)
        {
            cachedChapterInstancesParent = chapterInstancesParent;
            return;
        }

        if (chapterPrefabs == null)
            return;

        for (int i = 0; i < chapterPrefabs.Count; i++)
        {
            GameObject chapter = chapterPrefabs[i];
            if (chapter == null)
                continue;

            cachedChapterInstancesParent = chapter.transform.parent;
            return;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        RemoveNullChapterPrefabSlots();
        SyncChapterPrefabSourcesFromSceneInstances();
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

    private void SyncChapterPrefabSourcesFromSceneInstances()
    {
        if (chapterPrefabs == null)
            return;

        EnsureChapterSourceListSize();

        for (int i = 0; i < chapterPrefabs.Count; i++)
        {
            if (chapterPrefabSources[i] != null && IsProjectPrefabAsset(chapterPrefabSources[i]))
                continue;

            if (chapterPrefabs[i] == null)
                continue;

            GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(chapterPrefabs[i]);
            if (source != null)
                chapterPrefabSources[i] = source;
        }

        RefreshChapterTemplateCache();
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

    /// <summary>새 플레이 세션: 저장 챕터를 지우고 챕터 1을 활성화합니다. 초기 데이터는 GameManager가 먼저 적용해야 합니다.</summary>
    public void BeginNewPlaySession()
    {
        int frame = Time.frameCount;
        if (frame == lastBeginNewPlaySessionFrame)
        {
            Debug.LogWarning(
                "[ChapterManager] 같은 프레임에 BeginNewPlaySession이 중복 호출되어 FactoryStage만 다시 스폰합니다.");
            RespawnFactoryStageFieldEntities(resetToFirstStage: true);
            return;
        }

        lastBeginNewPlaySessionFrame = frame;
        CurrentChapterIndex = 1;
        BeginPlaySessionAtChapter(1, isRestart: false, "새 플레이");
    }

    /// <summary>현재 챕터 재시작: 월드 Destroy/Instantiate 직후. 처음부터 다시 시작과 동일하게 스폰합니다.</summary>
    public void BeginCurrentChapterPlaySession()
    {
        BeginPlaySessionAtChapter(CurrentChapterIndex, isRestart: true, "현재 챕터 재시작");
    }

    private void BeginPlaySessionAtChapter(int oneBasedChapterIndex, bool isRestart, string logLabel)
    {
        if (!PreparePlaySessionChapter(oneBasedChapterIndex, isRestart))
            return;

        if (!isActiveAndEnabled)
        {
            SpawnFieldEntitiesForPlaySession();
            TeleportPlayerToCurrentChapterSpawn();
            return;
        }

        StartCoroutine(BeginPlaySessionSpawnRoutine(logLabel, oneBasedChapterIndex));
    }

    private bool PreparePlaySessionChapter(int oneBasedChapterIndex, bool isRestart)
    {
        GameManager.ActivateGameplayWorldSceneRoots();

        EnsureAllChapterInstances();

        if (!EnsureChapterInstance(oneBasedChapterIndex))
        {
            Debug.LogError(
                $"[ChapterManager] 챕터 {oneBasedChapterIndex} 인스턴스를 생성하지 못했습니다. " +
                "Chapter Prefab Sources에 Project 프리팹(씬 오브젝트 X)을 연결하세요.");
            return false;
        }

        RestoreAllChapterInstanceTransforms();
        ActivateChapter(oneBasedChapterIndex, savePrefs: persistChapterIndex, isRestart: isRestart, refreshGameplay: false);
        return true;
    }

    private IEnumerator BeginPlaySessionSpawnRoutine(string logLabel, int oneBasedChapterIndex)
    {
        yield return null;

        SpawnFieldEntitiesForPlaySession();
        TeleportPlayerToCurrentChapterSpawn();
        ScheduleTeleportPlayerAfterFrame();
        Debug.Log($"[ChapterManager] {logLabel} — 챕터 {oneBasedChapterIndex}·FactoryStage·몬스터·아이템 스테이지 1부터 재스폰");
    }

    private void SpawnFieldEntitiesForPlaySession()
    {
        ActivateCurrentFactoryStageRoot();
        ClearAllSpawnedMonstersAndItemsInScene();
        RespawnFactoryStageFieldEntities(resetToFirstStage: true);
        RespawnActiveChapterFieldEntities(resetToFirstStage: true);
        RefreshFieldEntitiesVisibility();
    }

    /// <summary>1프레임 뒤 플레이어 스폰을 한 번 더 맞춥니다(오프닝 종료·Managers 활성화 타이밍 보정).</summary>
    public void ScheduleTeleportPlayerAfterFrame()
    {
        if (!isActiveAndEnabled)
        {
            TeleportPlayerToCurrentChapterSpawn();
            return;
        }

        StartCoroutine(TeleportPlayerNextFrameRoutine());
    }

    private IEnumerator TeleportPlayerNextFrameRoutine()
    {
        yield return null;
        TeleportPlayerToCurrentChapterSpawn();
    }

    /// <summary>활성 챕터의 PlayerSpawn(또는 SpawnPoint)으로 플레이어·카메라를 맞춥니다.</summary>
    public void TeleportPlayerToCurrentChapterSpawn()
    {
        GameObject activeChapter = GetActiveChapterRoot();
        if (activeChapter == null)
        {
            Debug.LogWarning("[ChapterManager] 활성 챕터가 없어 플레이어 스폰 이동을 건너뜁니다.");
            return;
        }

        Transform spawn = FindSpawnPoint(activeChapter.transform);
        Vector3 spawnPosition = spawn != null ? spawn.position : activeChapter.transform.position;

        if (spawn == null)
        {
            Debug.LogWarning(
                $"[ChapterManager] 챕터 {CurrentChapterIndex}에 '{SpawnObjectName}' / '{AlternateSpawnObjectName}'가 없어 " +
                "챕터 루트 위치로 이동합니다. 프리팹에 PlayerSpawn 빈 오브젝트를 추가하세요.");
        }

        GameObject player = FindPlayerObject();
        if (player == null)
        {
            Debug.LogWarning("[ChapterManager] Player를 찾지 못해 스폰 이동을 건너뜁니다.");
            return;
        }

        TeleportWithPhysicsSafeguard(player, spawnPosition);
        FocusCameraOnChapterEntrance(spawnPosition);
    }

    /// <summary>씬에 남은 몬스터·아이템(챕터 Destroy 후 고아 포함)을 모두 제거합니다.</summary>
    public void ClearAllSpawnedMonstersAndItemsInScene()
    {
        MonsterSpawner[] monsterSpawners =
            FindObjectsByType<MonsterSpawner>(FindObjectsInactive.Include);
        for (int i = 0; i < monsterSpawners.Length; i++)
            monsterSpawners[i]?.ForceClearAllSpawned();

        ItemSpawner[] itemSpawners =
            FindObjectsByType<ItemSpawner>(FindObjectsInactive.Include);
        for (int i = 0; i < itemSpawners.Length; i++)
            itemSpawners[i]?.ForceClearAllSpawned();

        DestroySceneObjectsByTag("Monster");
        DestroySceneObjectsWithComponent<ItemPickup>();
    }

    /// <summary>씬에 있는 모든 챕터 인스턴스를 제거합니다(처음부터 다시 시작).</summary>
    public void DestroyAllChapterInstances()
    {
        if (chapterPrefabs != null)
        {
            for (int i = 0; i < chapterPrefabs.Count; i++)
            {
                GameObject chapter = chapterPrefabs[i];
                if (chapter != null)
                    Destroy(chapter);

                chapterPrefabs[i] = null;
            }
        }

        DestroyOrphanChaptersByTag();
        CurrentChapterIndex = 1;
        Debug.Log("[ChapterManager] 모든 챕터 인스턴스 Destroy 완료");
    }

    /// <summary>슬롯에 챕터 인스턴스가 없으면 source 프리팹에서 새로 만듭니다.</summary>
    public bool EnsureChapterInstance(int oneBasedChapterIndex)
    {
        if (!HasChapterSlot(oneBasedChapterIndex))
            return false;

        int slot = oneBasedChapterIndex - 1;
        GameObject existing = chapterPrefabs[slot];
        if (existing != null)
        {
            if (existing)
                return true;

            chapterPrefabs[slot] = null;
        }

        GameObject template = ResolveChapterTemplate(slot);
        if (template == null)
        {
            Debug.LogError(
                $"[ChapterManager] 챕터 {oneBasedChapterIndex} 템플릿이 없습니다. " +
                $"chapterPrefabSources[{slot}]에 Assets/2.SLA/Prefabs/FactoryMaps 프리팹을 할당하세요.");
            return false;
        }

        Transform parent = ResolveChapterInstancesParent(slot);
        GameObject instance = parent != null ? Instantiate(template, parent) : Instantiate(template);
        instance.name = GetChapterInstanceName(slot, template);
        instance.SetActive(false);
        chapterPrefabs[slot] = instance;
        return true;
    }

    /// <summary>ChapterMaps 아래 챕터 1·2·3 슬롯 인스턴스가 모두 존재하도록 보장합니다(없으면 프리팹에서 생성, 기본은 비활성).</summary>
    public void EnsureAllChapterInstances()
    {
        if (chapterPrefabs == null)
            return;

        for (int i = 0; i < chapterPrefabs.Count; i++)
            EnsureChapterInstance(i + 1);
    }

    private void ReinstantiateAllChapterSlotsAfterDestroy()
    {
        if (chapterPrefabs == null)
            return;

        for (int i = 0; i < chapterPrefabs.Count; i++)
            EnsureChapterInstance(i + 1);
    }

    private static void ActivateChapterHierarchy(GameObject chapterRoot, bool shouldActivate)
    {
        if (chapterRoot == null)
            return;

        if (chapterRoot.activeSelf != shouldActivate)
            chapterRoot.SetActive(shouldActivate);

        if (shouldActivate)
            GameManager.ActivateHierarchyDeep(chapterRoot);
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
            InventoryManager.Instance?.ClearInventory();
            resultMessage = $"챕터 {nextIndex}(으)로 이동했습니다.";
            return true;
        }
        finally
        {
            IsTransitionInProgress = false;
        }
    }

    /// <summary>씬 재로드 없이 현재 챕터를 프리팹 재생성으로 초기화합니다. 오염도 등은 GameManager.PerformChapterReset 후 호출하세요.</summary>
    public void RestartCurrentChapter()
    {
        if (restartChapterCoroutine != null)
            StopCoroutine(restartChapterCoroutine);

        restartChapterCoroutine = StartCoroutine(RebuildWorldAndBeginCurrentChapterSession());
    }

    /// <summary>
    /// 현재 챕터부터 다시 시작: 모든 챕터·FactoryStage Destroy → 프리팹 Instantiate →
    /// <see cref="BeginCurrentChapterPlaySession"/> (처음부터 다시 시작과 동일 스폰).
    /// </summary>
    public IEnumerator RebuildWorldAndBeginCurrentChapterSession()
    {
        if (IsTransitionInProgress)
        {
            Debug.LogWarning("[ChapterManager] 챕터 전환 중에는 재시작할 수 없습니다.");
            yield break;
        }

        int chapterIndex = CurrentChapterIndex;
        if (!IsValidChapterSlot(chapterIndex))
        {
            Debug.LogError($"[ChapterManager] 현재 챕터 {chapterIndex} 프리팹이 없습니다.");
            yield break;
        }

        IsTransitionInProgress = true;

        try
        {
            GameManager.ActivateGameplayWorldSceneRoots();

            DestroyAllChaptersForChapterRestart();
            QueueDestroyFactoryStagesForChapterRestart();

            yield return null;

            InstantiateQueuedFactoryStages();
            ActivateCurrentFactoryStageRoot();

            yield return null;

            ReinstantiateAllChapterSlotsAfterDestroy();

            if (!PreparePlaySessionChapter(chapterIndex, isRestart: true))
                yield break;

            SpawnFieldEntitiesForPlaySession();
            TeleportPlayerToCurrentChapterSpawn();
            ScheduleTeleportPlayerAfterFrame();

            yield return null;

            RefreshFieldEntitiesVisibility();
            RestoreFieldPhysicsAfterChapterReset();
            MapInitializer.RefreshActiveMapColliders();
            Debug.Log(
                $"[ChapterManager] 챕터 {chapterIndex} — 전체 Destroy/Instantiate 후 필드 재구성 완료");
        }
        finally
        {
            IsTransitionInProgress = false;
            restartChapterCoroutine = null;
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
            if (GameManager.Instance != null && GameManager.Instance.IsAwaitingPostOpeningPlaySession)
            {
                DeactivateAllChapterPrefabs();
                return;
            }

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

    /// <summary>오프닝 시퀀스 등 메인 플레이 전에 모든 챕터 프리팹을 끕니다.</summary>
    public void DeactivateAllChaptersForOpening()
    {
        DeactivateAllChapterPrefabs();
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

    private void DeactivateAllChapterPrefabsExcept(int oneBasedChapterIndex)
    {
        if (chapterPrefabs == null)
            return;

        for (int i = 0; i < chapterPrefabs.Count; i++)
        {
            GameObject chapter = chapterPrefabs[i];
            if (chapter == null)
                continue;

            bool shouldStayActive = i + 1 == oneBasedChapterIndex;
            if (chapter.activeSelf != shouldStayActive)
                chapter.SetActive(shouldStayActive);
        }
    }

    private void DestroyAllChaptersForChapterRestart()
    {
        ClearAllSpawnedMonstersAndItemsInScene();
        CaptureChapterInstanceSnapshots();

        if (chapterPrefabs != null)
        {
            for (int i = 0; i < chapterPrefabs.Count; i++)
            {
                if (chapterPrefabs[i] != null)
                    Destroy(chapterPrefabs[i]);

                chapterPrefabs[i] = null;
            }
        }

        DestroyOrphanChaptersByTag();
    }

    private void CaptureChapterInstanceSnapshots()
    {
        pendingChapterRebuildSnapshots.Clear();

        if (chapterPrefabs == null)
            return;

        for (int i = 0; i < chapterPrefabs.Count; i++)
        {
            GameObject chapter = chapterPrefabs[i];
            if (chapter == null)
                continue;

            Transform transform = chapter.transform;
            pendingChapterRebuildSnapshots.Add(new ChapterInstanceSnapshot
            {
                OneBasedChapterIndex = i + 1,
                WorldPosition = transform.position,
                WorldRotation = transform.rotation,
                LocalScale = transform.localScale,
                Parent = transform.parent,
                SiblingIndex = transform.GetSiblingIndex(),
                Name = chapter.name,
            });
        }
    }

    private void RestoreAllChapterInstanceTransforms()
    {
        if (pendingChapterRebuildSnapshots.Count == 0)
            return;

        for (int i = 0; i < pendingChapterRebuildSnapshots.Count; i++)
            ApplyChapterInstanceSnapshot(pendingChapterRebuildSnapshots[i]);

        pendingChapterRebuildSnapshots.Clear();
    }

    private void ApplyChapterInstanceSnapshot(ChapterInstanceSnapshot snapshot)
    {
        if (!IsValidChapterSlot(snapshot.OneBasedChapterIndex))
            return;

        GameObject chapter = chapterPrefabs[snapshot.OneBasedChapterIndex - 1];
        if (chapter == null)
            return;

        Transform transform = chapter.transform;
        if (snapshot.Parent != null)
        {
            transform.SetParent(snapshot.Parent, false);
            transform.SetSiblingIndex(snapshot.SiblingIndex);
        }

        transform.SetPositionAndRotation(snapshot.WorldPosition, snapshot.WorldRotation);
        transform.localScale = snapshot.LocalScale;

        if (!string.IsNullOrEmpty(snapshot.Name))
            chapter.name = snapshot.Name;
    }

    private void QueueDestroyFactoryStagesForChapterRestart()
    {
        pendingFactoryStageRebuild.Clear();

        List<GameObject> sceneRoots = FindFactoryStageSceneRoots();
        if (sceneRoots.Count == 0)
        {
            Debug.LogWarning("[ChapterManager] FactoryStage 씬 루트가 없어 재생성을 건너뜁니다.");
            return;
        }

        EnsureFactoryStageSourceListSize(sceneRoots.Count);

        for (int i = 0; i < sceneRoots.Count; i++)
        {
            GameObject oldRoot = sceneRoots[i];
            GameObject template = ResolveFactoryStageTemplate(i, oldRoot);
            if (template == null)
            {
                Debug.LogError(
                    $"[ChapterManager] FactoryStage '{oldRoot.name}' 템플릿을 찾지 못했습니다. " +
                    "Factory Stage Prefab Sources에 Assets/3.ChangHEE/Prefab/FactoryStage_01_PrefabRoot 등을 연결하세요.");
                Destroy(oldRoot);
                continue;
            }

            MonsterSpawner oldMonsterSpawner = oldRoot.GetComponentInChildren<MonsterSpawner>(true);

            pendingFactoryStageRebuild.Add(new FactoryStageRebuildData
            {
                Template = template,
                Parent = oldRoot.transform.parent,
                SiblingIndex = oldRoot.transform.GetSiblingIndex(),
                Name = oldRoot.name,
                LocalPosition = oldRoot.transform.localPosition,
                LocalRotation = oldRoot.transform.localRotation,
                LocalScale = oldRoot.transform.localScale,
                WasActive = true,
                MonsterPrefabRefs = oldMonsterSpawner != null
                    ? oldMonsterSpawner.ExportMonsterPrefabReferences()
                    : null,
            });

            Destroy(oldRoot);
        }
    }

    private void InstantiateQueuedFactoryStages()
    {
        for (int i = 0; i < pendingFactoryStageRebuild.Count; i++)
        {
            FactoryStageRebuildData data = pendingFactoryStageRebuild[i];
            if (data.Template == null)
                continue;

            GameObject newRoot = data.Parent != null
                ? Instantiate(data.Template, data.Parent)
                : Instantiate(data.Template);

            newRoot.name = data.Name;
            newRoot.transform.SetSiblingIndex(data.SiblingIndex);
            newRoot.transform.localPosition = data.LocalPosition;
            newRoot.transform.localRotation = data.LocalRotation;
            newRoot.transform.localScale = data.LocalScale;
            GameManager.EnsureActiveInHierarchy(newRoot);

            MonsterSpawner newMonsterSpawner = newRoot.GetComponentInChildren<MonsterSpawner>(true);
            if (newMonsterSpawner != null)
                newMonsterSpawner.ImportMonsterPrefabReferences(data.MonsterPrefabRefs);
        }

        pendingFactoryStageRebuild.Clear();
    }

    private struct ChapterInstanceSnapshot
    {
        public int OneBasedChapterIndex;
        public Vector3 WorldPosition;
        public Quaternion WorldRotation;
        public Vector3 LocalScale;
        public Transform Parent;
        public int SiblingIndex;
        public string Name;
    }

    private struct FactoryStageRebuildData
    {
        public GameObject Template;
        public Transform Parent;
        public int SiblingIndex;
        public string Name;
        public Vector3 LocalPosition;
        public Quaternion LocalRotation;
        public Vector3 LocalScale;
        public bool WasActive;
        public GameObject[] MonsterPrefabRefs;
    }

    private static void EnsureMonsterBattleTrackerOnFactoryRoot(GameObject factoryRoot)
    {
        if (factoryRoot == null)
            return;

        MonsterSpawner spawner = factoryRoot.GetComponentInChildren<MonsterSpawner>(true);
        if (spawner == null)
            return;

        if (spawner.GetComponent<MonsterBattleTracker>() == null)
            spawner.gameObject.AddComponent<MonsterBattleTracker>();
    }

    private static List<GameObject> FindFactoryStageSceneRoots()
    {
        var roots = new List<GameObject>();
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
            return roots;

        GameObject[] sceneRoots = scene.GetRootGameObjects();
        for (int i = 0; i < sceneRoots.Length; i++)
        {
            GameObject root = sceneRoots[i];
            if (root != null && GameManager.IsFactoryStageSceneRootName(root.name))
                roots.Add(root);
        }

        return roots;
    }

    private void EnsureFactoryStageSourceListSize(int requiredCount)
    {
        if (factoryStagePrefabSources == null)
            factoryStagePrefabSources = new List<GameObject>();

        while (factoryStagePrefabSources.Count < requiredCount)
            factoryStagePrefabSources.Add(null);
    }

    private GameObject ResolveFactoryStageTemplate(int sourceIndex, GameObject sceneInstance)
    {
        if (factoryStagePrefabSources != null && sourceIndex >= 0 && sourceIndex < factoryStagePrefabSources.Count)
        {
            GameObject fromSource = ResolvePrefabAssetReference(factoryStagePrefabSources[sourceIndex]);
            if (fromSource != null)
                return fromSource;
        }

        if (sceneInstance != null)
        {
            GameObject fromInstance = ResolvePrefabAssetReference(sceneInstance);
            if (fromInstance != null)
                return fromInstance;
        }

#if UNITY_EDITOR
        GameObject fromPath = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/3.ChangHEE/Prefab/FactoryStage_01_PrefabRoot.prefab");
        if (fromPath != null)
            return fromPath;
#endif

        return null;
    }

    private GameObject RecreateChapterInstance(int oneBasedChapterIndex)
    {
        int slot = oneBasedChapterIndex - 1;
        GameObject oldInstance = chapterPrefabs[slot];
        if (oldInstance == null)
            return null;

        GameObject template = ResolveChapterTemplate(slot);
        if (template == null)
        {
            Debug.LogError(
                $"[ChapterManager] 챕터 {oneBasedChapterIndex} 템플릿 프리팹이 없습니다. " +
                "chapterPrefabSources에 프로젝트 프리팹을 할당하세요.");
            return null;
        }

        Transform parent = oldInstance.transform.parent;
        int siblingIndex = oldInstance.transform.GetSiblingIndex();
        string objectName = oldInstance.name;
        Vector3 localPosition = oldInstance.transform.localPosition;
        Quaternion localRotation = oldInstance.transform.localRotation;
        Vector3 localScale = oldInstance.transform.localScale;

        GameObject newInstance = Instantiate(template, parent);
        newInstance.name = objectName;
        newInstance.transform.SetSiblingIndex(siblingIndex);
        newInstance.transform.localPosition = localPosition;
        newInstance.transform.localRotation = localRotation;
        newInstance.transform.localScale = localScale;

        Destroy(oldInstance);

        chapterPrefabs[slot] = newInstance;
        return newInstance;
    }

    private void RefreshChapterTemplateCache()
    {
        if (chapterPrefabs == null || chapterPrefabs.Count == 0)
        {
            chapterTemplateCache = Array.Empty<GameObject>();
            return;
        }

        chapterTemplateCache = new GameObject[chapterPrefabs.Count];
        for (int i = 0; i < chapterPrefabs.Count; i++)
            chapterTemplateCache[i] = ResolveChapterTemplateUncached(i);
    }

    private GameObject ResolveChapterTemplate(int slotIndex)
    {
        if (chapterTemplateCache != null && slotIndex >= 0 && slotIndex < chapterTemplateCache.Length &&
            chapterTemplateCache[slotIndex] != null)
            return chapterTemplateCache[slotIndex];

        GameObject resolved = ResolveChapterTemplateUncached(slotIndex);
        if (resolved == null)
            return null;

        if (chapterTemplateCache == null || chapterTemplateCache.Length != chapterPrefabs.Count)
            RefreshChapterTemplateCache();
        else if (slotIndex >= 0 && slotIndex < chapterTemplateCache.Length)
            chapterTemplateCache[slotIndex] = resolved;

        return resolved;
    }

    private GameObject ResolveChapterTemplateUncached(int slotIndex)
    {
        EnsureChapterSourceListSize();

        if (slotIndex >= 0 && slotIndex < chapterPrefabSources.Count)
        {
            GameObject fromSource = ResolvePrefabAssetReference(chapterPrefabSources[slotIndex]);
            if (fromSource != null)
                return fromSource;
        }

        if (slotIndex >= 0 && slotIndex < chapterPrefabs.Count && chapterPrefabs[slotIndex] != null)
        {
            GameObject fromInstance = ResolvePrefabAssetReference(chapterPrefabs[slotIndex]);
            if (fromInstance != null)
                return fromInstance;

            Debug.LogWarning(
                $"[ChapterManager] 챕터 {slotIndex + 1} — Project 프리팹을 찾지 못했습니다. " +
                "Chapter Prefab Sources에 Assets/2.SLA/Prefabs/FactoryMaps 프리팹을 연결하세요.");
        }

        return null;
    }

    private static GameObject ResolvePrefabAssetReference(GameObject reference)
    {
        if (reference == null)
            return null;

        if (IsProjectPrefabAsset(reference))
            return reference;

#if UNITY_EDITOR
        GameObject prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(reference);
        if (prefabAsset != null && IsProjectPrefabAsset(prefabAsset))
            return prefabAsset;
#endif

        return null;
    }

    private static bool IsProjectPrefabAsset(GameObject gameObject)
    {
        return gameObject != null && !gameObject.scene.IsValid();
    }

    private static string GetChapterInstanceName(int slotIndex, GameObject template)
    {
        if (template != null && !string.IsNullOrEmpty(template.name))
            return template.name;

        return $"FactoryMap_Chapter{slotIndex + 1}";
    }

    private bool HasChapterSlot(int oneBasedIndex)
    {
        return chapterPrefabs != null && oneBasedIndex >= 1 && oneBasedIndex <= chapterPrefabs.Count;
    }

    private void EnsureChapterSourceListSize()
    {
        if (chapterPrefabs == null)
            return;

        if (chapterPrefabSources == null)
            chapterPrefabSources = new List<GameObject>();

        while (chapterPrefabSources.Count < chapterPrefabs.Count)
            chapterPrefabSources.Add(null);

        if (chapterPrefabSources.Count > chapterPrefabs.Count)
            chapterPrefabSources.RemoveRange(chapterPrefabs.Count, chapterPrefabSources.Count - chapterPrefabs.Count);
    }

    private Transform ResolveChapterInstancesParent(int slotIndex)
    {
        if (chapterInstancesParent != null)
            return chapterInstancesParent;

        if (cachedChapterInstancesParent != null)
            return cachedChapterInstancesParent;

        if (chapterPrefabs != null && slotIndex >= 0 && slotIndex < chapterPrefabs.Count && chapterPrefabs[slotIndex] != null)
            return chapterPrefabs[slotIndex].transform.parent;

        return null;
    }

    private static void DestroyOrphanChaptersByTag()
    {
        try
        {
            GameObject[] tagged = GameObject.FindGameObjectsWithTag(ChapterObjectTag);
            for (int i = 0; i < tagged.Length; i++)
            {
                if (tagged[i] != null)
                    Destroy(tagged[i]);
            }
        }
        catch (UnityException)
        {
            // Tag가 Project에 없으면 ChapterManager 목록만 사용합니다.
        }
    }

    private static void FocusCameraOnChapterEntrance(Vector3 spawnWorldPosition)
    {
        CameraFollow cameraFollow =
            FindAnyObjectByType<CameraFollow>(FindObjectsInactive.Include);

        if (cameraFollow != null)
        {
            cameraFollow.RebindToPlayer(snapImmediately: false);
            cameraFollow.SnapToWorldPoint(spawnWorldPosition);
            return;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera == null)
            return;

        Vector3 position = mainCamera.transform.position;
        mainCamera.transform.position = new Vector3(spawnWorldPosition.x, spawnWorldPosition.y, position.z);
    }

    private static void RestoreFieldPhysicsAfterChapterReset()
    {
        UIBattleManager.ResetAllRuntimeBattleState();

        Rigidbody2D[] rigidbodies =
            FindObjectsByType<Rigidbody2D>(FindObjectsInactive.Include);

        for (int i = 0; i < rigidbodies.Length; i++)
        {
            Rigidbody2D body = rigidbodies[i];
            if (body == null)
                continue;

            body.simulated = true;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
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
            ActivateChapterHierarchy(chapter, shouldActivate);
        }

        ActivateCurrentFactoryStageRoot();

        if (savePrefs && persistChapterIndex)
        {
            PlayerPrefs.SetInt(CurrentChapterPrefsKey, CurrentChapterIndex);
            PlayerPrefs.Save();
        }

        if (refreshGameplay)
            RefreshChapterGameplay(includeSpawnerReset: true);

        TeleportPlayerToActiveChapterSpawn();
        PublishChapterLoaded(isRestart);

        if (Application.isPlaying)
        {
            PlaySessionStats.EnsureInstance()
                ?.OnChapterEntered(CurrentChapterIndex, ChapterCount, isRestart);
            MapInitializer.RefreshActiveMapColliders();
        }

        Debug.Log($"[ChapterManager] 활성 챕터: {CurrentChapterIndex} (restart={isRestart})");
    }

    private void RefreshChapterGameplay(bool includeSpawnerReset)
    {
        ResetChapterRestartGameplayState();

        if (includeSpawnerReset)
        {
            RespawnFactoryStageFieldEntities(resetToFirstStage: false);
            RespawnActiveChapterFieldEntities(resetToFirstStage: false);
        }
    }

    /// <summary>챕터 재시작 직후 배틀 UI·결과창 등 필드 상태를 정리합니다.</summary>
    private void ResetChapterRestartGameplayState()
    {
        GameManager.Instance?.ResetToField();
        GameManager.Instance?.ResetPlayerOxygenOnChapterTransition();

        if (UIManager.Instance != null)
            UIManager.Instance.CloseBattleUI();

        UIBattleManager.ResetSavedContaminationProgress();

        UIResult[] resultPanels = FindObjectsByType<UIResult>(FindObjectsInactive.Include);
        for (int i = 0; i < resultPanels.Length; i++)
            resultPanels[i]?.ResetStageResultState();
    }

    /// <summary>활성 챕터의 MonsterSpawner·ItemSpawner로 몬스터·아이템을 다시 뿌립니다.</summary>
    public void RespawnActiveChapterFieldEntities(bool resetToFirstStage)
    {
        GameObject activeChapter = GetActiveChapterRoot();
        if (activeChapter == null)
            return;

        RespawnSpawnersUnderRoot(activeChapter.transform, resetToFirstStage);
    }

    /// <summary>FactoryStage_* 루트(씬 루트) 아래 스포너로 몬스터·아이템을 다시 뿌립니다.</summary>
    public void RespawnFactoryStageFieldEntities(bool resetToFirstStage)
    {
        GameObject activeRoot = ActivateCurrentFactoryStageRoot();
        int spawnerCount = activeRoot != null
            ? RespawnSpawnersUnderRoot(activeRoot.transform, resetToFirstStage, CurrentChapterIndex)
            : 0;

        if (spawnerCount == 0)
        {
            Debug.LogWarning(
                "[ChapterManager] FactoryStage에서 MonsterSpawner를 찾지 못했습니다. " +
                "Factory Stage Prefab Sources 연결·Destroy 후 Instantiate 여부를 확인하세요.");
        }
    }

    private GameObject ActivateCurrentFactoryStageRoot()
    {
        GameObject activeRoot = GameManager.ActivateFactoryStageSceneRootForChapter(CurrentChapterIndex);
        EnsureMonsterBattleTrackerOnFactoryRoot(activeRoot);
        return activeRoot;
    }

    private static int RespawnSpawnersUnderRoot(Transform root, bool resetToFirstStage, int factoryStageLevel = 1)
    {
        if (root == null)
            return 0;

        int count = 0;

        GameManager.EnsureActiveInHierarchy(root.gameObject);

        MonsterSpawner[] monsterSpawners = root.GetComponentsInChildren<MonsterSpawner>(true);
        for (int i = 0; i < monsterSpawners.Length; i++)
        {
            MonsterSpawner spawner = monsterSpawners[i];
            if (spawner == null)
                continue;

            GameManager.EnsureActiveInHierarchy(spawner.gameObject);
            spawner.SetStageLevel(factoryStageLevel);

            spawner.RespawnCurrentStage();

            count++;
        }

        ItemSpawner[] itemSpawners = root.GetComponentsInChildren<ItemSpawner>(true);
        for (int i = 0; i < itemSpawners.Length; i++)
        {
            ItemSpawner spawner = itemSpawners[i];
            if (spawner == null)
                continue;

            GameManager.EnsureActiveInHierarchy(spawner.gameObject);
            spawner.SetStageLevel(factoryStageLevel);

            spawner.RespawnCurrentStage();
        }

        return count;
    }

    private static void RefreshFieldEntitiesVisibility()
    {
        try
        {
            GameObject[] monsters = GameObject.FindGameObjectsWithTag("Monster");
            for (int i = 0; i < monsters.Length; i++)
                GameManager.EnsureFieldEntityVisible(monsters[i]);
        }
        catch (UnityException)
        {
        }

        ItemPickup[] pickups = FindObjectsByType<ItemPickup>(FindObjectsInactive.Include);
        for (int i = 0; i < pickups.Length; i++)
        {
            if (pickups[i] != null)
                GameManager.EnsureFieldEntityVisible(pickups[i].gameObject);
        }
    }

    private static void DestroySceneObjectsByTag(string tag)
    {
        try
        {
            GameObject[] objects = GameObject.FindGameObjectsWithTag(tag);
            for (int i = 0; i < objects.Length; i++)
            {
                if (objects[i] != null)
                    Destroy(objects[i]);
            }
        }
        catch (UnityException)
        {
        }
    }

    private static void DestroySceneObjectsWithComponent<T>() where T : Component
    {
        T[] components = FindObjectsByType<T>(FindObjectsInactive.Include);
        for (int i = 0; i < components.Length; i++)
        {
            if (components[i] != null)
                Destroy(components[i].gameObject);
        }
    }

    private void TeleportPlayerToActiveChapterSpawn()
    {
        TeleportPlayerToCurrentChapterSpawn();
    }

    private static GameObject FindPlayerObject()
    {
        try
        {
            GameObject activePlayer = GameObject.FindGameObjectWithTag("Player");
            if (activePlayer != null)
                return activePlayer;
        }
        catch (UnityException)
        {
        }

        PlayerController[] controllers =
            FindObjectsByType<PlayerController>(FindObjectsInactive.Include);
        if (controllers.Length > 0 && controllers[0] != null)
            return controllers[0].gameObject;

        return null;
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
        if (!HasChapterSlot(oneBasedIndex))
            return false;

        int slot = oneBasedIndex - 1;
        if (chapterPrefabs[slot] != null)
            return true;

        return ResolveChapterTemplate(slot) != null;
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
