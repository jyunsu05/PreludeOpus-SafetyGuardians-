using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(Rigidbody2D))]
public class MonsterFieldSoundController : MonoBehaviour
{
    [Header("Clips")]
    [SerializeField] private AudioClip idleClip;
    [SerializeField] private AudioClip runClip;
    [SerializeField] private AudioClip attackClip;
    [SerializeField] private AudioClip purificationCompleteClip;

    [Header("Proximity")]
    [Tooltip("플레이어가 이 거리 안에 들어오면 사운드를 재생합니다.")]
    [SerializeField] private float hearRange = 7f;
    [Tooltip("몬스터 추적이 시작되는 거리와 맞춥니다. 이 거리 밖에서는 Idle만 재생합니다.")]
    [SerializeField] private float chaseRange = 5f;

    [Header("Movement")]
    [SerializeField] private float movingThreshold = 0.01f;

    private enum LoopKind
    {
        None,
        Idle,
        Run
    }

    private AudioSource loopSource;
    private AudioSource sfxSource;
    private Rigidbody2D rb;
    private Transform player;
    private LoopKind currentLoop = LoopKind.None;
    private bool purificationPlaying;
    private Coroutine purificationRoutine;
    private bool gameManagerSubscribed;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        ConfigureAudioSources();
    }

    private void ConfigureAudioSources()
    {
        AudioSource[] sources = GetComponents<AudioSource>();
        loopSource = sources[0];
        loopSource.playOnAwake = false;
        loopSource.loop = true;
        loopSource.spatialBlend = 0f;
        loopSource.volume = 1f;

        if (sources.Length > 1)
        {
            sfxSource = sources[1];
        }
        else
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
        }

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

    private void Update()
    {
        if (!gameManagerSubscribed)
            TrySubscribeGameManager();

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

        if (idleClip != null)
        {
            bool isChasingPlayer = IsPlayerWithinChaseRange() &&
                rb != null &&
                rb.linearVelocity.sqrMagnitude > movingThreshold * movingThreshold;

            if (isChasingPlayer && runClip != null)
                SetLoop(LoopKind.Run, runClip);
            else
                SetLoop(LoopKind.Idle, idleClip);

            return;
        }

        StopLoop();
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

    private bool IsPlayerWithinChaseRange()
    {
        if (player == null)
            return false;

        float distance = Vector2.Distance(transform.position, player.position);
        return distance <= chaseRange;
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
            SetLoop(LoopKind.Idle, idleClip);
        else
            StopLoop();
    }

    private void SetLoop(LoopKind kind, AudioClip clip)
    {
        if (purificationPlaying)
            return;

        if (currentLoop == kind && loopSource.isPlaying && loopSource.clip == clip)
            return;

        currentLoop = kind;
        loopSource.mute = false;
        loopSource.loop = true;
        loopSource.clip = clip;
        loopSource.Play();

        Debug.Log(
            $"[MonsterFieldSoundController] {gameObject.name} 루프 재생 — {kind}, " +
            $"clip={GetClipName(clip)}, battle={IsBattleActive()}");
    }

    private void StopLoop()
    {
        bool wasPlaying = loopSource != null && loopSource.isPlaying;
        LoopKind previousLoop = currentLoop;
        currentLoop = LoopKind.None;

        if (loopSource == null)
            return;

        loopSource.mute = false;
        if (loopSource.isPlaying)
            loopSource.Stop();

        if (wasPlaying)
        {
            Debug.Log(
                $"[MonsterFieldSoundController] {gameObject.name} 루프 정지 — " +
                $"previous={previousLoop}, battle={IsBattleActive()}");
        }
    }

    private void SuppressLoopForPurification()
    {
        bool wasPlaying = loopSource != null && loopSource.isPlaying;
        LoopKind previousLoop = currentLoop;
        currentLoop = LoopKind.None;

        if (loopSource == null)
            return;

        loopSource.mute = true;
        if (loopSource.isPlaying)
            loopSource.Stop();

        Debug.Log(
            $"[MonsterFieldSoundController] {gameObject.name} 정화 연출용 Idle 억제 — " +
            $"previous={previousLoop}, wasPlaying={wasPlaying}");
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

    public IEnumerator PlayBattleAttackSoundRoutine()
    {
        if (!IsBattleActive() || attackClip == null || sfxSource == null)
            yield break;

        bool resumeIdleAfterAttack = currentLoop != LoopKind.None;

        if (loopSource != null && loopSource.isPlaying)
            loopSource.mute = true;

        if (sfxSource.isPlaying)
            sfxSource.Stop();

        sfxSource.loop = false;
        sfxSource.clip = attackClip;
        sfxSource.pitch = 1f;
        sfxSource.volume = 1f;
        sfxSource.time = 0f;
        sfxSource.Play();

        Debug.Log(
            $"[MonsterFieldSoundController] {gameObject.name} 공격 사운드 재생 — " +
            $"clip={GetClipName(attackClip)}");

        yield return new WaitForSecondsRealtime(attackClip.length);

        if (sfxSource.isPlaying)
            sfxSource.Stop();

        if (loopSource != null)
            loopSource.mute = false;

        if (!resumeIdleAfterAttack || !IsBattleActive() || !IsActiveBattleMonster())
            yield break;

        UpdateBattleLoopSound();
    }

    public void PlayBattlePurifySound(float hitAnimationDuration)
    {
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
        sfxSource.volume = 1f;
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

        if (loopSource != null)
            loopSource.mute = false;

        Debug.Log($"[MonsterFieldSoundController] {gameObject.name} Idle 루프 재개");
        UpdateBattleLoopSound();
    }

    private void CancelPurificationPlayback()
    {
        if (purificationRoutine != null)
        {
            StopCoroutine(purificationRoutine);
            purificationRoutine = null;
        }

        purificationPlaying = false;

        if (loopSource != null)
            loopSource.mute = false;

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
}
