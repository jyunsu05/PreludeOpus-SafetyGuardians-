using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System;

public class UIBattleManager : MonoBehaviour
{
    private const int DefaultContaminationLevel = 100;
    private static readonly Dictionary<string, int> contaminationProgressByMonsterId = new Dictionary<string, int>();
    private static string lastResolvedEncounterMonsterId;

    public static void ResetSavedContaminationProgress()
    {
        contaminationProgressByMonsterId.Clear();
        lastResolvedEncounterMonsterId = null;
    }

    public event System.Action OnContaminationEmpty;
    public event System.Action<float> OnPurifyPerformed;
    [Header("--- 몬스터 기본 정보 UI (항상 보임) ---")]
    [SerializeField] private Image monsterImage;
    [Tooltip("MonsterImages의 BattleMonsterSpriteLooper. 비우면 monsterImage에서 자동 탐색합니다.")]
    [SerializeField] private BattleMonsterSpriteLooper monsterSpriteLooper;
    [SerializeField] private TextMeshProUGUI monsterNameText;       // 몬스터: name
    [SerializeField] private TextMeshProUGUI difficultyText;        // 포획 난이도: New Text
    [SerializeField] private Slider contaminationSlider;            // 오염도 게이지 바
    [SerializeField] private TextMeshProUGUI contaminationValueText;
    [SerializeField] private string defaultMonsterId = string.Empty;

    [Header("--- 탐색 시 통째로 열리는 부모 Panel ---")]
    [SerializeField] private GameObject scanInfoPanel;              // 3개를 하나로 묶으신 부모 오브젝트

    [Header("--- 부모 Panel 내부의 텍스트들 ---")]
    [SerializeField] private TextMeshProUGUI infectionTypeText;     // SubstanceName : description
    [SerializeField] private TextMeshProUGUI descriptionText;       // PurificationMethodExplanationText : purification_method
    [SerializeField] private TextMeshProUGUI inventoryStatusText;   // 인벤토리 상황 : 아이템 보유

    [Header("--- 턴제 배틀 ---")]
    [SerializeField] private BattleTurnController turnController;
    [Tooltip("정화 아이템 1회 사용 시 오염도 감소량. 아이템은 즉시 삭제되지만 턴당 효과는 이 값을 사용합니다.")]
    [SerializeField] private int purifyEffectPerUse = 10;

    [Header("--- 배틀 산소 UI ---")]
    [Tooltip("배틀 씬에 만든 산소 게이지(UIBattleOxygenGauge). 비우면 자식에서 자동 탐색합니다.")]
    [SerializeField] private UIBattleOxygenGauge battleOxygenGauge;

    [Header("--- 정화 연출 ---")]
    [Tooltip("정화 행동 시 DoPurify 트리거. 비우면 자식에서 PurificationCircle을 자동 탐색합니다.")]
    [SerializeField] private Animator purificationCircleAnimator;
    [Tooltip("정화 행동 시 DoPurify 트리거. 비우면 자식에서 PurificationParticles를 자동 탐색합니다.")]
    [SerializeField] private Animator purificationParticlesAnimator;
    [Tooltip("Circle/Particles 클립 길이(0.6초)에 맞춘 대기 시간입니다.")]
    [SerializeField] private float purifyAnimationDuration = 0.6f;

    [Header("--- 플레이어 피격 연출 (몬스터 공격) ---")]
    [Tooltip("PlayerHitVfx의 BattlePlayerHitPresenter. 비우면 자식에서 자동 탐색합니다.")]
    [FormerlySerializedAs("monsterHitPresenter")]
    [SerializeField] private BattlePlayerHitPresenter playerHitPresenter;
    [Tooltip("Presenter가 없을 때 사용할 피격 연출 대기 시간(초)입니다.")]
    [FormerlySerializedAs("monsterHitAnimationDuration")]
    [SerializeField] private float playerHitAnimationDuration = 0.65f;
    [SerializeField] private AudioClip fireMonsterAttackClip;

    [Header("--- 탐색 연출 ---")]
    [Tooltip("SearchLens의 BattleSearchLensPresenter. 비우면 자식에서 자동 탐색합니다.")]
    [FormerlySerializedAs("searchLensAnimator")]
    [SerializeField] private BattleSearchLensPresenter searchLensPresenter;
    [Tooltip("Presenter가 없을 때 사용할 탐색 연출 대기 시간(초)입니다.")]
    [FormerlySerializedAs("searchAnimationDuration")]
    [SerializeField] private float searchAnimationDuration = 3f;

    [Header("--- 배틀 UI 버튼 사운드 ---")]
    [SerializeField] private AudioClip searchSoundClip;
    [SerializeField] private AudioClip purificationUiSoundClip;
    [SerializeField] private AudioClip escapeSoundClip;

    private const float SearchSfxBgmRatio = 0.7f;
    private const float PurifyLoopSfxBgmRatio = 0.47f;
    private const float EscapeSfxBgmRatio = 0.68f;
    private const float HitClothSfxBgmRatio = 0.62f;
    private const float HitImpactSfxBgmRatio = 0.72f;
    private const float FireAttackSfxBgmRatio = 0.72f;

    private const string DefaultPurifyItemId = "MI-101";
    private const string FireMonsterId = "M-003";
    private static readonly int DoPurifyTrigger = Animator.StringToHash("DoPurify");

    private Coroutine purifyAnimationRoutine;
    private Coroutine searchAnimationRoutine;
    private Coroutine revealMonsterImageRoutine;
    private AudioSource playerHitAudioSource;
    private bool isPlayerHitEffectPlaying;

    public bool IsScanned { get; private set; }
    public bool IsSearching { get; private set; }
    public bool IsPurifying { get; private set; }
    public bool IsEscapeLocked { get; private set; }
    public string LastConsumedBattleItemId { get; private set; }
    public bool HasPendingPurifyItemConsumption => hasPendingPurifyItemConsumption;
    public bool HasBattleWon => hasBattleWon;

    /// <summary>true면 도망 UI를 숨깁니다(UIButtonContainer가 SetActive 처리).</summary>
    public event Action<bool> OnEscapeLockChanged;

    private MonsterData currentMonsterData;
    private string currentMonsterId;
    private int contaminationAtBattleEntry;
    private bool isSubscribedToBattleEnded;
    private bool hasFinalizedContaminationForSession;
    private bool isProcessingBattleExit;
    private PlayerController lockedPlayerController;
    private Rigidbody2D lockedPlayerRigidbody;
    private bool wasPlayerRigidbodySimulated;
    private RigidbodyConstraints2D playerConstraints;
    private readonly List<MonsterPhysicsSnapshot> lockedMonsters = new List<MonsterPhysicsSnapshot>();
    private static bool isProcessingPlayerItemUse;
    private bool hasPendingPurifyItemConsumption;
    private bool hasBattleWon;

    private sealed class MonsterPhysicsSnapshot
    {
        public Rigidbody2D rigidbody;
        public bool wasSimulated;
        public RigidbodyConstraints2D constraints;
    }

    void Awake()
    {
        DisableNestedDuplicateManagers();
        SetMonsterImageVisible(false);
        TryResolveContaminationValueText();
        if (enabled)
            ConfigurePlayerHitAudioSource();
    }

    public static void PrepareFieldBattlePresentation(GameObject battleSceneRoot, string monsterId)
    {
        if (battleSceneRoot == null || string.IsNullOrEmpty(monsterId))
            return;

        UIBattleManager manager = battleSceneRoot.GetComponentInChildren<UIBattleManager>(true);
        if (manager == null)
            return;

        manager.SetMonsterImageVisible(false);
        manager.SetMonsterById(monsterId);
    }

    private void DisableNestedDuplicateManagers()
    {
        if (!IsPrimaryBattleManagerHost())
        {
            enabled = false;
            return;
        }

        UIBattleManager[] managers = GetComponentsInChildren<UIBattleManager>(true);
        for (int i = 0; i < managers.Length; i++)
        {
            UIBattleManager candidate = managers[i];
            if (candidate == null || candidate == this)
                continue;

            Debug.LogWarning(
                $"[UIBattleManager] 중복 매니저 제거: {candidate.gameObject.name}. " +
                "턴제 배틀은 UIBattlescene 루트 매니저만 사용합니다.");
            Destroy(candidate.gameObject);
        }
    }

    private bool IsPrimaryBattleManagerHost()
    {
        if (gameObject.name == "UIBattlescene")
            return true;

        Transform parent = transform.parent;
        return parent != null &&
               parent.name == "UIBattlescene" &&
               gameObject.name == "UIBattleManager";
    }

    public static UIBattleManager TryGetPrimaryActive()
    {
        UIBattleManager[] managers = FindObjectsByType<UIBattleManager>(FindObjectsInactive.Include);
        UIBattleManager fallback = null;

        for (int i = 0; i < managers.Length; i++)
        {
            UIBattleManager manager = managers[i];
            if (manager == null || !manager.enabled)
                continue;

            if (!manager.IsPrimaryBattleManagerHost())
                continue;

            if (!manager.isActiveAndEnabled)
                continue;

            return manager;
        }

        for (int i = 0; i < managers.Length; i++)
        {
            UIBattleManager manager = managers[i];
            if (manager == null || !manager.enabled || !manager.IsPrimaryBattleManagerHost())
                continue;

            if (fallback == null)
                fallback = manager;
        }

        return fallback;
    }

    public static UIBattleManager TryGetPrimaryInHierarchy(Transform battleRoot)
    {
        if (battleRoot == null)
            return TryGetPrimaryActive();

        UIBattleManager[] managers = battleRoot.GetComponentsInChildren<UIBattleManager>(true);
        for (int i = 0; i < managers.Length; i++)
        {
            UIBattleManager manager = managers[i];
            if (manager != null && manager.enabled && manager.IsPrimaryBattleManagerHost())
                return manager;
        }

        return TryGetPrimaryActive();
    }

    void OnEnable()
    {
        hasFinalizedContaminationForSession = false;
        isProcessingBattleExit = false;
        SubscribeBattleEnded();
        SetMonsterImageVisible(false);
        ExitBattle();
        NotifyEscapeUnlockedForNewBattle();
        ResetBattleUIState();
        LoadMonsterFromData();
        EnsureEnemyStatus();
        EnsureTurnController();
        ResolveAndBindPlayerOxygen();
        turnController?.BeginBattle();
        ApplyFieldPrepaidBattleEntryState();
        LockPlayerMovementAtBattleEntry();
        LockMonsterMovementAtBattleEntry();
        DisableContaminationSliderDirectInput();
        ScheduleRevealMonsterImage();
    }

    private void ApplyFieldPrepaidBattleEntryState()
    {
        if (!BattleEncounterContext.WasFieldEntryPrepaid)
            return;

        NotifySearchCompleted();
        RevealScannedInfo(
            GetInfectionTypeDisplayText(),
            GetDescriptionDisplayText(),
            BuildInventoryStatusText());
        UIButtonContainer.RefreshAllPlayerTurnButtons();
    }

    private void DisableContaminationSliderDirectInput()
    {
        if (contaminationSlider == null)
            return;

        contaminationSlider.interactable = false;
    }

    void OnDisable()
    {
        if (revealMonsterImageRoutine != null)
        {
            StopCoroutine(revealMonsterImageRoutine);
            revealMonsterImageRoutine = null;
        }

        SetMonsterImageVisible(false);
        UnsubscribeBattleEnded();
        ExitBattle();
        FinalizeContaminationOnce();
        ForceRestoreFieldPhysics();
    }

    void OnDestroy()
    {
        UnsubscribeBattleEnded();
        ForceRestoreFieldPhysics();
    }

    public bool CanAttemptEscape =>
        !IsPurifying &&
        !IsEscapeLocked &&
        !isProcessingBattleExit &&
        IsPlayerTurnActive();

    public BattleTurnController TurnController => turnController;

    public bool IsPlayerTurnActive() => CanAcceptPlayerBattleAction();

    /// <summary>플레이어 턴이며 턴 전환 연출 중이 아닐 때만 배틀 행동을 허용합니다.</summary>
    public bool CanAcceptPlayerBattleAction()
    {
        if (hasBattleWon || IsPurifying || IsSearching || isProcessingBattleExit)
            return false;

        ResolveTurnController();
        if (turnController == null)
            return false;

        return turnController.IsPlayerTurn && !turnController.IsResolvingTurn;
    }

    /// <summary>탐색 버튼을 누를 수 있는지 확인합니다. (정화 아이템 보유와 무관)</summary>
    public bool CanBeginSearch()
    {
        if (IsScanned || IsSearching)
            return false;

        if (hasBattleWon || IsPurifying || isProcessingBattleExit)
            return false;

        ResolveTurnController();
        if (turnController == null)
            return false;

        return turnController.IsPlayerTurn && !turnController.IsResolvingTurn;
    }

    /// <summary>탐색 버튼 클릭 직후 SearchLens를 즉시 켭니다. (아이템 보유와 무관)</summary>
    public void PrepareSearchLensForPlayback()
    {
        ResolveSearchLensPresenter();
        searchLensPresenter?.PrepareForPlayback();
    }

    /// <summary>탐색 연출 시작에 실패했을 때 SearchLens를 끕니다.</summary>
    public void CancelSearchLensPresentation()
    {
        ResolveSearchLensPresenter();
        searchLensPresenter?.StopSearchAnimation();
    }

    /// <summary>탐색 연출을 재생한 뒤 콜백을 호출합니다. (아이템 보유 여부와 무관)</summary>
    public bool TryBeginSearch(System.Action onCompleted)
    {
        if (!CanBeginSearch())
            return false;

        StopSearchAnimationRoutineOnly();
        searchAnimationRoutine = StartCoroutine(ExecuteSearchAnimationRoutine(onCompleted));
        return true;
    }

    private IEnumerator ExecuteSearchAnimationRoutine(System.Action onCompleted)
    {
        IsSearching = true;

        try
        {
            ResolveSearchLensPresenter();
            if (searchLensPresenter != null)
            {
                searchLensPresenter.OnMovementBeat += HandleSearchMovementBeat;
                try
                {
                    yield return searchLensPresenter.RunSearchSequence();
                }
                finally
                {
                    searchLensPresenter.OnMovementBeat -= HandleSearchMovementBeat;
                }
            }
            else
            {
                Debug.LogWarning("[UIBattleManager] SearchLens presenter를 찾지 못했습니다.");
                PlaySearchBeatSound();
                yield return new WaitForSecondsRealtime(ResolveSearchAnimationDuration());
            }
        }
        finally
        {
            IsSearching = false;
            searchAnimationRoutine = null;
        }

        onCompleted?.Invoke();
    }

    private void HandleSearchMovementBeat()
    {
        PlaySearchBeatSound();
    }

    /// <summary>탐색 성공 — 정화 버튼을 켤 수 있는 상태로 전환합니다.</summary>
    public void NotifySearchCompleted()
    {
        IsScanned = true;
    }

    /// <summary>배틀 인벤토리 슬롯/정화 버튼 공통 — 정화는 효과만 즉시 적용하고 아이템 차감은 승리 시 처리합니다.</summary>
    public bool UseItem(string itemId)
    {
        if (isProcessingPlayerItemUse)
            return false;

        if (string.IsNullOrEmpty(itemId) || !CanAcceptPlayerBattleAction())
            return false;

        ResolveTurnController();
        if (turnController == null || !turnController.TryLockPlayerAction())
            return false;

        isProcessingPlayerItemUse = true;
        bool succeeded;
        try
        {
            if (IsPurifyConsumableForCurrentMonster(itemId))
                succeeded = ExecutePurifyItemUse(itemId, out _);
            else if (IsOxygenRecoveryItem(itemId))
                succeeded = ExecuteOxygenItemUse(itemId, out _);
            else
            {
                turnController.ReleasePlayerActionLock();
                Debug.LogWarning($"[UIBattleManager] 배틀에서 사용할 수 없는 아이템: {itemId}");
                return false;
            }

            if (succeeded)
                RefreshBattleUiAfterPlayerAction();
            else
                turnController.ReleasePlayerActionLock();

            return succeeded;
        }
        finally
        {
            isProcessingPlayerItemUse = false;
        }
    }

    /// <summary>정화 버튼 — 인벤토리 UseItem()과 동일한 턴제 흐름을 사용합니다.</summary>
    public bool OnClickPurify(out int appliedEffect)
    {
        appliedEffect = 0;
        string itemId = GetRequiredPurifyItemId();

        if (!UseItem(itemId))
            return false;

        appliedEffect = Mathf.Max(1, purifyEffectPerUse);
        return true;
    }

    /// <summary>정화가 불가능한 이유를 반환합니다. itemId를 넘기면 인벤토리 슬롯 사용 시 아이템 적합성도 검사합니다.</summary>
    public bool TryGetPurifyBlockReason(out string message, string itemId = null)
    {
        message = null;

        if (hasBattleWon)
        {
            message = "이미 정화가 완료되었습니다.";
            return true;
        }

        if (isProcessingBattleExit)
        {
            message = "전투를 종료하는 중입니다.";
            return true;
        }

        if (IsPurifying)
        {
            message = "정화 연출이 진행 중입니다.";
            return true;
        }

        if (IsSearching)
        {
            message = "탐색 연출이 진행 중입니다.";
            return true;
        }

        if (!IsScanned)
        {
            message = "오염원을 먼저 [탐색]하세요.";
            return true;
        }

        ResolveTurnController();
        if (turnController == null)
        {
            message = "지금은 행동할 수 없습니다.";
            return true;
        }

        if (!turnController.IsPlayerTurn)
        {
            message = "몬스터 턴입니다. 잠시 기다려 주세요.";
            return true;
        }

        if (turnController.IsResolvingTurn)
        {
            message = "턴이 진행 중입니다.";
            return true;
        }

        if (isProcessingPlayerItemUse)
        {
            message = "다른 행동을 처리하는 중입니다.";
            return true;
        }

        string requiredItemId = GetRequiredPurifyItemId();
        if (!string.IsNullOrEmpty(itemId) &&
            InventoryManager.Instance != null &&
            !InventoryManager.Instance.IsConsumableForRequirement(itemId, requiredItemId))
        {
            string requiredName = ResolveItemDisplayName(requiredItemId);
            message = $"이 몬스터는 {requiredName}(으)로 정화해야 합니다.";
            return true;
        }

        if (!CanPurifyWithInventory(requiredItemId))
        {
            string itemName = ResolveItemDisplayName(requiredItemId);
            message = $"정화에 {itemName}이(가) 필요합니다.";
            return true;
        }

        return false;
    }

    public void ShowPlayerFeedback(string message)
    {
        if (string.IsNullOrEmpty(message))
            return;

        ResolveTurnController();
        if (turnController != null)
            turnController.ShowPlayerFeedback(message);
        else
            Debug.LogWarning($"[UIBattleManager] {message}");
    }

    private bool ExecutePurifyItemUse(string itemId, out int appliedEffect)
    {
        appliedEffect = 0;

        if (IsPurifying)
            return false;

        if (!IsScanned)
        {
            Debug.LogWarning("[UIBattleManager] 탐색 전에는 정화 아이템을 사용할 수 없습니다.");
            return false;
        }

        if (string.IsNullOrEmpty(itemId) || !CanPurifyWithInventory(itemId))
            return false;

        string requiredItemId = GetRequiredPurifyItemId();
        if (InventoryManager.Instance == null ||
            !InventoryManager.Instance.IsConsumableForRequirement(itemId, requiredItemId))
        {
            Debug.LogWarning($"[UIBattleManager] 이 몬스터에는 {requiredItemId} 아이템이 필요합니다.");
            return false;
        }

        appliedEffect = Mathf.Max(1, purifyEffectPerUse);
        int basePurifyDamage = appliedEffect;
        int finalPurifyDamage = turnController != null
            ? turnController.CalculateAmplifiedContaminationDamage(appliedEffect)
            : appliedEffect;

        if (purifyAnimationRoutine != null)
            StopCoroutine(purifyAnimationRoutine);

        purifyAnimationRoutine = StartCoroutine(
            ExecutePurifyWithAnimationRoutine(basePurifyDamage, finalPurifyDamage));

        return true;
    }

    private IEnumerator ExecutePurifyWithAnimationRoutine(int basePurifyDamage, int finalPurifyDamage)
    {
        BeginPurifySession();
        if (!BattleEncounterContext.WasFieldEntryPrepaid)
            hasPendingPurifyItemConsumption = true;
        UIButtonContainer.SetAllBattleInputBlocked(true);
        UIInventory.RefreshAllVisible();

        SetPurifyVfxActive(true);
        TriggerPurifyAnimation();
        PlayPurificationUiSound();

        ResolveMonsterSpriteLooper();
        Coroutine hitCoroutine = null;
        if (monsterSpriteLooper != null)
            hitCoroutine = monsterSpriteLooper.StartCoroutine(monsterSpriteLooper.PlayHitOnceRoutine());

        float hitAnimationDuration = GetPurifyHitAnimationDuration();
        PlayMonsterPurifySound(hitAnimationDuration);
        OnPurifyPerformed?.Invoke(hitAnimationDuration);

        float purifyWaitDuration = ResolvePurifyAnimationDuration();
        yield return new WaitForSecondsRealtime(purifyWaitDuration);
        SetPurifyVfxActive(false);

        if (hitCoroutine != null)
            yield return hitCoroutine;

        ApplyPurifyDamage(finalPurifyDamage);
        StopPurificationUiSound();
        FinalizePurifyTurn(basePurifyDamage, finalPurifyDamage);

        EndPurifyAttempt(unlockEscape: false);
        UIButtonContainer.SetAllBattleInputBlocked(false);
        UIButtonContainer.RefreshAllPlayerTurnButtons();
        UIInventory.RefreshAllVisible();
        purifyAnimationRoutine = null;
    }

    private void ApplyPurifyDamage(int finalPurifyDamage)
    {
        ReduceContamination(finalPurifyDamage);
    }

    private void FinalizePurifyTurn(int basePurifyDamage, int finalPurifyDamage)
    {
        if (hasBattleWon)
        {
            turnController?.ReleasePlayerActionLock();
            return;
        }

        if (GetCurrentContamination() > 0)
            turnController?.CommitPlayerPurifyTurn(basePurifyDamage, finalPurifyDamage);
        else
            turnController?.ReleasePlayerActionLock();
    }

    /// <summary>몬스터 공격 행동 시 반투명 Hit 이미지를 잠깐 표시합니다.</summary>
    public IEnumerator PlayPlayerHitEffectRoutine()
    {
        isPlayerHitEffectPlaying = true;

        try
        {
            ResolvePlayerHitPresenter();

            if (IsCurrentMonsterFire())
                yield return PlayFireMonsterAttackLeadInRoutine();
            else
                yield return PlayMonsterAttackLeadInRoutine();

            if (playerHitPresenter == null)
            {
                yield return new WaitForSecondsRealtime(Mathf.Max(0.01f, playerHitAnimationDuration));
                yield break;
            }

            playerHitPresenter.ShowHitOverlay();
            PlayPlayerHitClothSound();

            float impactDelay = playerHitPresenter.GetImpactHitDelay();
            if (impactDelay > 0f)
                yield return new WaitForSecondsRealtime(impactDelay);

            PlayPlayerHitImpactSound();

            float overlayDuration = Mathf.Max(0.01f, playerHitPresenter.HitOverlayDuration);
            float totalSoundDuration = playerHitPresenter.GetTotalHitSoundDuration();
            float waitAfterImpact = Mathf.Max(overlayDuration, totalSoundDuration) - impactDelay;
            if (waitAfterImpact > 0f)
                yield return new WaitForSecondsRealtime(waitAfterImpact);
        }
        finally
        {
            isPlayerHitEffectPlaying = false;
            playerHitPresenter?.HideHitOverlay();
        }
    }

    private void ConfigurePlayerHitAudioSource()
    {
        AudioSource[] sources = GetComponents<AudioSource>();
        playerHitAudioSource = sources.Length > 0 ? sources[0] : gameObject.AddComponent<AudioSource>();

        playerHitAudioSource.playOnAwake = false;
        playerHitAudioSource.loop = false;
        playerHitAudioSource.spatialBlend = 0f;
    }

    private UIButtonClickSoundPlayer ResolveUiSoundPlayer()
    {
        return UIButtonClickSoundPlayer.Instance;
    }

    private void PlaySearchBeatSound()
    {
        if (searchSoundClip == null)
            return;

        ResolveUiSoundPlayer()?.PlayOneShotClip(searchSoundClip, ResolveBattleSfxVolume(SearchSfxBgmRatio));
    }

    private void PlayPurificationUiSound()
    {
        if (purificationUiSoundClip == null)
            return;

        ResolveUiSoundPlayer()?.PlayTrackedClip(
            purificationUiSoundClip,
            ResolveBattleSfxVolume(PurifyLoopSfxBgmRatio),
            loop: true);
    }

    private void StopPurificationUiSound()
    {
        ResolveUiSoundPlayer()?.StopTrackedClip();
    }

    private void PlayEscapeSound()
    {
        if (escapeSoundClip == null)
            return;

        ResolveUiSoundPlayer()?.PlayOneShotClip(escapeSoundClip, ResolveBattleSfxVolume(EscapeSfxBgmRatio));
    }

    private void PlayPlayerHitClothSound()
    {
        if (!GameplayAudioGuard.CanPlay)
            return;

        AudioClip clip = playerHitPresenter != null ? playerHitPresenter.HitClothSoundClip : null;
        if (clip == null || playerHitAudioSource == null)
            return;

        playerHitAudioSource.PlayOneShot(clip, ResolveBattleSfxVolume(HitClothSfxBgmRatio));
    }

    private void PlayPlayerHitImpactSound()
    {
        if (!GameplayAudioGuard.CanPlay)
            return;

        AudioClip clip = playerHitPresenter != null ? playerHitPresenter.ImpactHitSoundClip : null;
        if (clip == null || playerHitAudioSource == null)
            return;

        playerHitAudioSource.PlayOneShot(clip, ResolveBattleSfxVolume(HitImpactSfxBgmRatio));
    }

    private bool IsCurrentMonsterFire()
    {
        if (string.Equals(currentMonsterId, FireMonsterId, StringComparison.OrdinalIgnoreCase))
            return true;

        string imageKey = currentMonsterData != null ? currentMonsterData.image_key : null;
        return !string.IsNullOrEmpty(imageKey) &&
               imageKey.IndexOf("fire", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private IEnumerator PlayFireMonsterAttackLeadInRoutine()
    {
        if (!GameplayAudioGuard.CanPlay || fireMonsterAttackClip == null || playerHitAudioSource == null)
            yield break;

        playerHitAudioSource.PlayOneShot(fireMonsterAttackClip, ResolveBattleSfxVolume(FireAttackSfxBgmRatio));
        yield return new WaitForSecondsRealtime(fireMonsterAttackClip.length);
    }

    private static float ResolveBattleSfxVolume(float bgmRatio)
    {
        if (GameManager.Instance != null)
            return GameManager.Instance.GetBattleSfxVolume(bgmRatio);

        return Mathf.Clamp01(0.5f * bgmRatio);
    }

    private IEnumerator PlayMonsterAttackLeadInRoutine()
    {
        MonsterFieldSoundController soundController = ResolveBattleMonsterSoundController();
        if (soundController == null)
            yield break;

        yield return soundController.PlayBattleAttackSoundRoutine();
    }

    private void ResolvePlayerHitPresenter()
    {
        if (playerHitPresenter != null)
            return;

        playerHitPresenter = GetComponentInChildren<BattlePlayerHitPresenter>(true);
    }

    private void ResolveSearchLensPresenter()
    {
        if (searchLensPresenter != null)
            return;

        searchLensPresenter = GetComponentInChildren<BattleSearchLensPresenter>(true);
        if (searchLensPresenter == null)
            Debug.LogWarning("[UIBattleManager] BattleSearchLensPresenter를 찾지 못했습니다.");
    }

    private float ResolveSearchAnimationDuration()
    {
        ResolveSearchLensPresenter();
        if (searchLensPresenter != null)
            return Mathf.Max(0.01f, searchLensPresenter.AnimationDuration);

        return Mathf.Max(0.01f, searchAnimationDuration);
    }

    private float ResolvePurifyAnimationDuration()
    {
        ResolvePurifyAnimators();

        float duration = purifyAnimationDuration;
        duration = Mathf.Max(duration, GetAnimatorClipLength(purificationCircleAnimator));
        duration = Mathf.Max(duration, GetAnimatorClipLength(purificationParticlesAnimator));
        return Mathf.Max(0.01f, duration);
    }

    private static float GetAnimatorClipLength(Animator animator)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return 0f;

        float maxLength = 0f;
        AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
        for (int i = 0; i < clips.Length; i++)
        {
            AnimationClip clip = clips[i];
            if (clip == null || clip.length <= 0f)
                continue;

            maxLength = Mathf.Max(maxLength, clip.length);
        }

        return maxLength;
    }

    private void TriggerPurifyAnimation()
    {
        ResolvePurifyAnimators();

        bool circleTriggered = TryTriggerPurifyAnimation(purificationCircleAnimator);
        bool particlesTriggered = TryTriggerPurifyAnimation(purificationParticlesAnimator);

        if (!circleTriggered && !particlesTriggered)
        {
            Debug.LogWarning(
                "[UIBattleManager] PurificationCircle/PurificationParticles Animator가 없어 정화 연출을 건너뜁니다.");
        }
    }

    private void SetPurifyVfxActive(bool active)
    {
        ResolvePurifyAnimators();
        SetPurifyVfxObjectActive(purificationCircleAnimator, active);
        SetPurifyVfxObjectActive(purificationParticlesAnimator, active);
    }

    private static void SetPurifyVfxObjectActive(Animator animator, bool active)
    {
        if (animator == null)
            return;

        GameObject vfxObject = animator.gameObject;
        if (!active)
        {
            vfxObject.SetActive(false);
            return;
        }

        vfxObject.SetActive(true);
        animator.Rebind();
        animator.Update(0f);
    }

    private static bool TryTriggerPurifyAnimation(Animator animator)
    {
        if (animator == null || !animator.isActiveAndEnabled)
            return false;

        animator.SetTrigger(DoPurifyTrigger);
        return true;
    }

    private void ResolvePurifyAnimators()
    {
        if (purificationCircleAnimator != null && purificationParticlesAnimator != null)
            return;

        Animator[] animators = GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < animators.Length; i++)
        {
            Animator candidate = animators[i];
            if (candidate == null)
                continue;

            string objectName = candidate.gameObject.name;
            if (purificationParticlesAnimator == null &&
                string.Equals(objectName, "PurificationParticles", StringComparison.OrdinalIgnoreCase))
            {
                purificationParticlesAnimator = candidate;
                continue;
            }

            if (purificationCircleAnimator == null &&
                string.Equals(objectName, "PurificationCircle", StringComparison.OrdinalIgnoreCase))
            {
                purificationCircleAnimator = candidate;
            }
        }
    }

    private bool ExecuteOxygenItemUse(string itemId, out int appliedEffect)
    {
        appliedEffect = 0;

        if (InventoryManager.Instance == null || !InventoryManager.Instance.HasItem(itemId))
            return false;

        if (!InventoryManager.Instance.TryConsumeBattleItem(itemId, out appliedEffect))
            return false;

        LastConsumedBattleItemId = itemId;
        PlayerOxygen playerOxygen = PlayerOxygen.ResolveRuntime();
        if (playerOxygen != null)
            playerOxygen.ApplyBattleOxygenRestore(appliedEffect);

        turnController?.CommitPlayerSupportItem(itemId, appliedEffect);
        Debug.Log($"[UIBattleManager] 산소 회복 아이템 사용: {itemId} (+{appliedEffect})");
        return true;
    }

    public bool CanPurifyWithInventory(string itemId = null)
    {
        if (BattleEncounterContext.WasFieldEntryPrepaid)
            return true;

        string requiredItemId = GetRequiredPurifyItemId();
        if (InventoryManager.Instance == null || string.IsNullOrEmpty(requiredItemId))
            return false;

        if (string.IsNullOrEmpty(itemId) ||
            string.Equals(itemId, requiredItemId, StringComparison.Ordinal))
        {
            return InventoryManager.Instance.HasBattleConsumableForRequirement(requiredItemId);
        }

        return InventoryManager.Instance.IsConsumableForRequirement(itemId, requiredItemId);
    }

    private bool IsPurifyConsumableForCurrentMonster(string itemId)
    {
        if (InventoryManager.Instance == null || string.IsNullOrEmpty(itemId))
            return false;

        string requiredItemId = GetRequiredPurifyItemId();
        if (InventoryManager.Instance.IsConsumableForRequirement(itemId, requiredItemId))
            return true;

        return string.Equals(itemId, requiredItemId, StringComparison.Ordinal) &&
               InventoryManager.Instance.HasBattleConsumableForRequirement(requiredItemId);
    }

    private static bool IsMonsterPurifyItem(string itemId)
    {
        return DataManager.Instance != null &&
               DataManager.Instance.IsMonsterPurificationItem(itemId);
    }

    private static bool IsOxygenRecoveryItem(string itemId)
    {
        if (string.IsNullOrEmpty(itemId) || DataManager.Instance == null)
            return false;

        ItemData data = DataManager.Instance.GetItemData(itemId);
        if (data == null || string.IsNullOrEmpty(data.item_type))
            return false;

        return data.item_type.IndexOf("산소", StringComparison.Ordinal) >= 0;
    }

    private static void RefreshBattleUiAfterPlayerAction()
    {
        UIInventory.RefreshAllVisible();
        UIButtonContainer.RefreshAllPlayerTurnButtons();
    }

    public string GetRequiredPurifyItemId()
    {
        if (currentMonsterData == null)
            return DefaultPurifyItemId;

        if (!string.IsNullOrEmpty(currentMonsterData.drop_item_id))
            return currentMonsterData.drop_item_id;

        if (currentMonsterData.drop_items != null && currentMonsterData.drop_items.Count > 0)
            return currentMonsterData.drop_items[0].item_id;

        return DefaultPurifyItemId;
    }

    public string BuildInventoryStatusText()
    {
        if (currentMonsterData == null)
            return "필요 아이템 정보 없음";

        string itemId = GetRequiredPurifyItemId();
        if (string.IsNullOrEmpty(itemId))
            return "필요 아이템 정보 없음";

        string itemName = ResolveItemDisplayName(itemId);
        if (InventoryManager.Instance == null)
            return $"{itemName} 보유 0";

        int count = InventoryManager.Instance.GetMonsterPurificationItemCount(itemId);
        if (count <= 0)
            return $"{itemName} 없음";

        return $"{itemName} 보유 {count}";
    }

    /// <summary>배틀 종료·UI 비활성 시 상태/도망 잠금을 방어적으로 해제합니다.</summary>
    public void ExitBattle()
    {
        StopPurifyAnimationRoutine();
        StopPurificationUiSound();
        AbortSearchAnimation();
        SetPurifyVfxActive(false);
        monsterSpriteLooper?.StopAll();

        if (!isPlayerHitEffectPlaying)
            playerHitPresenter?.HideHitOverlay();
        ClearPendingPurifyItemConsumption();
        hasBattleWon = false;
        ResetBattleSessionState();
        IsScanned = false;
        IsSearching = false;
        IsPurifying = false;
        isProcessingBattleExit = false;
        SetEscapeLocked(false);
        UIButtonContainer.SetAllBattleInputBlocked(false);
        SetMonsterImageVisible(false);
    }

    private void SetMonsterImageVisible(bool visible)
    {
        if (monsterImage == null)
            return;

        if (!visible)
        {
            monsterImage.enabled = false;
            Color hidden = monsterImage.color;
            hidden.a = 0f;
            monsterImage.color = hidden;
            return;
        }

        if (monsterImage.sprite == null)
        {
            monsterImage.enabled = false;
            return;
        }

        Color shown = monsterImage.color;
        shown.a = 1f;
        monsterImage.color = shown;
        monsterImage.enabled = true;
    }

    private void ScheduleRevealMonsterImage()
    {
        if (revealMonsterImageRoutine != null)
            StopCoroutine(revealMonsterImageRoutine);

        revealMonsterImageRoutine = StartCoroutine(RevealMonsterImageWhenReady());
    }

    private IEnumerator RevealMonsterImageWhenReady()
    {
        SetMonsterImageVisible(false);
        yield return null;
        yield return new WaitForEndOfFrame();

        if (monsterImage != null && monsterImage.sprite == null)
            LoadMonsterFromData();

        bool canShowMonsterImage = monsterImage != null && monsterImage.sprite != null;
        SetMonsterImageVisible(canShowMonsterImage);

        if (canShowMonsterImage)
        {
            ResolveMonsterSpriteLooper();
            monsterSpriteLooper?.PlayIdleLoop();
        }

        revealMonsterImageRoutine = null;
    }

    private void StopPurifyAnimationRoutine()
    {
        if (purifyAnimationRoutine == null)
            return;

        StopCoroutine(purifyAnimationRoutine);
        purifyAnimationRoutine = null;
        StopPurificationUiSound();
    }

    private void StopSearchAnimationRoutineOnly()
    {
        if (searchAnimationRoutine == null)
            return;

        StopCoroutine(searchAnimationRoutine);
        searchAnimationRoutine = null;
        IsSearching = false;
    }

    private void AbortSearchAnimation()
    {
        StopSearchAnimationRoutineOnly();
        searchLensPresenter?.StopSearchAnimation();
    }

    /// <summary>도망 버튼 광클 방지. 성공 시 MarkFleeExit까지 처리됨.</summary>
    public bool TryBeginFleeExit()
    {
        if (!CanAttemptEscape)
        {
            if (IsPurifying || IsEscapeLocked)
                Debug.Log("[UIBattleManager] 정화 진행 중 — 도망할 수 없습니다.");
            else
                Debug.Log("[UIBattleManager] 도망 처리 중 — 추가 입력 무시.");
            return false;
        }

        isProcessingBattleExit = true;
        PlayEscapeSound();
        BattleEncounterContext.MarkFleeExit();
        return true;
    }

    public void CompleteFleeExit()
    {
        ExitBattle();

        if (GameManager.Instance != null)
            GameManager.Instance.ReturnToField();
        else if (UIManager.Instance != null)
            UIManager.Instance.CloseBattleUI();
        else
            Debug.LogError("[UIBattleManager] GameManager를 찾을 수 없습니다.");
    }

    private void BeginPurifySession()
    {
        IsPurifying = true;
        SetEscapeLocked(true);
    }

    private void EndPurifyAttempt(bool unlockEscape)
    {
        IsPurifying = false;
        if (unlockEscape)
            SetEscapeLocked(false);
    }

    private void SetEscapeLocked(bool locked)
    {
        if (IsEscapeLocked == locked)
            return;

        IsEscapeLocked = locked;
        OnEscapeLockChanged?.Invoke(locked);
    }

    /// <summary>배틀 진입 시 도망 UI를 기본(해제) 상태로 동기화합니다. 이전 전투의 잠금이 남지 않도록 항상 알립니다.</summary>
    private void NotifyEscapeUnlockedForNewBattle()
    {
        IsEscapeLocked = false;
        OnEscapeLockChanged?.Invoke(false);
    }

    private static string ResolveItemDisplayName(string itemId)
    {
        if (DataManager.Instance == null || string.IsNullOrEmpty(itemId))
            return itemId;

        ItemData data = DataManager.Instance.GetItemData(itemId);
        if (data == null || string.IsNullOrEmpty(data.name))
            return itemId;

        return data.name;
    }

    public void ResetBattleUIState()
    {
        bool preserveWonGauge = hasBattleWon;

        ClearPendingPurifyItemConsumption();
        hasBattleWon = false;
        LastConsumedBattleItemId = null;

        if (scanInfoPanel != null)
            scanInfoPanel.SetActive(false);

        if (infectionTypeText != null) infectionTypeText.text = string.Empty;
        if (descriptionText != null) descriptionText.text = string.Empty;
        if (inventoryStatusText != null) inventoryStatusText.text = string.Empty;

        if (!preserveWonGauge)
            ResetContaminationGaugeToInitial();

        SetPurifyVfxActive(false);
    }

    public MonsterData GetCurrentMonsterData() => currentMonsterData;

    public string GetInfectionTypeDisplayText()
    {
        if (currentMonsterData == null || string.IsNullOrEmpty(currentMonsterData.description))
            return "감염물질 이름";

        return currentMonsterData.description;
    }

    public string GetDescriptionDisplayText()
    {
        if (currentMonsterData == null || string.IsNullOrEmpty(currentMonsterData.purification_method))
            return "정화 방법 설명";

        return currentMonsterData.purification_method;
    }

    public void SetMonsterById(string monsterId)
    {
        if (DataManager.Instance == null)
        {
            Debug.LogWarning("[UIBattleManager] DataManager가 없어 몬스터 데이터를 불러올 수 없습니다.");
            return;
        }

        MonsterData data = DataManager.Instance.GetMonsterData(monsterId);
        if (data == null)
        {
            Debug.LogWarning($"[UIBattleManager] 몬스터 ID '{monsterId}' 데이터를 찾을 수 없습니다.");
            return;
        }

        ApplyMonsterData(data);
    }

    private void LoadMonsterFromData()
    {
        if (DataManager.Instance == null)
            return;

        string monsterId = ResolveBattleMonsterId();
        if (string.IsNullOrEmpty(monsterId))
        {
            Debug.LogWarning("[UIBattleManager] 유효한 몬스터 ID를 찾지 못했습니다. JSON/충돌 매핑을 확인하세요.");
            ClearCurrentMonsterUI();
            return;
        }

        SetMonsterById(monsterId);
    }

    private void ClearCurrentMonsterUI()
    {
        currentMonsterData = null;
        currentMonsterId = null;

        if (monsterNameText != null)
            monsterNameText.text = "Unknown";

        if (difficultyText != null)
            difficultyText.text = "Unknown";

        if (monsterImage != null)
        {
            monsterImage.sprite = null;
            SetMonsterImageVisible(false);
        }

        monsterSpriteLooper?.StopAll();

        if (contaminationSlider != null)
        {
            contaminationSlider.maxValue = DefaultContaminationLevel;
            contaminationSlider.value = DefaultContaminationLevel;
            SyncContaminationValueText();
        }
    }

    private string ResolveBattleMonsterId()
    {
        string encounterId = BattleEncounterContext.ConsumeEncounteredMonsterId();
        bool isEncounterValid = IsValidMonsterId(encounterId);
        if (isEncounterValid)
        {
            lastResolvedEncounterMonsterId = encounterId;
            Debug.LogWarning($"[UIBattleManager] ResolveBattleMonsterId: encounterId='{encounterId}' (valid)");
            return encounterId;
        }

        string sceneResolvedId = TryResolveEncounteredMonsterIdFromScene();
        bool isSceneResolvedValid = IsValidMonsterId(sceneResolvedId);
        if (isSceneResolvedValid)
        {
            lastResolvedEncounterMonsterId = sceneResolvedId;
            Debug.LogWarning($"[UIBattleManager] ResolveBattleMonsterId: encounterId='{encounterId ?? "null"}' invalid, sceneResolvedId='{sceneResolvedId}' (valid)");
            return sceneResolvedId;
        }

        bool isCachedValid = IsValidMonsterId(lastResolvedEncounterMonsterId);
        if (isCachedValid)
        {
            Debug.LogWarning($"[UIBattleManager] ResolveBattleMonsterId: encounter/scene invalid, cachedId='{lastResolvedEncounterMonsterId}' (valid)");
            return lastResolvedEncounterMonsterId;
        }

        Debug.LogWarning($"[UIBattleManager] ResolveBattleMonsterId 실패. encounterId='{encounterId ?? "null"}', sceneResolvedId='{sceneResolvedId ?? "null"}', cachedId='{lastResolvedEncounterMonsterId ?? "null"}'");

        return null;
    }

    private bool IsValidMonsterId(string monsterId)
    {
        if (string.IsNullOrEmpty(monsterId) || DataManager.Instance == null)
            return false;

        return DataManager.Instance.GetMonsterData(monsterId) != null;
    }

    private string GetFirstMonsterIdFromJson()
    {
        if (DataManager.Instance == null)
            return null;

        List<string> ids = DataManager.Instance.GetMonsterIds();
        if (ids == null || ids.Count == 0)
            return null;

        ids.Sort(StringComparer.Ordinal);
        for (int i = 0; i < ids.Count; i++)
        {
            if (IsValidMonsterId(ids[i]))
                return ids[i];
        }

        return null;
    }

    private string TryResolveEncounteredMonsterIdFromScene()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
            return null;

        Collider2D playerCollider = player.GetComponent<Collider2D>();
        if (playerCollider != null)
        {
            const int maxOverlapCount = 32;
            Collider2D[] overlapHits = new Collider2D[maxOverlapCount];
            int hitCount = playerCollider.Overlap(ContactFilter2D.noFilter, overlapHits);
            string overlapResolvedId = ResolveClosestMonsterId(player.transform.position, overlapHits, hitCount);
            if (!string.IsNullOrEmpty(overlapResolvedId))
                return overlapResolvedId;
        }

        float radius = 0.6f;
        if (playerCollider != null)
            radius = Mathf.Max(playerCollider.bounds.extents.x, playerCollider.bounds.extents.y) + 0.3f;

        Collider2D[] hits = Physics2D.OverlapCircleAll(player.transform.position, radius);
        return ResolveClosestMonsterId(player.transform.position, hits, hits.Length);
    }

    private string ResolveClosestMonsterId(Vector2 playerPosition, Collider2D[] hits, int hitCount)
    {
        if (hits == null || hitCount <= 0)
            return null;

        string closestResolvedId = null;
        float closestDistanceSqr = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null || !IsMonsterLikeCollider(hit))
                continue;

            string resolvedId = TryResolveMonsterIdFromObjectName(hit.gameObject.name);
            if (string.IsNullOrEmpty(resolvedId))
                continue;

            float distanceSqr = ((Vector2)hit.transform.position - playerPosition).sqrMagnitude;
            if (distanceSqr < closestDistanceSqr)
            {
                closestDistanceSqr = distanceSqr;
                closestResolvedId = resolvedId;
            }
        }

        return closestResolvedId;
    }

    private bool IsMonsterLikeCollider(Collider2D col)
    {
        try
        {
            if (col.CompareTag("Monster"))
                return true;
        }
        catch (UnityException)
        {
        }

        string objectName = col.gameObject.name;
        if (string.IsNullOrEmpty(objectName))
            return false;

        string lower = objectName.ToLowerInvariant();
         return lower.Contains("slime") || lower.Contains("m001") ||
             lower.Contains("fungus") || lower.Contains("mold") || lower.Contains("m002") ||
             lower.Contains("fire") || lower.Contains("m003") ||
               objectName.Contains("슬라임") || objectName.Contains("곰팡") || objectName.Contains("불");
    }

    private string TryResolveMonsterIdFromObjectName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName) || DataManager.Instance == null)
            return null;

        string lowerObjectName = objectName.ToLowerInvariant();
        List<string> ids = DataManager.Instance.GetMonsterIds();
        for (int i = 0; i < ids.Count; i++)
        {
            string id = ids[i];
            MonsterData data = DataManager.Instance.GetMonsterData(id);
            if (data == null)
                continue;

            if (!string.IsNullOrEmpty(data.name) && lowerObjectName.Contains(data.name.ToLowerInvariant()))
                return id;

            if (!string.IsNullOrEmpty(data.image_key) && lowerObjectName.Contains(data.image_key.ToLowerInvariant()))
                return id;

            if (!string.IsNullOrEmpty(data.id) && lowerObjectName.Contains(data.id.ToLowerInvariant()))
                return id;

            if (!string.IsNullOrEmpty(data.id))
            {
                string normalizedId = data.id.Replace("-", string.Empty).ToLowerInvariant();
                if (lowerObjectName.Contains(normalizedId))
                    return id;
            }
        }

        if (lowerObjectName.Contains("slime") || lowerObjectName.Contains("m001") || objectName.Contains("슬라임"))
            return "M-001";

        if (lowerObjectName.Contains("fungus") || lowerObjectName.Contains("mold") || lowerObjectName.Contains("m002") || objectName.Contains("곰팡"))
            return "M-002";

        if (lowerObjectName.Contains("fire") || lowerObjectName.Contains("m003") || objectName.Contains("불"))
            return "M-003";

        return null;
    }

    private void ApplyMonsterData(MonsterData data)
    {
        currentMonsterData = data;
        currentMonsterId = data != null ? data.id : null;

        string difficulty = string.IsNullOrEmpty(data.capture_difficulty) ? "Unknown" : data.capture_difficulty;
        int maxContamination = GetMonsterMaxContamination(data);
        int currentContamination = ResolveInitialContamination(data);
        contaminationAtBattleEntry = currentContamination;
        BattleEncounterContext.ClearFleeExit();
        SetMonsterBasicUI(data.name, difficulty, maxContamination, currentContamination);
        ApplyMonsterBattleSprite(data);
    }

    private void ApplyMonsterBattleSprite(MonsterData data)
    {
        ResolveMonsterSpriteLooper();

        if (monsterSpriteLooper != null &&
            data != null &&
            !string.IsNullOrEmpty(data.image_key) &&
            monsterSpriteLooper.ConfigureFromAtlas(data.image_key))
        {
            monsterSpriteLooper.PlayIdleLoop();
            SetMonsterImageVisible(false);
            return;
        }

        monsterSpriteLooper?.StopAll();

        if (monsterImage != null)
        {
            monsterImage.sprite = GetMonsterSprite(data);
            SetMonsterImageVisible(monsterImage.sprite != null);
        }
    }

    private void ResolveMonsterSpriteLooper()
    {
        if (monsterSpriteLooper != null)
            return;

        if (monsterImage != null)
            monsterSpriteLooper = monsterImage.GetComponent<BattleMonsterSpriteLooper>();

        if (monsterSpriteLooper == null)
            monsterSpriteLooper = GetComponentInChildren<BattleMonsterSpriteLooper>(true);
    }

    private float GetPurifyHitAnimationDuration()
    {
        ResolveMonsterSpriteLooper();

        if (monsterSpriteLooper != null)
        {
            float hitDuration = monsterSpriteLooper.GetHitAnimationDuration();
            if (hitDuration > 0f)
                return hitDuration;
        }

        return Mathf.Max(0.01f, purifyAnimationDuration);
    }

    private void PlayMonsterPurifySound(float hitAnimationDuration)
    {
        MonsterFieldSoundController soundController = ResolveBattleMonsterSoundController();
        if (soundController == null)
        {
            Debug.LogWarning(
                $"[UIBattleManager] 정화 사운드 대상을 찾지 못했습니다. " +
                $"monsterId={GetCurrentMonsterData()?.id ?? BattleEncounterContext.PeekEncounteredMonsterId() ?? "null"}, " +
                $"hitDuration={hitAnimationDuration:F2}s");
            return;
        }

        Debug.Log(
            $"[UIBattleManager] 정화 사운드 재생 요청 → {soundController.gameObject.name}, " +
            $"hitDuration={hitAnimationDuration:F2}s");

        soundController.PlayBattlePurifySound(hitAnimationDuration);
    }

    private MonsterFieldSoundController ResolveBattleMonsterSoundController()
    {
        GameObject encountered = BattleEncounterContext.PeekEncounteredMonsterObject();
        if (encountered != null)
        {
            MonsterFieldSoundController controller = encountered.GetComponent<MonsterFieldSoundController>();
            if (controller != null)
                return controller;
        }

        MonsterData data = GetCurrentMonsterData();
        string monsterId = data != null && !string.IsNullOrEmpty(data.id)
            ? data.id
            : BattleEncounterContext.PeekEncounteredMonsterId();

        if (string.IsNullOrEmpty(monsterId))
            return null;

        MonsterFieldSoundController[] controllers = FindObjectsByType<MonsterFieldSoundController>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        Vector2 origin = player != null ? player.transform.position : Vector2.zero;
        MonsterFieldSoundController closestController = null;
        float closestDistance = float.MaxValue;

        for (int i = 0; i < controllers.Length; i++)
        {
            MonsterFieldSoundController candidate = controllers[i];
            if (candidate == null || !MatchesMonsterSoundTarget(candidate.gameObject, monsterId))
                continue;

            float distance = Vector2.Distance(origin, candidate.transform.position);
            if (distance >= closestDistance)
                continue;

            closestController = candidate;
            closestDistance = distance;
        }

        return closestController;
    }

    private static bool MatchesMonsterSoundTarget(GameObject candidate, string monsterId)
    {
        if (candidate == null || string.IsNullOrEmpty(monsterId))
            return false;

        string objectName = candidate.name.ToLowerInvariant();

        switch (monsterId)
        {
            case "M-001":
                return objectName.Contains("slime") || objectName.Contains("m001") || candidate.name.Contains("슬라임");
            case "M-002":
                return objectName.Contains("mold") || objectName.Contains("fungus") || objectName.Contains("m002") ||
                       candidate.name.Contains("곰팡");
            case "M-003":
                return objectName.Contains("fire") || objectName.Contains("m003") || candidate.name.Contains("불");
            default:
                return false;
        }
    }

    private Sprite GetMonsterSprite(MonsterData data)
    {
        if (AtlasManager.Instance == null || data == null)
            return null;

        if (!string.IsNullOrEmpty(data.image_key))
        {
            Sprite sprite = GetBestMonsterAtlasSprite(data.image_key);
            if (sprite != null)
                return sprite;
        }

        if (!string.IsNullOrEmpty(data.id))
        {
            Sprite sprite = AtlasManager.Instance.GetMonsterSprite(data.id);
            if (sprite != null)
                return sprite;
        }

        if (!string.IsNullOrEmpty(data.name))
            return AtlasManager.Instance.GetMonsterSprite(data.name);

        return null;
    }

    private Sprite GetBestMonsterAtlasSprite(string baseKey)
    {
        Sprite direct = AtlasManager.Instance.GetMonsterSprite(baseKey);
        if (direct == null)
            return null;

        // Large sprites are usually already the intended representative image.
        // Skip extra probing to avoid warning spam from missing *_N keys.
        if (GetSpriteArea(direct) >= 4096f)
            return direct;

        Sprite best = direct;

        // For very small fallback slices (e.g. fire _0), probe sequential variants conservatively.
        for (int i = 1; i <= 8; i++)
        {
            Sprite candidate = AtlasManager.Instance.GetMonsterSprite($"{baseKey}_{i}");
            if (candidate == null)
                break;

            if (best == null || GetSpriteArea(candidate) > GetSpriteArea(best))
                best = candidate;
        }

        return best;
    }

    private static float GetSpriteArea(Sprite sprite)
    {
        Rect rect = sprite.rect;
        return rect.width * rect.height;
    }

    public void SetMonsterBasicUI(string name, string difficulty, int maxContamination)
    {
        SetMonsterBasicUI(name, difficulty, maxContamination, maxContamination);
    }

    private void SetMonsterBasicUI(string name, string difficulty, int maxContamination, int currentContamination)
    {
        monsterNameText.text = name;
        difficultyText.text = difficulty;
        GetComponent<EnemyStatus>()?.ConfigureStatusText(difficultyText, difficulty);
        contaminationSlider.maxValue = maxContamination;
        contaminationSlider.value = Mathf.Clamp(currentContamination, 0, maxContamination);
        SyncContaminationValueText();
    }

    // [탐색] 버튼을 눌렀을 때 실행될 함수
    public void RevealScannedInfo(string infectionType, string description, string inventoryStatus)
    {
        if (scanInfoPanel != null)
        {
            scanInfoPanel.SetActive(true);
        }

        if (infectionTypeText != null)
            infectionTypeText.text = infectionType ?? string.Empty;

        if (descriptionText != null)
            descriptionText.text = description ?? string.Empty;

        if (inventoryStatusText != null)
            inventoryStatusText.text = inventoryStatus ?? string.Empty;
    }

    public int GetCurrentContamination()
    {
        if (contaminationSlider == null)
            return 0;

        return Mathf.RoundToInt(contaminationSlider.value);
    }

    public int GetMaxContamination()
    {
        if (contaminationSlider == null)
            return DefaultContaminationLevel;

        return Mathf.RoundToInt(contaminationSlider.maxValue);
    }

    public void UpdateContaminationGauge(int currentContamination)
    {
        contaminationSlider.value = currentContamination;
        SyncContaminationValueText();
    }

    public void ReduceContamination(int amount)
    {
        if (contaminationSlider == null || hasBattleWon)
            return;

        contaminationSlider.value = Mathf.Max(0, contaminationSlider.value - amount);
        CacheCurrentMonsterContamination((int)contaminationSlider.value);
        SyncContaminationValueText();
        Debug.Log($"[UIBattleManager] 오염도 감소: {contaminationSlider.value}");

        if (contaminationSlider.value <= 0)
            HandleContaminationCleared();
    }

    private void HandleContaminationCleared()
    {
        OnBattleWin();
    }

    /// <summary>오염도 0 달성 시 호출. 승리 처리 후 정화 아이템 1개를 실제 차감합니다.</summary>
    public void OnBattleWin()
    {
        if (hasBattleWon)
            return;

        hasBattleWon = true;

        ResolveTurnController();
        turnController?.NotifyBattleWon();

        ClearCurrentMonsterContamination();
        if (contaminationSlider != null)
        {
            contaminationSlider.value = 0;
            SyncContaminationValueText();
        }

        Debug.Log("[UIBattleManager] 오염도 0 도달! 정화 완료.");

        CommitPendingPurifyItemOnBattleWin();
        StopPurificationUiSound();
        OnContaminationEmpty?.Invoke();
    }

    public void ResetMonsterBattleStatus()
    {
        ResetBattleSessionState();
    }

    /// <summary>배틀씬 종료·새 전투 진입 시 몬스터 전투 버프/디버프를 초기화합니다.</summary>
    public void ResetBattleSessionState()
    {
        ResetEnemyStatusForBattle();
    }

    private void ResetEnemyStatusForBattle()
    {
        EnemyStatus status = GetComponent<EnemyStatus>();
        if (status == null)
            status = GetComponentInChildren<EnemyStatus>(true);

        if (status != null)
            status.ResetForBattle(GetDifficultyDisplayText());
    }

    private void CommitPendingPurifyItemOnBattleWin()
    {
        if (BattleEncounterContext.WasFieldEntryPrepaid)
        {
            LastConsumedBattleItemId = BattleEncounterContext.GetFieldEntryConsumedItemId();
            ClearPendingPurifyItemConsumption();
            return;
        }

        if (!hasPendingPurifyItemConsumption || InventoryManager.Instance == null)
        {
            ClearPendingPurifyItemConsumption();
            return;
        }

        string requiredItemId = GetRequiredPurifyItemId();
        if (InventoryManager.Instance.TryConsumeBattleItemForRequirement(
                requiredItemId, out _, out string consumedItemId))
        {
            LastConsumedBattleItemId = consumedItemId;
            Debug.Log($"[UIBattleManager] 승리 확정 — 정화 아이템 차감: {consumedItemId}");
            UIInventory.RefreshAllVisible();
        }
        else
        {
            Debug.LogWarning(
                $"[UIBattleManager] 승리했지만 정화 아이템 차감 실패 — 요구 ID: {requiredItemId}");
        }

        ClearPendingPurifyItemConsumption();
    }

    private void ClearPendingPurifyItemConsumption()
    {
        hasPendingPurifyItemConsumption = false;
    }

    private void ResolveTurnController()
    {
        if (turnController != null && turnController.isActiveAndEnabled)
            return;

        turnController = GetComponent<BattleTurnController>();
        if (turnController == null)
            turnController = GetComponentInChildren<BattleTurnController>(true);

        if (turnController == null)
            turnController = FindAnyObjectByType<BattleTurnController>(FindObjectsInactive.Include);
    }

    private void EnsureTurnController()
    {
        ResolveTurnController();
        if (turnController != null)
            return;

        turnController = gameObject.AddComponent<BattleTurnController>();
        Debug.LogWarning("[UIBattleManager] BattleTurnController가 없어 루트에 새로 추가했습니다.");
    }

    private void EnsureEnemyStatus()
    {
        EnemyStatus status = GetComponent<EnemyStatus>();
        if (status == null)
            status = gameObject.AddComponent<EnemyStatus>();

        status.ConfigureStatusText(difficultyText, GetDifficultyDisplayText());
    }

    private void ResolveAndBindPlayerOxygen()
    {
        PlayerOxygen runtimeOxygen = PlayerOxygen.ResolveRuntime();
        turnController?.BindPlayerOxygen(runtimeOxygen);

        if (battleOxygenGauge == null)
            battleOxygenGauge = GetComponentInChildren<UIBattleOxygenGauge>(true);

        UIBattleOxygenGauge[] gauges =
            GetComponentsInChildren<UIBattleOxygenGauge>(true);

        for (int i = 0; i < gauges.Length; i++)
        {
            if (gauges[i] != null)
                gauges[i].BindToRuntimePlayerOxygen();
        }
    }

    public string GetDifficultyDisplayText()
    {
        if (difficultyText == null)
            return string.Empty;

        return difficultyText.text;
    }

    private void SubscribeBattleEnded()
    {
        if (isSubscribedToBattleEnded || GameManager.Instance == null)
            return;

        GameManager.Instance.OnBattleEnded -= HandleBattleEnded;
        GameManager.Instance.OnBattleEnded += HandleBattleEnded;
        isSubscribedToBattleEnded = true;
    }

    private void UnsubscribeBattleEnded()
    {
        if (!isSubscribedToBattleEnded || GameManager.Instance == null)
            return;

        GameManager.Instance.OnBattleEnded -= HandleBattleEnded;
        isSubscribedToBattleEnded = false;
    }

    private void HandleBattleEnded()
    {
        ResolveTurnController();
        turnController?.NotifyBattleEnded();
        ExitBattle();
        FinalizeContaminationOnce();
        ForceRestoreFieldPhysics();

        lastResolvedEncounterMonsterId = null;
        BattleEncounterContext.SetEncounteredMonsterId(null);
        BattleEncounterContext.ClearFieldEntryPrepaid();
    }

    private void FinalizeContaminationOnce()
    {
        if (hasFinalizedContaminationForSession)
            return;

        hasFinalizedContaminationForSession = true;
        FinalizeContaminationOnBattleClose();
    }

    /// <summary>배틀 UI 비활성·게임오버 등에서 플레이어/몬스터 물리 잠금을 해제합니다.</summary>
    public void ForceRestoreFieldPhysics()
    {
        UnlockPlayerMovement();
        UnlockMonsterMovement();
        RestoreAllMonsterRigidbodiesInScene();
    }

    /// <summary>씬에 존재하는 모든 UIBattleManager의 배틀 상태·물리를 초기화합니다.</summary>
    public static void ResetAllRuntimeBattleState()
    {
        UIBattleManager[] managers =
            FindObjectsByType<UIBattleManager>(FindObjectsInactive.Include);

        for (int i = 0; i < managers.Length; i++)
        {
            UIBattleManager manager = managers[i];
            if (manager == null)
                continue;

            manager.ExitBattle();
            manager.ForceRestoreFieldPhysics();
        }

        RestoreAllMonsterRigidbodiesInScene();
    }

    private static void RestoreAllMonsterRigidbodiesInScene()
    {
        Rigidbody2D[] rigidbodies = FindObjectsByType<Rigidbody2D>(FindObjectsInactive.Exclude);
        for (int i = 0; i < rigidbodies.Length; i++)
        {
            Rigidbody2D body = rigidbodies[i];
            if (body == null)
                continue;

            if (!body.simulated)
                body.simulated = true;
        }
    }

    private void ResetContaminationGaugeToInitial()
    {
        if (contaminationSlider == null)
            return;

        int maxContamination = GetMonsterMaxContamination(currentMonsterData);
        int initialContamination = ResolveInitialContamination(currentMonsterData);

        contaminationSlider.maxValue = maxContamination;
        contaminationSlider.value = Mathf.Clamp(initialContamination, 0, maxContamination);
        SyncContaminationValueText();
    }

    private void TryResolveContaminationValueText()
    {
        if (contaminationValueText != null || contaminationSlider == null)
            return;

        Transform barRoot = contaminationSlider.transform.parent;
        if (barRoot == null)
            return;

        TextMeshProUGUI[] texts = barRoot.GetComponentsInChildren<TextMeshProUGUI>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i].gameObject.name == "Text (TMP) (1)")
            {
                contaminationValueText = texts[i];
                return;
            }
        }

        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i].gameObject.name.IndexOf("Value", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                contaminationValueText = texts[i];
                return;
            }
        }

        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i].gameObject.name != "PollutionDegreeText")
            {
                contaminationValueText = texts[i];
                return;
            }
        }
    }

    private void SyncContaminationValueText()
    {
        if (contaminationValueText == null || contaminationSlider == null)
            return;

        contaminationValueText.text =
            $"{Mathf.CeilToInt(contaminationSlider.value)}/{Mathf.CeilToInt(contaminationSlider.maxValue)}";
    }

    private int GetMonsterMaxContamination(MonsterData data)
    {
        if (data == null)
            return DefaultContaminationLevel;

        return data.contamination_level > 0 ? data.contamination_level : DefaultContaminationLevel;
    }

    private int ResolveInitialContamination(MonsterData data)
    {
        if (data == null)
            return DefaultContaminationLevel;

        int maxContamination = GetMonsterMaxContamination(data);
        if (!string.IsNullOrEmpty(data.id) && contaminationProgressByMonsterId.TryGetValue(data.id, out int savedContamination))
            return Mathf.Clamp(savedContamination, 0, maxContamination);

        return maxContamination;
    }

    private void CacheCurrentMonsterContamination(int contamination)
    {
        if (string.IsNullOrEmpty(currentMonsterId))
            return;

        contaminationProgressByMonsterId[currentMonsterId] = Mathf.Max(0, contamination);
    }

    private void ClearCurrentMonsterContamination()
    {
        if (string.IsNullOrEmpty(currentMonsterId))
            return;

        contaminationProgressByMonsterId.Remove(currentMonsterId);
    }

    private void FinalizeContaminationOnBattleClose()
    {
        if (BattleEncounterContext.IsFleeExitPending)
        {
            RevertContaminationProgressAfterFlee();
            BattleEncounterContext.ClearFleeExit();
            return;
        }

        SaveCurrentContaminationProgress();
    }

    private void RevertContaminationProgressAfterFlee()
    {
        if (string.IsNullOrEmpty(currentMonsterId))
            return;

        int restored = Mathf.Max(0, contaminationAtBattleEntry);
        contaminationProgressByMonsterId[currentMonsterId] = restored;

        if (contaminationSlider != null)
        {
            contaminationSlider.value = Mathf.Clamp(restored, 0, contaminationSlider.maxValue);
            SyncContaminationValueText();
        }

        Debug.Log($"[UIBattleManager] 도망 → 오염도 진행도 복구: {currentMonsterId} = {restored}");
    }

    private void SaveCurrentContaminationProgress()
    {
        if (contaminationSlider == null || string.IsNullOrEmpty(currentMonsterId))
            return;

        int current = Mathf.RoundToInt(contaminationSlider.value);
        if (current <= 0)
        {
            contaminationProgressByMonsterId.Remove(currentMonsterId);
            return;
        }

        contaminationProgressByMonsterId[currentMonsterId] = current;
    }

    private void LockPlayerMovementAtBattleEntry()
    {
        if (lockedPlayerController != null)
            return;

        lockedPlayerController = FindAnyObjectByType<PlayerController>(FindObjectsInactive.Exclude);
        if (lockedPlayerController == null)
            return;

        lockedPlayerRigidbody = lockedPlayerController.GetComponent<Rigidbody2D>();
        if (lockedPlayerRigidbody != null)
        {
            wasPlayerRigidbodySimulated = lockedPlayerRigidbody.simulated;
            playerConstraints = lockedPlayerRigidbody.constraints;
            lockedPlayerRigidbody.linearVelocity = Vector2.zero;
            lockedPlayerRigidbody.angularVelocity = 0f;
            lockedPlayerRigidbody.simulated = false;
        }
    }

    private void UnlockPlayerMovement()
    {
        if (lockedPlayerRigidbody != null)
        {
            lockedPlayerRigidbody.constraints = playerConstraints;
            lockedPlayerRigidbody.simulated = true;
            lockedPlayerRigidbody.linearVelocity = Vector2.zero;
            lockedPlayerRigidbody.angularVelocity = 0f;
        }

        lockedPlayerController = null;
        lockedPlayerRigidbody = null;
        wasPlayerRigidbodySimulated = false;
        playerConstraints = RigidbodyConstraints2D.None;
    }

    private void LockMonsterMovementAtBattleEntry()
    {
        if (lockedMonsters.Count > 0)
            return;

        Collider2D[] colliders = FindObjectsByType<Collider2D>(FindObjectsInactive.Exclude);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D col = colliders[i];
            if (col == null || !IsMonsterLikeCollider(col))
                continue;

            Rigidbody2D rb = col.attachedRigidbody != null ? col.attachedRigidbody : col.GetComponent<Rigidbody2D>();
            if (rb == null || HasLockedSnapshot(rb))
                continue;

            lockedMonsters.Add(new MonsterPhysicsSnapshot
            {
                rigidbody = rb,
                wasSimulated = rb.simulated,
                constraints = rb.constraints
            });

            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.simulated = false;
        }
    }

    private bool HasLockedSnapshot(Rigidbody2D rb)
    {
        for (int i = 0; i < lockedMonsters.Count; i++)
        {
            if (lockedMonsters[i].rigidbody == rb)
                return true;
        }

        return false;
    }

    private void UnlockMonsterMovement()
    {
        for (int i = 0; i < lockedMonsters.Count; i++)
        {
            MonsterPhysicsSnapshot snapshot = lockedMonsters[i];
            if (snapshot == null || snapshot.rigidbody == null)
                continue;

            snapshot.rigidbody.constraints = snapshot.constraints;
            snapshot.rigidbody.simulated = true;
            snapshot.rigidbody.linearVelocity = Vector2.zero;
            snapshot.rigidbody.angularVelocity = 0f;
        }

        lockedMonsters.Clear();
    }
}