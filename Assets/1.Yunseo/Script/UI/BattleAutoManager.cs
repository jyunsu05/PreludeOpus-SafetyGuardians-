using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 챕터에 따라 배틀 씬 자동/수동 모드를 결정하고, 오토 모드일 때 플레이어 턴에 정화 행동을 대행합니다.
/// Ch.1: 수동 (IsAutoBattle = false)
/// Ch.2+: 하이브리드 — 첫 정화는 수동, 이후 플레이어 턴마다 자동 탐색·정화
/// </summary>
[DisallowMultipleComponent]
public class BattleAutoManager : MonoBehaviour
{
    public static BattleAutoManager Instance { get; private set; }

    [Header("--- 챕터 판별 ---")]
    [Tooltip("이 챕터 번호(1-based) 이상이면 배틀 진입 시 자동 전투 모드로 시작합니다.")]
    [SerializeField, Min(1)] private int autoBattleFromChapter = 2;

    [Tooltip("에디터·디버그용. true면 챕터와 무관하게 IsAutoBattle을 강제합니다.")]
    [SerializeField] private bool forceAutoBattleForDebug;

    [Header("--- 자동 행동 타이밍 ---")]
    [SerializeField, Min(0f)] private float autoActionDelaySeconds = 1f;

    [Tooltip("true면 자동 모드에서 탐색 연출을 건너뛰고 즉시 스캔 완료 처리합니다. (추후 소탕/배속 확장용)")]
    [SerializeField] private bool skipSearchAnimationInAutoMode = true;

    [Header("--- 연동 ---")]
    [SerializeField] private UIBattleManager battleManager;
    [SerializeField] private BattleTurnController turnController;
    [SerializeField] private BattleAutoOverlayView overlayView;
    [Tooltip("Ch.2+ 자동 전투 중 표시할 배지(UIAutoBattle 등). 비우면 자식에서 'UIAutoBattle' 이름을 찾습니다.")]
    [SerializeField] private GameObject autoBattleBadge;

    [Header("--- 인디케이터 문구 ---")]
    [SerializeField] private string autoBattleIndicatorMessage = "자동 정화 중...";
    [SerializeField] private string autoSearchIndicatorMessage = "자동 탐색 중...";

    public event Action<bool> OnAutoBattleStateChanged;

    /// <summary>현재 전투가 자동 모드인지 여부.</summary>
    public bool IsAutoBattle { get; private set; }

    /// <summary>배틀 진입 시점에 판별한 1-based 챕터 번호.</summary>
    public int CurrentChapterId { get; private set; } = 1;

    /// <summary>챕터 2 이상이면 자동 전투 대상 챕터입니다.</summary>
    public bool IsAutoBattleChapter => CurrentChapterId >= autoBattleFromChapter;

    public bool IsProcessingAutoTurn { get; private set; }

    /// <summary>이번 플레이어 턴에 탐색·도망 등으로 자동 행동을 막아야 하는지 여부.</summary>
    public bool HasManualPlayerControlThisTurn { get; private set; }

    /// <summary>플레이어가 한 번이라도 수동 정화에 성공했는지(이후 자동 전투 진행).</summary>
    public bool IsAutoBattleEngaged { get; private set; }

    private Coroutine autoTurnRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        ResolveReferences();
        ResolveAutoBattleBadge();
        SetAutoBattleBadgeVisible(false);
    }

    private void OnEnable()
    {
        ResolveReferences();
        IsAutoBattleEngaged = false;
        HasManualPlayerControlThisTurn = false;
        InitializeAutoModeFromChapter();
        SubscribeTurnEvents();
        TryScheduleAutoTurnForCurrentPhase();
    }

    private void OnDisable()
    {
        UnsubscribeTurnEvents();
        StopAutoTurnRoutine();
        SetOverlayVisible(false);
        SetAutoBattleBadgeVisible(false);
        IsProcessingAutoTurn = false;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>챕터 정보를 다시 읽고 자동 모드 여부를 갱신합니다.</summary>
    public void InitializeAutoModeFromChapter()
    {
        CurrentChapterId = ResolveCurrentChapterId();
        bool shouldAutoBattle = forceAutoBattleForDebug || IsAutoBattleChapter;
        SetAutoBattle(shouldAutoBattle);

        Debug.Log(
            $"[BattleAutoManager] 챕터 {CurrentChapterId} — " +
            $"IsAutoBattle={IsAutoBattle} (threshold={autoBattleFromChapter})");
    }

    /// <summary>런타임에서 자동 모드를 켜거나 끕니다. (추후 배속·소탕 UI 연동용)</summary>
    public void SetAutoBattle(bool enabled)
    {
        bool stateChanged = IsAutoBattle != enabled;
        IsAutoBattle = enabled;

        if (stateChanged)
        {
            OnAutoBattleStateChanged?.Invoke(IsAutoBattle);

            if (!IsAutoBattle)
            {
                StopAutoTurnRoutine();
                SetOverlayVisible(false);
                UIButtonContainer.SetAllBattleInputBlocked(false);
            }
        }

        SetAutoBattleBadgeVisible(IsAutoBattle && IsAutoBattleEngaged);
    }

    private void HandleTurnPhaseChanged(BattleTurnController.BattleTurnPhase phase)
    {
        if (phase != BattleTurnController.BattleTurnPhase.PlayerTurn)
        {
            StopAutoTurnRoutine();
            HasManualPlayerControlThisTurn = false;
            if (IsAutoBattle)
                SetOverlayVisible(false);
            return;
        }

        HasManualPlayerControlThisTurn = false;
        TryScheduleAutoTurnForCurrentPhase();
    }

    /// <summary>
    /// 탐색·도망·산소 아이템 등 수동 행동 시 이번 턴 자동 코루틴만 중단합니다.
    /// (수동 정화와 달리 자동 전투 해금에는 사용하지 않습니다.)
    /// </summary>
    public void BlockAutoTurnForManualAction()
    {
        HasManualPlayerControlThisTurn = true;
        StopAutoTurnRoutine();
        SetOverlayVisible(false);

        if (IsAutoBattle)
            UIButtonContainer.SetAllBattleInputBlocked(false);
    }

    /// <summary>플레이어가 수동 정화에 성공한 뒤, 이후 턴부터 자동 전투를 진행합니다.</summary>
    public void EngageAutoBattleAfterManualPurify()
    {
        if (!IsAutoBattle)
            return;

        IsAutoBattleEngaged = true;
        HasManualPlayerControlThisTurn = false;
        StopAutoTurnRoutine();
        SetOverlayVisible(false);
        UIButtonContainer.SetAllBattleInputBlocked(false);
        SetAutoBattleBadgeVisible(IsAutoBattle && IsAutoBattleEngaged);

        Debug.Log("[BattleAutoManager] 수동 정화 완료 — 이후 턴부터 자동 전투를 진행합니다.");
    }

    private void TryScheduleAutoTurnForCurrentPhase()
    {
        if (!IsAutoBattle || !IsAutoBattleEngaged || turnController == null)
            return;

        if (!turnController.IsPlayerTurn || turnController.IsResolvingTurn)
            return;

        if (battleManager != null &&
            (battleManager.HasBattleWon || battleManager.IsPurifying || battleManager.IsSearching))
        {
            return;
        }

        StopAutoTurnRoutine();
        autoTurnRoutine = StartCoroutine(AutoPlayerTurnRoutine());
    }

    private IEnumerator AutoPlayerTurnRoutine()
    {
        IsProcessingAutoTurn = true;

        SetOverlayVisible(true, autoBattleIndicatorMessage);
        UIButtonContainer.SetAllBattleInputBlocked(true);

        if (autoActionDelaySeconds > 0f)
        {
            float waited = 0f;
            while (waited < autoActionDelaySeconds)
            {
                if (HasManualPlayerControlThisTurn)
                {
                    CompleteAutoTurnCleanup();
                    yield break;
                }

                yield return null;
                waited += Time.unscaledDeltaTime;
            }
        }

        if (!IsAutoBattle || turnController == null || !turnController.IsPlayerTurn)
        {
            CompleteAutoTurnCleanup();
            yield break;
        }

        if (HasManualPlayerControlThisTurn ||
            (battleManager != null && battleManager.IsSearching))
        {
            CompleteAutoTurnCleanup();
            yield break;
        }

        if (battleManager != null && !battleManager.IsScanned)
        {
            SetOverlayVisible(true, autoSearchIndicatorMessage);
            yield return ExecuteAutoSearchRoutine();
        }

        if (HasManualPlayerControlThisTurn ||
            (battleManager != null && battleManager.IsSearching))
        {
            CompleteAutoTurnCleanup();
            yield break;
        }

        if (!CanExecutePurifyNow())
        {
            CompleteAutoTurnCleanup();
            yield break;
        }

        SetOverlayVisible(true, autoBattleIndicatorMessage);
        ExecutePurify();
        CompleteAutoTurnCleanup();
    }

    private IEnumerator ExecuteAutoSearchRoutine()
    {
        if (battleManager == null)
            yield break;

        if (skipSearchAnimationInAutoMode)
        {
            battleManager.NotifySearchCompleted();
            battleManager.RevealScannedInfo(
                battleManager.GetInfectionTypeDisplayText(),
                battleManager.GetDescriptionDisplayText(),
                battleManager.BuildInventoryStatusText());
            UIButtonContainer.RefreshAllPlayerTurnButtons();
            yield break;
        }

        if (!battleManager.CanBeginSearch())
            yield break;

        bool searchFinished = false;
        battleManager.PrepareSearchLensForPlayback();

        if (!battleManager.TryBeginSearch(() => searchFinished = true))
        {
            battleManager.CancelSearchLensPresentation();
            yield break;
        }

        while (!searchFinished)
            yield return null;

        battleManager.NotifySearchCompleted();
        battleManager.RevealScannedInfo(
            battleManager.GetInfectionTypeDisplayText(),
            battleManager.GetDescriptionDisplayText(),
            battleManager.BuildInventoryStatusText());
        UIButtonContainer.RefreshAllPlayerTurnButtons();
    }

    /// <summary>
    /// 자동 모드에서 플레이어 턴의 정화 행동을 수행합니다.
    /// UIBattleManager.OnClickPurify()와 동일한 턴제·연출 파이프라인을 재사용합니다.
    /// </summary>
    public bool ExecutePurify()
    {
        if (!CanExecutePurifyNow())
            return false;

        bool succeeded = battleManager.OnClickPurify(out int appliedEffect);
        if (!succeeded)
        {
            Debug.LogWarning("[BattleAutoManager] 자동 정화 실패 — 아이템 부족 또는 턴 조건 미충족.");
            return false;
        }

        Debug.Log($"[BattleAutoManager] 자동 정화 실행 — effect={appliedEffect}");
        return true;
    }

    private bool CanExecutePurifyNow()
    {
        if (battleManager == null || turnController == null)
            return false;

        if (!IsAutoBattle || !turnController.IsPlayerTurn || turnController.IsResolvingTurn)
            return false;

        if (battleManager.HasBattleWon || battleManager.IsPurifying || battleManager.IsSearching)
            return false;

        if (!battleManager.IsScanned)
            return false;

        return battleManager.CanPurifyWithInventory(battleManager.GetRequiredPurifyItemId());
    }

    private void CompleteAutoTurnCleanup()
    {
        IsProcessingAutoTurn = false;
        autoTurnRoutine = null;

        if (!IsAutoBattle)
        {
            SetOverlayVisible(false);
            UIButtonContainer.SetAllBattleInputBlocked(false);
            return;
        }

        // 정화 연출 중에는 UIBattleManager가 입력 잠금을 유지합니다.
        if (battleManager == null || !battleManager.IsPurifying)
        {
            SetOverlayVisible(false);
            UIButtonContainer.SetAllBattleInputBlocked(false);
            UIButtonContainer.RefreshAllPlayerTurnButtons();
        }
    }

    private void StopAutoTurnRoutine()
    {
        if (autoTurnRoutine == null)
            return;

        StopCoroutine(autoTurnRoutine);
        autoTurnRoutine = null;
        IsProcessingAutoTurn = false;
    }

    private void SetOverlayVisible(bool visible, string message = null)
    {
        if (overlayView == null)
            return;

        overlayView.SetVisible(visible, message ?? autoBattleIndicatorMessage);
    }

    private void SetAutoBattleBadgeVisible(bool visible)
    {
        ResolveAutoBattleBadge();
        if (autoBattleBadge != null)
            autoBattleBadge.SetActive(visible);
    }

    private void ResolveAutoBattleBadge()
    {
        if (autoBattleBadge != null)
            return;

        Transform searchRoot = transform.parent != null ? transform.parent : transform;
        Transform[] descendants = searchRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < descendants.Length; i++)
        {
            Transform candidate = descendants[i];
            if (candidate != null && candidate.name == "UIAutoBattle")
            {
                autoBattleBadge = candidate.gameObject;
                return;
            }
        }
    }

    private void SubscribeTurnEvents()
    {
        if (turnController == null)
            return;

        turnController.OnTurnPhaseChanged -= HandleTurnPhaseChanged;
        turnController.OnTurnPhaseChanged += HandleTurnPhaseChanged;
    }

    private void UnsubscribeTurnEvents()
    {
        if (turnController == null)
            return;

        turnController.OnTurnPhaseChanged -= HandleTurnPhaseChanged;
    }

    private void ResolveReferences()
    {
        if (battleManager == null || !battleManager.isActiveAndEnabled)
            battleManager = UIBattleManager.TryGetPrimaryActive();

        if (battleManager == null || !battleManager.isActiveAndEnabled)
            battleManager = FindAnyObjectByType<UIBattleManager>(FindObjectsInactive.Include);

        if (turnController == null && battleManager != null)
            turnController = battleManager.TurnController;

        if (turnController == null)
            turnController = FindAnyObjectByType<BattleTurnController>(FindObjectsInactive.Include);

        if (overlayView == null)
            overlayView = GetComponentInChildren<BattleAutoOverlayView>(true);
    }

    private static int ResolveCurrentChapterId()
    {
        if (ChapterManager.Instance != null)
            return Mathf.Max(1, ChapterManager.Instance.CurrentChapterIndex);

        if (FactoryChapterController.Instance != null)
            return Mathf.Max(1, FactoryChapterController.Instance.CurrentChapter);

        if (PlayerPrefs.HasKey(ChapterManager.CurrentChapterPrefsKey))
            return Mathf.Max(1, PlayerPrefs.GetInt(ChapterManager.CurrentChapterPrefsKey, 1));

        return 1;
    }
}
