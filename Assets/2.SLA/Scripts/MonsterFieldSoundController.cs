using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(Rigidbody2D))]
[DefaultExecutionOrder(200)]
public class MonsterFieldSoundController : MonoBehaviour
{
    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");

    [Header("Clips")]
    [SerializeField] private AudioClip idleClip;
    [SerializeField] private AudioClip runClip;
    [SerializeField] private AudioClip attackClip;
    [SerializeField] private AudioClip purificationCompleteClip;

    [Header("Proximity")]
    [Tooltip("플레이어가 이 거리 안에 들어오면 사운드를 재생합니다.")]
    [SerializeField] private float hearRange = 7f;

    [Header("Movement")]
    [SerializeField] private float movingThreshold = 0.01f;
    [Tooltip("이동이 잠깐 끊겨도 Run 루프가 바로 Idle로 바뀌지 않도록 유지하는 시간(초)")]
    [SerializeField] private float runLoopHoldDuration = 0.5f;

    private enum LoopKind
    {
        None,
        Idle,
        Run
    }

    private AudioSource fieldLoopSource;
    private AudioSource sfxSource;
    private Rigidbody2D rb;
    private Animator animator;
    private MonsterAnimationController animationController;
    private Transform player;
    private LoopKind currentLoop = LoopKind.None;
    private float runLoopHoldTimer;
    private bool purificationPlaying;
    private Coroutine purificationRoutine;
    private bool gameManagerSubscribed;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        animationController = GetComponent<MonsterAnimationController>();
        ConfigureAudioSources();
    }

    private void ConfigureAudioSources()
    {
        AudioSource[] sources = GetComponents<AudioSource>();
        fieldLoopSource = sources.Length > 0 ? sources[0] : gameObject.AddComponent<AudioSource>();
        fieldLoopSource.playOnAwake = false;
        fieldLoopSource.loop = true;
        fieldLoopSource.spatialBlend = 0f;
        fieldLoopSource.volume = ResolveFieldLoopVolume();

        if (sources.Length > 1)
            sfxSource = sources[1];
        else
            sfxSource = gameObject.AddComponent<AudioSource>();

        sfxSource.playOnAwake = false;
        sfxSource.loop = false;
        sfxSource.spatialBlend = 0f;
    }

    private void OnEnable()
    {
        TrySubscribeGameManager();
    }

    private void Start()
    {
        TryFindPlayer();

        if (!IsBattleActive())
            UpdateLoopSound();
    }

    private void OnDisable()
    {
        UnsubscribeGameManager();
        CancelPurificationPlayback();
        StopLoop();
    }

    private void LateUpdate()
    {
        if (!gameManagerSubscribed)
            TrySubscribeGameManager();

        if (!GameplayAudioGuard.CanPlayFieldCharacterSounds)
        {
            CancelPurificationPlayback();
            StopLoop();
            return;
        }

        if (IsBattleActive())
        {
            if (purificationPlaying)
                return;

            if (IsActiveBattleMonster())
                UpdateBattleLoopSound();
            else if (currentLoop != LoopKind.None)
                StopLoop();

            return;
        }

        UpdateLoopSound();
        MaintainFieldLoopPlayback();
    }

    private void UpdateLoopSound()
    {
        if (player == null)
            TryFindPlayer();

        if (!IsPlayerWithinHearRange())
        {
            StopLoop();
            return;
        }

        if (idleClip == null)
        {
            StopLoop();
            return;
        }

        if (ShouldPlayRunLoop() && runClip != null)
            SetFieldLoop(LoopKind.Run, runClip);
        else
            SetFieldLoop(LoopKind.Idle, idleClip);
    }

    private void MaintainFieldLoopPlayback()
    {
        if (purificationPlaying || currentLoop == LoopKind.None || fieldLoopSource == null)
            return;

        AudioClip expectedClip = currentLoop == LoopKind.Run ? runClip : idleClip;
        if (expectedClip == null)
            return;

        fieldLoopSource.loop = true;
        fieldLoopSource.mute = false;

        if (fieldLoopSource.clip != expectedClip)
        {
            fieldLoopSource.clip = expectedClip;
            fieldLoopSource.time = 0f;
            fieldLoopSource.Play();
            return;
        }

        if (!fieldLoopSource.isPlaying)
            fieldLoopSource.Play();
    }

    private void TryFindPlayer()
    {
        if (player != null)
            return;

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
            player = playerObject.transform;
    }

    private bool IsPlayerWithinHearRange()
    {
        if (player == null)
            return false;

        float distance = Vector2.Distance(transform.position, player.position);
        return distance <= hearRange;
    }

    private bool ShouldPlayRunLoop()
    {
        if (IsMonsterMoving())
        {
            runLoopHoldTimer = runLoopHoldDuration;
            return true;
        }

        if (runLoopHoldTimer > 0f)
        {
            runLoopHoldTimer -= Time.deltaTime;
            return true;
        }

        return false;
    }

    private bool IsMonsterMoving()
    {
        if (IsInLocomotionAnimatorState())
            return true;

        if (animator != null && animator.isActiveAndEnabled && animator.GetBool(IsMovingHash))
            return true;

        if (animationController != null && animationController.IsMoving)
            return true;

        return rb != null &&
               rb.linearVelocity.sqrMagnitude > movingThreshold * movingThreshold;
    }

    private bool IsInLocomotionAnimatorState()
    {
        if (animator == null || !animator.isActiveAndEnabled)
            return false;

        if (IsLocomotionState(animator.GetCurrentAnimatorStateInfo(0)))
            return true;

        if (animator.IsInTransition(0) && IsLocomotionState(animator.GetNextAnimatorStateInfo(0)))
            return true;

        return false;
    }

    private static bool IsLocomotionState(AnimatorStateInfo stateInfo)
    {
        return stateInfo.IsName("Monster_M001_Slime_Move")
            || stateInfo.IsName("Monster_M002_Mold_Move")
            || stateInfo.IsName("Monster_M003_Fire_Move");
    }

    private bool IsActiveBattleMonster()
    {
        GameObject encountered = BattleEncounterContext.PeekEncounteredMonsterObject();
        if (encountered != null)
            return encountered == gameObject;

        return IsPlayerWithinHearRange();
    }

    private void UpdateBattleLoopSound()
    {
        if (idleClip != null)
            SetFieldLoop(LoopKind.Idle, idleClip);
        else
            StopLoop();
    }

    private void SetFieldLoop(LoopKind kind, AudioClip clip)
    {
        if (purificationPlaying || clip == null || fieldLoopSource == null)
            return;

        bool kindChanged = currentLoop != kind;
        bool clipChanged = fieldLoopSource.clip != clip;

        fieldLoopSource.loop = true;
        fieldLoopSource.mute = false;
        fieldLoopSource.volume = ResolveFieldLoopVolume();

        if (clipChanged)
        {
            fieldLoopSource.clip = clip;
            fieldLoopSource.time = 0f;
            fieldLoopSource.Play();
        }
        else if (!fieldLoopSource.isPlaying)
        {
            fieldLoopSource.time = 0f;
            fieldLoopSource.Play();
        }

        if (kindChanged)
        {
            Debug.Log(
                $"[MonsterFieldSoundController] {gameObject.name} 루프 재생 — {kind}, " +
                $"clip={GetClipName(clip)}, animMove={IsInLocomotionAnimatorState()}, " +
                $"battle={IsBattleActive()}");
        }

        currentLoop = kind;
    }

    private void StopLoop()
    {
        bool wasPlaying = fieldLoopSource != null && fieldLoopSource.isPlaying;
        LoopKind previousLoop = currentLoop;
        currentLoop = LoopKind.None;
        runLoopHoldTimer = 0f;

        if (fieldLoopSource != null && fieldLoopSource.isPlaying)
            fieldLoopSource.Stop();

        if (wasPlaying)
        {
            Debug.Log(
                $"[MonsterFieldSoundController] {gameObject.name} 루프 정지 — " +
                $"previous={previousLoop}, battle={IsBattleActive()}");
        }
    }

    private void SuppressLoopForPurification()
    {
        LoopKind previousLoop = currentLoop;
        currentLoop = LoopKind.None;

        if (fieldLoopSource != null)
        {
            if (fieldLoopSource.isPlaying)
                fieldLoopSource.Stop();
        }

        Debug.Log(
            $"[MonsterFieldSoundController] {gameObject.name} 정화 연출용 루프 억제 — previous={previousLoop}");
    }

    private void MuteFieldLoopForSfx()
    {
        if (fieldLoopSource != null && fieldLoopSource.isPlaying)
            fieldLoopSource.Pause();
    }

    private void ResumeFieldLoopAfterSfx()
    {
        if (fieldLoopSource == null || currentLoop == LoopKind.None)
            return;

        AudioClip clip = currentLoop == LoopKind.Run ? runClip : idleClip;
        if (clip == null)
            return;

        fieldLoopSource.loop = true;
        fieldLoopSource.mute = false;
        fieldLoopSource.clip = clip;

        if (!fieldLoopSource.isPlaying)
            fieldLoopSource.Play();
        else
            fieldLoopSource.UnPause();
    }

    private void TrySubscribeGameManager()
    {
        if (gameManagerSubscribed || GameManager.Instance == null)
            return;

        GameManager.Instance.OnBattleStarted += HandleBattleStarted;
        GameManager.Instance.OnBattleEnded += HandleBattleEnded;
        gameManagerSubscribed = true;
    }

    private void UnsubscribeGameManager()
    {
        if (!gameManagerSubscribed || GameManager.Instance == null)
            return;

        GameManager.Instance.OnBattleStarted -= HandleBattleStarted;
        GameManager.Instance.OnBattleEnded -= HandleBattleEnded;
        gameManagerSubscribed = false;
    }

    private void HandleBattleStarted()
    {
        bool isActiveTarget = IsActiveBattleMonster();
        Debug.Log(
            $"[MonsterFieldSoundController] {gameObject.name} 배틀 시작 — " +
            $"activeTarget={isActiveTarget}, encounter={GetEncounteredMonsterName()}");

        if (isActiveTarget)
            UpdateBattleLoopSound();
        else
            StopLoop();
    }

    private void HandleBattleEnded()
    {
        Debug.Log($"[MonsterFieldSoundController] {gameObject.name} 배틀 종료 — 필드 사운드 복귀");
        CancelPurificationPlayback();
        UpdateLoopSound();
    }

    public void StopFieldSoundsForInventoryPause()
    {
        CancelPurificationPlayback();
        StopLoop();
    }

    public IEnumerator PlayBattleAttackSoundRoutine()
    {
        if (!GameplayAudioGuard.CanPlayFieldCharacterSounds || !IsBattleActive() || attackClip == null || sfxSource == null)
            yield break;

        bool resumeLoopAfterAttack = currentLoop != LoopKind.None;

        MuteFieldLoopForSfx();

        if (sfxSource.isPlaying)
            sfxSource.Stop();

        sfxSource.loop = false;
        sfxSource.clip = attackClip;
        sfxSource.pitch = 1f;
        sfxSource.volume = ResolveBattleMonsterSfxVolume();
        sfxSource.time = 0f;
        sfxSource.Play();

        Debug.Log(
            $"[MonsterFieldSoundController] {gameObject.name} 공격 사운드 재생 — " +
            $"clip={GetClipName(attackClip)}");

        yield return new WaitForSecondsRealtime(attackClip.length);

        if (sfxSource.isPlaying)
            sfxSource.Stop();

        if (!resumeLoopAfterAttack || !IsBattleActive() || !IsActiveBattleMonster())
            yield break;

        UpdateBattleLoopSound();
        ResumeFieldLoopAfterSfx();
    }

    public void PlayBattlePurifySound(float hitAnimationDuration)
    {
        if (!GameplayAudioGuard.CanPlayFieldCharacterSounds)
            return;

        if (!IsBattleActive())
        {
            Debug.LogWarning(
                $"[MonsterFieldSoundController] {gameObject.name} 정화 사운드 스킵 — 배틀 상태가 아님");
            return;
        }

        if (purificationCompleteClip == null || sfxSource == null)
        {
            Debug.LogWarning(
                $"[MonsterFieldSoundController] {gameObject.name} 정화 사운드 스킵 — clip/source 없음");
            return;
        }

        if (purificationRoutine != null)
            StopCoroutine(purificationRoutine);

        Debug.Log(
            $"[MonsterFieldSoundController] {gameObject.name} 정화 사운드 시작 — " +
            $"clip={GetClipName(purificationCompleteClip)}, hitDuration={hitAnimationDuration:F2}s");

        purificationRoutine = StartCoroutine(PlayPurificationThenResumeIdle(hitAnimationDuration));
    }

    private IEnumerator PlayPurificationThenResumeIdle(float hitAnimationDuration)
    {
        purificationPlaying = true;
        SuppressLoopForPurification();

        if (sfxSource.isPlaying)
            sfxSource.Stop();

        sfxSource.loop = false;
        sfxSource.clip = purificationCompleteClip;
        sfxSource.pitch = 1f;
        sfxSource.volume = ResolveBattlePurifySfxVolume();
        sfxSource.time = 0f;
        sfxSource.Play();

        float waitDuration = Mathf.Max(0.01f, hitAnimationDuration);
        yield return new WaitForSecondsRealtime(waitDuration);

        if (sfxSource.isPlaying)
            sfxSource.Stop();

        Debug.Log(
            $"[MonsterFieldSoundController] {gameObject.name} 정화 사운드 종료 — " +
            $"waited={waitDuration:F2}s");

        purificationPlaying = false;
        purificationRoutine = null;

        if (!IsBattleActive() || !IsActiveBattleMonster())
        {
            Debug.Log(
                $"[MonsterFieldSoundController] {gameObject.name} Idle 재개 스킵 — " +
                $"battle={IsBattleActive()}, activeTarget={IsActiveBattleMonster()}");
            yield break;
        }

        Debug.Log($"[MonsterFieldSoundController] {gameObject.name} Idle 루프 재개");
        UpdateBattleLoopSound();
        ResumeFieldLoopAfterSfx();
    }

    private void CancelPurificationPlayback()
    {
        if (purificationRoutine != null)
        {
            StopCoroutine(purificationRoutine);
            purificationRoutine = null;
        }

        purificationPlaying = false;

        if (sfxSource != null && sfxSource.isPlaying)
            sfxSource.Stop();
    }

    private static bool IsBattleActive()
    {
        return GameManager.Instance != null && GameManager.Instance.IsInBattle;
    }

    private static string GetClipName(AudioClip clip)
    {
        return clip != null ? clip.name : "(null)";
    }

    private static string GetEncounteredMonsterName()
    {
        GameObject encountered = BattleEncounterContext.PeekEncounteredMonsterObject();
        return encountered != null ? encountered.name : "(none)";
    }

    private static float ResolveFieldLoopVolume()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsInBattle)
            return GameManager.Instance.GetBattleSfxVolume(0.48f);

        if (GameManager.Instance != null)
            return GameManager.Instance.GetFactorySfxVolume(0.45f);

        return 0.225f;
    }

    private static float ResolveBattleMonsterSfxVolume()
    {
        if (GameManager.Instance != null)
            return GameManager.Instance.GetBattleSfxVolume(0.72f);

        return 0.36f;
    }

    private static float ResolveBattlePurifySfxVolume()
    {
        if (GameManager.Instance != null)
            return GameManager.Instance.GetBattleSfxVolume(0.62f);

        return 0.31f;
    }
}
