using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// FactoryMap_*_Grid에 붙여 사용합니다.
/// 플레이어가 그리드 벽 콜라이더 근처에 오면 확률적으로 파이프 연기 사운드를 재생합니다.
/// </summary>
[DisallowMultipleComponent]
public class FactoryPipeSmokeSoundZone : MonoBehaviour
{
    [Header("Clips")]
    [SerializeField] private bool enablePipeSmokeSound = true;
    [SerializeField] private AudioClip pipeSmokeClip1;
    [SerializeField] private AudioClip pipeSmokeClip2;

    [Header("Proximity")]
    [Tooltip("벽 콜라이더와 이 거리 안이면 근접으로 판정합니다.")]
    [SerializeField] private float proximityRadius = 2.5f;

    [Header("Playback")]
    [SerializeField] [Range(0f, 1f)] private float volume = 0.22f;
    [SerializeField] [Range(0f, 1f)] private float playChance = 0.35f;
    [SerializeField] private float checkIntervalMinSeconds = 2f;
    [SerializeField] private float checkIntervalMaxSeconds = 4.5f;
    [SerializeField] private float cooldownAfterPlaySeconds = 5f;

    private Collider2D wallCollider;
    private Transform player;
    private AudioSource sfxSource;
    private Coroutine checkRoutine;
    private float cooldownUntil;
    private bool gameManagerSubscribed;

    private void Awake()
    {
        TryResolveWallCollider();

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        sfxSource.loop = false;
        sfxSource.spatialBlend = 0f;
        sfxSource.volume = volume;
    }

    private void OnEnable()
    {
        TrySubscribeGameManager();
    }

    private void OnDisable()
    {
        UnsubscribeGameManager();
        StopChecking();
    }

    private void LateUpdate()
    {
        if (!gameManagerSubscribed)
            TrySubscribeGameManager();

        if (wallCollider == null)
            TryResolveWallCollider();

        if (ShouldCheckProximity())
            StartChecking();
        else
            StopChecking();
    }

    private bool ShouldCheckProximity()
    {
        return enablePipeSmokeSound &&
               GameplayAudioGuard.CanPlayFieldCharacterSounds &&
               !IsBattleActive() &&
               IsPlayerNearWalls();
    }

    public void StopForInventoryPause()
    {
        StopChecking();

        if (sfxSource != null && sfxSource.isPlaying)
            sfxSource.Stop();
    }

    private void TryResolveWallCollider()
    {
        if (wallCollider != null)
            return;

        CompositeCollider2D composite = GetComponentInChildren<CompositeCollider2D>(true);
        if (composite != null)
        {
            wallCollider = composite;
            return;
        }

        TilemapCollider2D tilemapCollider = GetComponentInChildren<TilemapCollider2D>(true);
        if (tilemapCollider != null)
            wallCollider = tilemapCollider;
    }

    private void TryFindPlayer()
    {
        if (player != null)
            return;

        PlayerOxygen runtimeOxygen = PlayerOxygen.ResolveRuntime();
        if (runtimeOxygen != null)
        {
            player = runtimeOxygen.transform;
            return;
        }

        try
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
                player = playerObject.transform;
        }
        catch (UnityException)
        {
        }
    }

    private bool IsPlayerNearWalls()
    {
        TryFindPlayer();

        if (player == null || wallCollider == null)
            return false;

        Vector2 playerPosition = player.position;
        Vector2 closestPoint = wallCollider.ClosestPoint(playerPosition);
        return Vector2.Distance(playerPosition, closestPoint) <= proximityRadius;
    }

    private void StartChecking()
    {
        if (checkRoutine != null)
            return;

        checkRoutine = StartCoroutine(CheckAndMaybePlayRoutine());
    }

    private void StopChecking()
    {
        if (checkRoutine != null)
        {
            StopCoroutine(checkRoutine);
            checkRoutine = null;
        }

        if (sfxSource != null && sfxSource.isPlaying)
            sfxSource.Stop();
    }

    private IEnumerator CheckAndMaybePlayRoutine()
    {
        while (enabled && ShouldCheckProximity())
        {
            if (Time.time >= cooldownUntil && Random.value <= playChance)
                PlayRandomSmokeClip();

            float minInterval = Mathf.Max(0.5f, checkIntervalMinSeconds);
            float maxInterval = Mathf.Max(minInterval, checkIntervalMaxSeconds);
            yield return new WaitForSeconds(Random.Range(minInterval, maxInterval));
        }

        checkRoutine = null;
    }

    private void PlayRandomSmokeClip()
    {
        if (!GameplayAudioGuard.CanPlay)
            return;

        AudioClip clip = ResolveRandomClip();
        if (clip == null || sfxSource == null)
            return;

        sfxSource.volume = volume;
        sfxSource.PlayOneShot(clip);
        cooldownUntil = Time.time + Mathf.Max(0.5f, cooldownAfterPlaySeconds);
    }

    private AudioClip ResolveRandomClip()
    {
        bool hasClip1 = pipeSmokeClip1 != null;
        bool hasClip2 = pipeSmokeClip2 != null;

        if (hasClip1 && hasClip2)
            return Random.value < 0.5f ? pipeSmokeClip1 : pipeSmokeClip2;

        if (hasClip1)
            return pipeSmokeClip1;

        return pipeSmokeClip2;
    }

    private static bool IsBattleActive()
    {
        return GameManager.Instance != null && GameManager.Instance.IsInBattle;
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
        StopChecking();
    }

    private void HandleBattleEnded()
    {
        if (!isActiveAndEnabled)
            return;

        if (ShouldCheckProximity())
            StartChecking();
    }
}
