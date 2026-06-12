using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public enum GameState { Field, Battle }
    public GameState CurrentState { get; private set; } = GameState.Field;

    public event Action OnBattleStarted;
    public event Action OnBattleEnded;
    public event Action OnStageCleared;
    public event Action OnStageMonstersSpawned;

    private static readonly string[] CoreGameplaySceneRootNames =
    {
        "Managers",
        "Player",
        "Canvas",
        "ChapterMaps",
    };

    [Header("--- 오프닝 / 재시작 씬 설정 ---")]
    [Tooltip("처음부터 시작(fullReset) 시 로드할 오프닝 씬")]
    [SerializeField] private string openingSceneName = "OpeningScene";

    [Tooltip("클리어 후 돌아갈 타이틀(게임 시작) 씬")]
    [SerializeField] private string gameStartScreenSceneName = "Game start screen";

    [Tooltip("오프닝 씬이 빌드에 없을 때 사용할 대체 씬")]
    [SerializeField] private string openingSceneFallbackName = "MainGameScenes";

    [Tooltip("현재 챕터 재시작 시 활성 씬을 알 수 없을 때 사용할 기본 챕터 씬")]
    [SerializeField] private string defaultChapterSceneName = "MainGameScenes";

    [Header("--- 처음부터 다시 시작 (플레이어 스폰) ---")]
    [Tooltip("비어 있으면 Play 시작 시점의 Player 위치를 오프닝 스폰으로 사용합니다.")]
    [SerializeField] private Transform openingPlayerSpawn;

    [Header("--- 처음부터 다시 시작 (초기 오염도) ---")]
    [Tooltip("전체 리셋·오프닝 후 새 게임에 적용할 공장 오염도 (Max 100 기준)")]
    [SerializeField] private float initialSessionPollution = 100f;

    [Header("--- 공장 체크포인트 (챕터 재시작) ---")]
    [Tooltip("체크포인트가 없을 때 챕터 재시작에 적용할 기본 오염도")]
    [SerializeField] private float defaultChapterPollutionOnRestart = 30f;

    private const string FactoryCheckpointPollutionKey = "SG_FactoryCheckpoint_Pollution";
    private const string FactoryCheckpointExistsKey = "SG_FactoryCheckpoint_Exists";

    private bool stageClearPending;
    private bool isPublishingBattleEnded;
    private bool isRestartInProgress;
    private bool isSubscribedToSceneLoaded;
    private Coroutine inPlaceChapterRestartCoroutine;
    private Vector3 cachedOpeningPlayerPosition;
    private bool hasCachedOpeningPlayerPosition;
    private bool isFullResetOpeningInProgress;
    private bool isFieldMovementFrozen;

    /// <summary>오프닝 직후 StartNewGameAfterOpening에서 BeginNewPlaySession을 호출할 예정이면 true.</summary>
    public bool IsAwaitingPostOpeningPlaySession => isFullResetOpeningInProgress;

    /// <summary>산소 게임오버 등으로 필드에서 플레이어·몬스터 이동이 멈춘 상태입니다.</summary>
    public bool IsFieldMovementFrozen => isFieldMovementFrozen;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SubscribeSceneLoaded();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        CacheOpeningPlayerSpawnPosition();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            UnsubscribeSceneLoaded();
            Instance = null;
        }
    }

    public void EnterBattle()
    {
        if (CurrentState == GameState.Battle)
            return;

        CurrentState = GameState.Battle;
        Debug.Log("[GameManager] 배틀 시작!");
        OnBattleStarted?.Invoke();
    }

    public void ReturnToField()
    {
        if (isPublishingBattleEnded)
        {
            Debug.LogWarning("[GameManager] 배틀 종료 처리가 이미 진행 중입니다. 중복 호출을 차단합니다.");
            return;
        }

        isPublishingBattleEnded = true;
        CurrentState = GameState.Field;

        Debug.Log("[GameManager] 필드로 복귀합니다.");

        try
        {
            OnBattleEnded?.Invoke();
        }
        catch (Exception e)
        {
            Debug.LogError($"[GameManager] OnBattleEnded 처리 중 오류: {e.Message}");
        }
        finally
        {
            isPublishingBattleEnded = false;
        }
    }

    public void ResetToField()
    {
        CurrentState = GameState.Field;
        isPublishingBattleEnded = false;
        stageClearPending = false;
    }

    /// <summary>
    /// 산소 게임오버 UI 표시 시 호출. 전투 상태를 정리하고 플레이어·몬스터 이동을 멈춥니다.
    /// </summary>
    public void EnterGameOverFreeze()
    {
        if (isFieldMovementFrozen)
            return;

        ResetToField();
        BattleEncounterContext.ClearFleeExit();

        UIBattleManager.ResetAllRuntimeBattleState();
        UIButtonContainer.ResetAllRuntimeButtonState();
        MonsterBattleTracker.ResetInstanceBattleTrackingState();
        PlayerController.ResetAllFieldBattleEntryStates();

        if (UIManager.Instance != null)
            UIManager.Instance.CloseBattleUI();

        isFieldMovementFrozen = true;
        StopAllFieldMovementImmediate();
        GameplayAudioGuard.BlockAndStopAll();
        Debug.Log("[GameManager] 게임오버 — 필드 이동 정지");
    }

    public void ClearFieldMovementFreeze()
    {
        isFieldMovementFrozen = false;
    }

    /// <summary>
    /// 게임오버 표시·재시작 직전에 호출. 배틀 UI·버튼·Rigidbody2D.simulated 등 런타임 상태를 정리합니다.
    /// </summary>
    public void ResetAllSystems()
    {
        GameplayAudioGuard.Unblock();
        ClearFieldMovementFreeze();
        ResetToField();
        BattleEncounterContext.ClearFleeExit();

        UIBattleManager.ResetAllRuntimeBattleState();
        UIButtonContainer.ResetAllRuntimeButtonState();
        MonsterBattleTracker.ResetInstanceBattleTrackingState();
        PlayerController.ResetAllFieldBattleEntryStates();
        MonsterEncounterReset.EnableAllEncounterCollidersInScene();

        if (UIManager.Instance != null)
            UIManager.Instance.CloseBattleUI();

        RestoreAllSimulatedRigidbodies2D();
    }

    private static void StopAllFieldMovementImmediate()
    {
        PlayerController[] players =
            FindObjectsByType<PlayerController>(FindObjectsInactive.Include);
        for (int i = 0; i < players.Length; i++)
            players[i]?.StopFieldMovementImmediate();

        MonsterController[] monsters =
            FindObjectsByType<MonsterController>(FindObjectsInactive.Include);
        for (int i = 0; i < monsters.Length; i++)
            monsters[i]?.StopFieldMovementImmediate();
    }

    public bool IsInBattle => CurrentState == GameState.Battle;

    /// <summary>
    /// 현재 오염도를 PlayerPrefs에 저장합니다. 공장 스테이지 진입·클리어 등 적절한 시점에 호출하세요.
    /// </summary>
    public void SaveFactoryCheckpoint()
    {
        if (PollutionManager.Instance == null)
        {
            Debug.LogWarning("[GameManager] PollutionManager가 없어 공장 체크포인트를 저장하지 못했습니다.");
            return;
        }

        float pollution = PollutionManager.Instance.CurrentPollution;
        PlayerPrefs.SetFloat(FactoryCheckpointPollutionKey, pollution);
        PlayerPrefs.SetInt(FactoryCheckpointExistsKey, 1);
        PlayerPrefs.Save();

        Debug.Log($"[GameManager] 공장 체크포인트 저장: 오염도 {pollution}");
    }

    /// <summary>
    /// 게임오버 등에서 호출. isFullReset=true면 전체 초기화 후 오프닝 씬,
    /// false면 챕터 부분 초기화 후 ChapterManager가 있으면 씬 로드 없이 현재 챕터 재시작.
    /// </summary>
    public void RequestRestart(bool isFullReset)
    {
        if (isRestartInProgress)
        {
            Debug.LogWarning("[GameManager] 재시작이 이미 진행 중입니다.");
            return;
        }

        if (isFullReset)
        {
            LoadOpeningScene();
            return;
        }

        ResetAllSystems();

        if (TryRestartCurrentChapterInPlace())
            return;

        string targetScene = ResolveRestartSceneName(isFullReset);
        if (!CanLoadScene(targetScene))
            return;

        isRestartInProgress = true;

        try
        {
            PerformReset(isFullReset);
            Debug.Log($"[GameManager] 재시작 씬 로드: {targetScene} (fullReset={isFullReset})");
            SceneManager.LoadScene(targetScene);
        }
        catch (Exception e)
        {
            isRestartInProgress = false;
            Debug.LogError($"[GameManager] 재시작 실패: {e.Message}");
        }
    }

    /// <summary>
    /// 게임오버 등에서 호출. 세션 데이터만 초기화한 뒤 OpeningScene으로 이동합니다.
    /// 씬 언로드로 월드 오브젝트는 정리되므로 인씬 전체 리셋(PerformFullReset)은 생략합니다.
    /// </summary>
    public void LoadOpeningScene()
    {
        if (isRestartInProgress)
        {
            Debug.LogWarning("[GameManager] 재시작이 이미 진행 중입니다.");
            return;
        }

        ResetAllSystems();

        if (!CanLoadScene(openingSceneName))
            return;

        isRestartInProgress = true;
        isFullResetOpeningInProgress = true;

        try
        {
            PrepareSessionDataForOpeningSceneTransition();
            Debug.Log($"[GameManager] OpeningScene 로드: {openingSceneName}");
            SceneManager.LoadScene(openingSceneName);
        }
        catch (Exception e)
        {
            isRestartInProgress = false;
            isFullResetOpeningInProgress = false;
            Debug.LogError($"[GameManager] OpeningScene 로드 실패: {e.Message}");
        }
    }

    /// <summary>
    /// 게임 클리어 UI 닫기 등에서 호출. 게임플레이 UI를 정리한 뒤 Game start screen으로 이동합니다.
    /// </summary>
    public void LoadGameStartScreen()
    {
        if (isRestartInProgress)
        {
            Debug.LogWarning("[GameManager] 재시작이 이미 진행 중입니다.");
            return;
        }

        if (!CanLoadScene(gameStartScreenSceneName))
            return;

        isRestartInProgress = true;

        try
        {
            GameplayAudioGuard.Unblock();
            CloseGameplayOverlays();
            HidePersistentGameplayUiForOpeningScene();
            Debug.Log($"[GameManager] Game start screen 로드: {gameStartScreenSceneName}");
            SceneManager.LoadScene(gameStartScreenSceneName);
        }
        catch (Exception e)
        {
            isRestartInProgress = false;
            Debug.LogError($"[GameManager] Game start screen 로드 실패: {e.Message}");
        }
    }

    private void PrepareSessionDataForOpeningSceneTransition()
    {
        ResetToField();
        ApplyInitialSessionData();
        CloseGameplayOverlays();
        ResetFullResetUiState();
        HidePersistentGameplayUiForOpeningScene();
    }

    private static void HidePersistentGameplayUiForOpeningScene()
    {
        if (UIManager.Instance == null)
            return;

        UIManager.Instance.CloseAllPanels();
        UIManager.Instance.gameObject.SetActive(false);
    }

    /// <summary>씬 재로드 없이 현재 챕터만 재시작합니다.</summary>
    public void RestartCurrentChapter()
    {
        if (isRestartInProgress)
        {
            Debug.LogWarning("[GameManager] 재시작이 이미 진행 중입니다.");
            return;
        }

        ResetAllSystems();

        if (!TryRestartCurrentChapterInPlace())
            RequestRestart(isFullReset: false);
    }

    private bool TryRestartCurrentChapterInPlace()
    {
        ChapterManager chapterManager = ChapterManager.EnsureInstance();
        if (chapterManager == null || chapterManager.ChapterCount == 0)
            return false;

        if (inPlaceChapterRestartCoroutine != null)
            StopCoroutine(inPlaceChapterRestartCoroutine);

        inPlaceChapterRestartCoroutine = StartCoroutine(InPlaceChapterRestartRoutine(chapterManager));
        return true;
    }

    private IEnumerator InPlaceChapterRestartRoutine(ChapterManager chapterManager)
    {
        isRestartInProgress = true;
        int chapterIndex = chapterManager.CurrentChapterIndex;

        try
        {
            ActivateGameplayWorldSceneRoots();
            PerformChapterReset(chapterIndex);
        }
        catch (Exception e)
        {
            Debug.LogError($"[GameManager] 챕터 재시작 데이터 초기화 실패: {e.Message}");
            isRestartInProgress = false;
            inPlaceChapterRestartCoroutine = null;
            yield break;
        }

        yield return chapterManager.RebuildWorldAndBeginCurrentChapterSession();

        try
        {
            FinalizeInPlaceChapterRestart();
            Debug.Log($"[GameManager] 현재 챕터 {chapterIndex} — 전체 Destroy/재생성·스폰 재시작 완료");
        }
        catch (Exception e)
        {
            Debug.LogError($"[GameManager] 챕터 재시작 후처리 실패: {e.Message}");
        }

        isRestartInProgress = false;
        inPlaceChapterRestartCoroutine = null;
    }

    private void FinalizeInPlaceChapterRestart()
    {
        ActivateGameplayWorldSceneRoots();
        UIBattleManager.ResetAllRuntimeBattleState();
        ResetRuntimePlayerState();
        RestoreAllSimulatedRigidbodies2D();
        PlayerController.ResetAllFieldBattleEntryStates();
        MonsterEncounterReset.EnableAllEncounterCollidersInScene();
        ApplyInitialSessionPollution();
        SyncGameplayHudAfterDataReset();
        ResetFullResetUiState();
        CloseGameplayOverlays();
        GameManager.ActivateChapterMapsHierarchy();

        UIResult[] resultPanels = FindObjectsByType<UIResult>(FindObjectsInactive.Include);
        for (int i = 0; i < resultPanels.Length; i++)
            resultPanels[i]?.ResetStageResultState();
    }

    private void PerformReset(bool isFullReset)
    {
        if (isFullReset)
            PerformFullReset(destroySceneChapters: false, activateFirstChapterAfterReset: true);
        else
        {
            ChapterManager chapterManager = ChapterManager.EnsureInstance();
            int chapterIndex = chapterManager != null ? chapterManager.CurrentChapterIndex : 1;
            PerformChapterReset(chapterIndex);
        }
    }

    /// <summary>
    /// 처음부터 다시 시작(1단계): 모든 게임 데이터를 초기값으로 되돌리고 챕터를 제거한 뒤 오프닝을 재생합니다.
    /// 실제 플레이 시작(챕터 1·HUD)은 오프닝 종료 후 <see cref="StartNewGameAfterOpening"/>에서 처리합니다.
    /// </summary>
    public void PerformFullReset()
    {
        PerformFullReset(destroySceneChapters: false, activateFirstChapterAfterReset: false);
    }

    /// <summary>
    /// 오프닝 스크롤이 끝난 뒤 호출. 초기 데이터를 다시 한 번 적용하고 챕터 1 플레이를 시작합니다.
    /// </summary>
    public void StartNewGameAfterOpening()
    {
        isFullResetOpeningInProgress = false;

        Debug.Log("[GameManager] 오프닝 종료 — 초기 데이터·월드·스폰 재시작");

        ActivateCoreGameplayObjects();
        ResetToField();
        ApplyInitialSessionData();
        UIBattleManager.ResetAllRuntimeBattleState();

        ChapterManager chapterManager = ChapterManager.EnsureInstance();
        if (chapterManager != null)
        {
            if (!chapterManager.isActiveAndEnabled)
            {
                if (!chapterManager.gameObject.activeInHierarchy)
                    chapterManager.gameObject.SetActive(true);

                chapterManager.enabled = true;
            }

            chapterManager.BeginNewPlaySession();
            chapterManager.TeleportPlayerToCurrentChapterSpawn();
            chapterManager.ScheduleTeleportPlayerAfterFrame();
        }
        else
            FactoryChapterController.Instance?.ResetToFirstChapter();

        ResetRuntimePlayerState();
        RestoreAllSimulatedRigidbodies2D();
        PlayerController.ResetAllFieldBattleEntryStates();
        MonsterEncounterReset.EnableAllEncounterCollidersInScene();
        ApplyInitialSessionPollution();
        SyncGameplayHudAfterDataReset();
        ResetFullResetUiState();
        CloseGameplayOverlays();
        StartCoroutine(FinalizeNewGameAfterOpeningRoutine());
        GameManager.ActivateChapterMapsHierarchy();

        Debug.Log("[GameManager] 새 게임 세션 준비 완료 (Managers·Player·챕터1·몬스터·아이템)");
    }

    private IEnumerator FinalizeNewGameAfterOpeningRoutine()
    {
        yield return null;
        PlayerController.ResetAllFieldBattleEntryStates();
        MonsterEncounterReset.EnableAllEncounterCollidersInScene();
        ApplyInitialSessionPollution();
        SyncGameplayHudAfterDataReset();
    }


    private float ResolveInitialSessionPollution()
    {
        return initialSessionPollution > 0f
            ? initialSessionPollution
            : PollutionManager.DefaultInitialPollution;
    }

    private void ApplyInitialSessionPollution()
    {
        ApplySessionPollution(ResolveInitialSessionPollution(), "새 게임");
    }

    private void ApplySessionPollution(float pollution, string contextLabel)
    {
        PollutionManager manager = PollutionManager.EnsureInstance();
        if (manager == null)
        {
            Debug.LogWarning(
                $"[GameManager] PollutionManager를 찾지 못해 {contextLabel} 오염도({pollution})를 적용하지 못했습니다.");
            return;
        }

        manager.ApplyInitialPollution(pollution);
        Debug.Log($"[GameManager] {contextLabel} — 공장 오염도 {pollution} 적용");
    }

    /// <summary>오프닝 종료 후 Managers·Player·Canvas 등 핵심 오브젝트를 켭니다.</summary>
    private void ActivateCoreGameplayObjects()
    {
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        ActivateGameplayWorldSceneRoots();
    }

    /// <summary>비활성 씬 루트도 포함해 이름으로 루트 오브젝트를 찾습니다(GameObject.Find는 비활성을 못 찾음).</summary>
    public static GameObject FindSceneRoot(string rootName)
    {
        if (string.IsNullOrEmpty(rootName))
            return null;

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
            return null;

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            GameObject root = roots[i];
            if (root != null && root.name == rootName)
                return root;
        }

        return null;
    }

    public static bool IsCoreGameplaySceneRootName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
            return false;

        for (int i = 0; i < CoreGameplaySceneRootNames.Length; i++)
        {
            if (CoreGameplaySceneRootNames[i] == objectName)
                return true;
        }

        return false;
    }

    public static bool IsFactoryStageSceneRootName(string objectName)
    {
        return !string.IsNullOrEmpty(objectName) && objectName.StartsWith("FactoryStage");
    }

    /// <summary>오프닝 종료 후 반드시 켤 씬 루트(Managers·FactoryStage 등)인지 판별합니다.</summary>
    public static bool ShouldForceActiveAfterOpening(string objectName)
    {
        return IsCoreGameplaySceneRootName(objectName);
    }

    /// <summary>Managers·Player·Canvas·ChapterMaps 씬 루트만 켭니다. 자식은 기존 activeSelf를 유지합니다.</summary>
    public static void ActivateCoreGameplaySceneRoots()
    {
        for (int i = 0; i < CoreGameplaySceneRootNames.Length; i++)
            ActivateSceneRootOnly(CoreGameplaySceneRootNames[i]);
    }

    /// <summary>FactoryStage_* 씬 루트와 스폰에 필요한 자식까지 모두 켭니다.</summary>
    public static void ActivateFactoryStageSceneRoots()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
            return;

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            GameObject root = roots[i];
            if (root == null || !IsFactoryStageSceneRootName(root.name))
                continue;

            EnsureActiveInHierarchy(root);
            ActivateSubtreeDeep(root.transform);
        }
    }

    /// <summary>현재 챕터 번호와 맞는 FactoryStage_* 씬 루트 하나만 켭니다.</summary>
    public static GameObject ActivateFactoryStageSceneRootForChapter(int oneBasedChapterIndex)
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
            return null;

        int targetStage = Mathf.Max(1, oneBasedChapterIndex);
        GameObject activeRoot = null;
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            GameObject root = roots[i];
            if (root == null || !IsFactoryStageSceneRootName(root.name))
                continue;

            bool shouldActivate = GetFactoryStageNumber(root.name) == targetStage;
            if (shouldActivate)
            {
                EnsureActiveInHierarchy(root);
                ActivateSubtreeDeep(root.transform);
                activeRoot = root;
            }
            else if (root.activeSelf)
            {
                root.SetActive(false);
            }
        }

        return activeRoot;
    }

    private static int GetFactoryStageNumber(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
            return -1;

        const string prefix = "FactoryStage_";
        int start = objectName.IndexOf(prefix, StringComparison.Ordinal);
        if (start < 0)
            return -1;

        start += prefix.Length;
        int end = start;
        while (end < objectName.Length && char.IsDigit(objectName[end]))
            end++;

        if (end == start)
            return -1;

        return int.TryParse(objectName.Substring(start, end - start), out int number)
            ? number
            : -1;
    }

    /// <summary>부모 체인·자신·모든 자식을 활성화합니다(챕터 맵 Grid/Tilemap 복구용).</summary>
    public static void ActivateHierarchyDeep(GameObject root)
    {
        if (root == null)
            return;

        EnsureActiveInHierarchy(root);
        ActivateSubtreeDeep(root.transform);
    }

    /// <summary>ChapterMaps 루트와 현재 활성 챕터 맵 계층을 복구합니다.</summary>
    public static void ActivateChapterMapsHierarchy()
    {
        ActivateSceneRootOnly("ChapterMaps");

        ChapterManager chapterManager = ChapterManager.Instance;
        if (chapterManager == null)
            chapterManager = FindAnyObjectByType<ChapterManager>(FindObjectsInactive.Include);

        GameObject activeChapter = chapterManager?.GetActiveChapterRoot();
        if (activeChapter != null)
            ActivateHierarchyDeep(activeChapter);

        if (Application.isPlaying)
            MapInitializer.RefreshActiveMapColliders();
    }

    /// <summary>부모 체인·자신을 활성화해 activeInHierarchy를 보장합니다.</summary>
    public static void EnsureActiveInHierarchy(GameObject target)
    {
        if (target == null)
            return;

        Transform parent = target.transform.parent;
        while (parent != null)
        {
            if (!parent.gameObject.activeSelf)
                parent.gameObject.SetActive(true);

            parent = parent.parent;
        }

        if (!target.activeSelf)
            target.SetActive(true);
    }

    /// <summary>필드 몬스터·아이템이 Game 뷰에 보이도록 활성·렌더러를 복구합니다.</summary>
    public static void EnsureFieldEntityVisible(GameObject entity)
    {
        if (entity == null)
            return;

        EnsureActiveInHierarchy(entity);

        SpriteRenderer[] renderers = entity.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                renderers[i].enabled = true;
        }
    }

    private static void ActivateSubtreeDeep(Transform root)
    {
        if (root == null)
            return;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child == null)
                continue;

            if (!child.gameObject.activeSelf)
                child.gameObject.SetActive(true);

            ActivateSubtreeDeep(child);
        }
    }

    /// <summary>필드 플레이에 필요한 씬 루트(Managers·FactoryStage 등)를 켭니다.</summary>
    public static void ActivateGameplayWorldSceneRoots()
    {
        ActivateCoreGameplaySceneRoots();
    }

    private static void ActivateSceneRootOnly(string rootName)
    {
        GameObject root = FindSceneRoot(rootName);
        if (root == null)
        {
            Debug.LogWarning(
                $"[GameManager] 씬 루트 '{rootName}'를 찾지 못해 활성화하지 못했습니다. " +
                "Hierarchy 최상위 이름이 일치하는지 확인하세요.");
            return;
        }

        if (!root.activeSelf)
            root.SetActive(true);
    }

    private void PerformFullReset(bool destroySceneChapters, bool activateFirstChapterAfterReset)
    {
        Debug.Log("[GameManager] PerformFullReset — 데이터 전량 초기화 후 오프닝 대기");

        ResetToField();
        ApplyInitialSessionData();

        ChapterManager chapterManager = ChapterManager.EnsureInstance();
        chapterManager?.ClearAllSpawnedMonstersAndItemsInScene();

        if (destroySceneChapters)
            DestroyAllChapterObjectsInScene();
        else
            chapterManager?.DeactivateAllChaptersForOpening();

        CloseGameplayOverlays();
        ResetFullResetUiState();
        ResetPlayerToOpeningSpawn();

        if (activateFirstChapterAfterReset)
        {
            if (chapterManager != null)
                chapterManager.ResetToFirstChapter();
            else
                FactoryChapterController.Instance?.ResetToFirstChapter();

            SyncGameplayHudAfterDataReset();
        }
    }

    /// <summary>오염도·인벤토리·체크포인트·챕터 저장 등 새 게임 기본값.</summary>
    private void ApplyInitialSessionData()
    {
        BattleEncounterContext.ResetAll();
        ClearFactoryCheckpoint();
        ChapterManager.ClearSavedChapter();
        FactoryChapterController.ClearSavedChapter();
        ApplyInitialSessionPollution();
        InventoryManager.Instance?.ResetAll();
        UIBattleManager.ResetSavedContaminationProgress();
        PlaySessionStats.EnsureInstance()?.ResetAll();
    }

    private static GameObject FindPlayerIncludingInactive()
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

    private void SyncGameplayHudAfterDataReset()
    {
        PollutionManager pollutionManager = PollutionManager.EnsureInstance();
        if (pollutionManager == null || UIManager.Instance == null)
            return;

        UIManager.Instance.UpdatePollutionBar(
            pollutionManager.CurrentPollution,
            pollutionManager.MaxPollution);
    }

    private void DestroyAllChapterObjectsInScene()
    {
        ChapterManager chapterManager = ChapterManager.EnsureInstance();
        if (chapterManager != null)
            chapterManager.DestroyAllChapterInstances();
    }

    private void CacheOpeningPlayerSpawnPosition()
    {
        if (openingPlayerSpawn != null)
        {
            cachedOpeningPlayerPosition = openingPlayerSpawn.position;
            hasCachedOpeningPlayerPosition = true;
            return;
        }

        GameObject player = FindPlayerIncludingInactive();
        if (player == null)
            return;

        cachedOpeningPlayerPosition = player.transform.position;
        hasCachedOpeningPlayerPosition = true;
    }

    private void ResetPlayerToOpeningSpawn()
    {
        CacheOpeningPlayerSpawnPosition();

        Vector3 spawnPosition = openingPlayerSpawn != null
            ? openingPlayerSpawn.position
            : hasCachedOpeningPlayerPosition
                ? cachedOpeningPlayerPosition
                : Vector3.zero;

        GameObject player = FindPlayerIncludingInactive();
        if (player == null)
        {
            Debug.LogWarning("[GameManager] Player 태그 오브젝트를 찾지 못해 오프닝 스폰 이동을 건너뜁니다.");
            return;
        }

        Rigidbody2D rigidbody = player.GetComponent<Rigidbody2D>();
        if (rigidbody != null)
        {
            rigidbody.simulated = false;
            rigidbody.linearVelocity = Vector2.zero;
            rigidbody.angularVelocity = 0f;
        }

        player.transform.position = spawnPosition;

        if (rigidbody != null)
            rigidbody.simulated = true;

        ResetRuntimePlayerState();

        CameraFollow cameraFollow =
            FindAnyObjectByType<CameraFollow>(FindObjectsInactive.Include);
        if (cameraFollow != null)
        {
            cameraFollow.RebindToPlayer(snapImmediately: false);
            cameraFollow.SnapToWorldPoint(spawnPosition);
        }
    }

    private void ResetFullResetUiState()
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.CloseAllPanels();
            UIManager.Instance.ResetStageResult();
        }

        UIResult[] resultPanels = FindObjectsByType<UIResult>(FindObjectsInactive.Include);
        for (int i = 0; i < resultPanels.Length; i++)
            resultPanels[i]?.ResetStageResultState();
    }

    private void PerformChapterReset(int keepChapterIndex)
    {
        Debug.Log($"[GameManager] 현재 챕터 {keepChapterIndex} 재시작 — 세션 데이터 전량 초기화(챕터 번호 유지)");

        ResetToField();
        BattleEncounterContext.ClearFleeExit();
        stageClearPending = false;

        ApplyChapterRestartSessionData(keepChapterIndex);
        CloseGameplayOverlays();
    }

    /// <summary>처음부터 다시 시작과 동일한 데이터 초기화. 챕터 인덱스만 유지합니다.</summary>
    private void ApplyChapterRestartSessionData(int keepChapterIndex)
    {
        BattleEncounterContext.ResetAll();
        ClearFactoryCheckpoint();
        ApplyInitialSessionPollution();
        InventoryManager.Instance?.ResetAll();
        UIBattleManager.ResetSavedContaminationProgress();

        if (keepChapterIndex >= 1)
        {
            PlayerPrefs.SetInt(ChapterManager.CurrentChapterPrefsKey, keepChapterIndex);
            PlayerPrefs.Save();
        }
    }

    private float ResolveFactoryCheckpointPollution()
    {
        if (PlayerPrefs.GetInt(FactoryCheckpointExistsKey, 0) != 1)
        {
            Debug.Log(
                $"[GameManager] 저장된 공장 체크포인트가 없습니다. 기본 오염도 {defaultChapterPollutionOnRestart}를 사용합니다.");
            return defaultChapterPollutionOnRestart;
        }

        float saved = PlayerPrefs.GetFloat(FactoryCheckpointPollutionKey, defaultChapterPollutionOnRestart);
        if (saved < 0f)
            return defaultChapterPollutionOnRestart;

        return saved;
    }

    private void ApplyFactoryCheckpointToPollutionManager()
    {
        float pollution = ResolveFactoryCheckpointPollution();
        PollutionManager manager = PollutionManager.EnsureInstance();
        if (manager == null)
        {
            Debug.LogWarning(
                $"[GameManager] PollutionManager를 찾지 못해 체크포인트 오염도({pollution})를 적용하지 못했습니다.");
            return;
        }

        manager.SetPollution(pollution);
        Debug.Log($"[GameManager] 챕터 재시작 — 체크포인트 오염도 적용: {pollution}");
    }

    private void ClearFactoryCheckpoint()
    {
        PlayerPrefs.DeleteKey(FactoryCheckpointPollutionKey);
        PlayerPrefs.DeleteKey(FactoryCheckpointExistsKey);
        PlayerPrefs.Save();
    }

    /// <summary>공장 챕터 전환 시 플레이어 산소 게이지를 최대치로 되돌립니다.</summary>
    public void ResetPlayerOxygenOnChapterTransition()
    {
        ResetRuntimePlayerState();
    }

    private void ResetRuntimePlayerState()
    {
        PlayerOxygen[] oxygenComponents =
            FindObjectsByType<PlayerOxygen>(FindObjectsInactive.Include);
        for (int i = 0; i < oxygenComponents.Length; i++)
            oxygenComponents[i]?.ResetOxygen();

        // 산소 UI 초기화가 공장 오염도 슬라이더를 건드리지 않도록 오염도 HUD를 다시 맞춥니다.
        SyncGameplayHudAfterDataReset();
    }

    private static void RestoreAllSimulatedRigidbodies2D()
    {
        Rigidbody2D[] rigidbodies =
            FindObjectsByType<Rigidbody2D>(FindObjectsInactive.Include);

        for (int i = 0; i < rigidbodies.Length; i++)
        {
            Rigidbody2D body = rigidbodies[i];
            if (body != null && !body.simulated)
                body.simulated = true;
        }
    }

    private void CloseGameplayOverlays()
    {
        UIGameOver gameOver = FindAnyObjectByType<UIGameOver>(FindObjectsInactive.Include);
        gameOver?.Close();

        if (UIManager.Instance != null)
            UIManager.Instance.CloseBattleUI();
    }

    private void OnSceneLoadedAfterRestart(Scene scene, LoadSceneMode mode)
    {
        if (!isRestartInProgress && !isFullResetOpeningInProgress)
            return;

        bool wasRestartLoad = isRestartInProgress;
        if (wasRestartLoad)
            isRestartInProgress = false;

        ResetToField();
        ClearFieldMovementFreeze();

        if (scene.name == openingSceneName)
        {
            Debug.Log("[GameManager] OpeningScene 진입");
            return;
        }

        if (scene.name == gameStartScreenSceneName)
        {
            Debug.Log("[GameManager] Game start screen 진입");
            return;
        }

        if (isFullResetOpeningInProgress)
        {
            if (UIManager.Instance != null)
                UIManager.Instance.gameObject.SetActive(true);

            StartNewGameAfterOpening();
            Debug.Log($"[GameManager] 오프닝 후 '{scene.name}' — 새 게임 시작");
            return;
        }

        if (wasRestartLoad)
        {
            PerformPostLoadSceneSetup();
            Debug.Log($"[GameManager] 재시작 씬 로드 완료: {scene.name}");
        }
    }

    private void PerformPostLoadSceneSetup()
    {
        PerformPostChapterSetup();
    }

    private void PerformPostChapterSetup()
    {
        ResetRuntimePlayerState();

        ChapterManager chapterManager = ChapterManager.EnsureInstance();
        if (chapterManager != null && chapterManager.ChapterCount > 0)
            chapterManager.ApplySavedChapter();
        else
            FactoryChapterController.EnsureInstance()?.ApplySavedChapter();

        UIResult[] resultPanels = FindObjectsByType<UIResult>(FindObjectsInactive.Include);
        for (int i = 0; i < resultPanels.Length; i++)
            resultPanels[i]?.ResetStageResultState();
    }

    private string ResolveRestartSceneName(bool isFullReset)
    {
        if (isFullReset)
        {
            if (CanLoadScene(openingSceneName))
                return openingSceneName;

            Debug.LogWarning(
                $"[GameManager] '{openingSceneName}' 씬을 찾을 수 없어 '{openingSceneFallbackName}'(으)로 대체합니다. " +
                "Build Settings에 오프닝 씬을 추가하세요.");

            return openingSceneFallbackName;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.IsValid() && !string.IsNullOrEmpty(activeScene.name))
            return activeScene.name;

        return defaultChapterSceneName;
    }

    private static bool CanLoadScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("[GameManager] 씬 이름이 비어 있습니다.");
            return false;
        }

        if (Application.CanStreamedLevelBeLoaded(sceneName))
            return true;

        Debug.LogError(
            $"[GameManager] '{sceneName}' 씬을 로드할 수 없습니다. " +
            "File → Build Settings 에 씬이 포함되어 있는지 확인하세요.");
        return false;
    }

    private void SubscribeSceneLoaded()
    {
        if (isSubscribedToSceneLoaded)
            return;

        SceneManager.sceneLoaded += OnSceneLoadedAfterRestart;
        isSubscribedToSceneLoaded = true;
    }

    private void UnsubscribeSceneLoaded()
    {
        if (!isSubscribedToSceneLoaded)
            return;

        SceneManager.sceneLoaded -= OnSceneLoadedAfterRestart;
        isSubscribedToSceneLoaded = false;
    }

    public void NotifyStageCleared()
    {
        stageClearPending = true;
        Debug.Log("[GameManager] 스테이지 클리어!");
    }

    public bool ConsumeStageClearPending()
    {
        if (!stageClearPending)
            return false;

        stageClearPending = false;
        OnStageCleared?.Invoke();
        return true;
    }

    public void NotifyStageMonstersSpawned()
    {
        stageClearPending = false;
        OnStageMonstersSpawned?.Invoke();
    }
}
