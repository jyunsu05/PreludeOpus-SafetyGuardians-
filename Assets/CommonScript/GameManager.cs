using System;
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

    [Header("--- 재시작 씬 설정 ---")]
    [Tooltip("처음부터 시작(fullReset) 시 로드할 오프닝 씬")]
    [SerializeField] private string openingSceneName = "OpeningScene";

    [Tooltip("오프닝 씬이 빌드에 없을 때 사용할 대체 씬")]
    [SerializeField] private string openingSceneFallbackName = "MainGameScenes";

    [Tooltip("현재 챕터 재시작 시 활성 씬을 알 수 없을 때 사용할 기본 챕터 씬")]
    [SerializeField] private string defaultChapterSceneName = "MainGameScenes";

    [Header("--- 공장 체크포인트 (챕터 재시작) ---")]
    [Tooltip("체크포인트가 없을 때 챕터 재시작에 적용할 기본 오염도")]
    [SerializeField] private float defaultChapterPollutionOnRestart = 30f;

    private const string FactoryCheckpointPollutionKey = "SG_FactoryCheckpoint_Pollution";
    private const string FactoryCheckpointExistsKey = "SG_FactoryCheckpoint_Exists";

    private bool stageClearPending;
    private bool isPublishingBattleEnded;
    private bool isRestartInProgress;
    private bool isSubscribedToSceneLoaded;

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

        if (!isFullReset && TryRestartCurrentChapterInPlace())
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

    /// <summary>씬 재로드 없이 현재 챕터만 재시작합니다.</summary>
    public void RestartCurrentChapter()
    {
        if (isRestartInProgress)
        {
            Debug.LogWarning("[GameManager] 재시작이 이미 진행 중입니다.");
            return;
        }

        if (!TryRestartCurrentChapterInPlace())
            RequestRestart(isFullReset: false);
    }

    private bool TryRestartCurrentChapterInPlace()
    {
        ChapterManager chapterManager = ChapterManager.EnsureInstance();
        if (chapterManager == null || chapterManager.ChapterCount == 0)
            return false;

        isRestartInProgress = true;

        try
        {
            PerformChapterReset();
            chapterManager.RestartCurrentChapter();
            PerformPostChapterSetup();
            Debug.Log("[GameManager] 씬 로드 없이 현재 챕터 재시작 완료");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[GameManager] 챕터 재시작 실패: {e.Message}");
            return false;
        }
        finally
        {
            isRestartInProgress = false;
        }
    }

    private void PerformReset(bool isFullReset)
    {
        if (isFullReset)
            PerformFullReset();
        else
            PerformChapterReset();
    }

    private void PerformFullReset()
    {
        Debug.Log("[GameManager] 전체 데이터 초기화 (처음부터 시작)");

        ResetToField();
        BattleEncounterContext.ClearFleeExit();

        ClearFactoryCheckpoint();
        ChapterManager.ClearSavedChapter();
        FactoryChapterController.ClearSavedChapter();
        PollutionManager.Instance?.ResetPollution();
        ChapterManager.EnsureInstance()?.ResetToFirstChapter();
        FactoryChapterController.Instance?.ResetToFirstChapter();
        InventoryManager.Instance?.ResetAll();
        UIBattleManager.ResetSavedContaminationProgress();

        ResetRuntimePlayerState();
        CloseGameplayOverlays();
    }

    private void PerformChapterReset()
    {
        Debug.Log("[GameManager] 챕터 데이터 부분 초기화 (현재 챕터 재시작)");

        ResetToField();
        BattleEncounterContext.ClearFleeExit();
        stageClearPending = false;

        ApplyFactoryCheckpointToPollutionManager();
        UIBattleManager.ResetSavedContaminationProgress();

        ResetRuntimePlayerState();
        CloseGameplayOverlays();
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

        if (PollutionManager.Instance == null)
        {
            Debug.LogWarning(
                $"[GameManager] PollutionManager가 없어 체크포인트 오염도({pollution})를 적용하지 못했습니다.");
            return;
        }

        PollutionManager.Instance.SetPollution(pollution);
        Debug.Log($"[GameManager] 챕터 재시작 — 체크포인트 오염도 적용: {pollution}");
    }

    private void ClearFactoryCheckpoint()
    {
        PlayerPrefs.DeleteKey(FactoryCheckpointPollutionKey);
        PlayerPrefs.DeleteKey(FactoryCheckpointExistsKey);
        PlayerPrefs.Save();
    }

    private void ResetRuntimePlayerState()
    {
        PlayerOxygen[] oxygenComponents = FindObjectsByType<PlayerOxygen>(FindObjectsInactive.Include);
        for (int i = 0; i < oxygenComponents.Length; i++)
            oxygenComponents[i]?.ResetOxygen();
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
        if (!isRestartInProgress)
            return;

        isRestartInProgress = false;
        ResetToField();
        PerformPostLoadSceneSetup();
        Debug.Log($"[GameManager] 재시작 씬 로드 완료: {scene.name}");
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
