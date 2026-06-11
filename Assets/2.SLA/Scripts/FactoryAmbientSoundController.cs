using System.Collections;
using UnityEngine;

/// <summary>
/// 공장 내부 배경 루프 사운드를 재생합니다.
/// 폐공장 분위기음을 메인 배경으로 깔고, 기계음은 낮은 볼륨으로 레이어링합니다.
/// </summary>
[DisallowMultipleComponent]
public class FactoryAmbientSoundController : MonoBehaviour
{
    [Header("Loop Clips")]
    [SerializeField] private AudioClip machineLoopClip;
    [SerializeField] private AudioClip abandonedFactoryLoopClip;

    [Header("Mix")]
    [SerializeField] [Range(0f, 1f)] private float machineVolume = 0.28f;
    [SerializeField] [Range(0f, 1f)] private float abandonedVolume = 0.85f;
    [SerializeField] private bool pauseDuringBattle = true;

    [Header("Pipe Ambience")]
    [SerializeField] private AudioClip pipeAmbienceClip;
    [SerializeField] [Range(0f, 1f)] private float pipeVolume = 0.22f;
    [SerializeField] private float pipeIntervalMinSeconds = 18f;
    [SerializeField] private float pipeIntervalMaxSeconds = 38f;
    [SerializeField] private float pipeFirstPlayDelaySeconds = 6f;

    [Header("Water Drop Ambience")]
    [SerializeField] private bool enableWaterDropAmbience = true;
    [SerializeField] private AudioClip waterDropAmbienceClip;
    [SerializeField] [Range(0f, 1f)] private float waterDropVolume = 0.35f;
    [SerializeField] private float waterDropIntervalMinSeconds = 8f;
    [SerializeField] private float waterDropIntervalMaxSeconds = 16f;
    [SerializeField] private float waterDropFirstPlayDelaySeconds = 3f;

    [Header("Water Drop Double Play")]
    [SerializeField] [Range(0f, 1f)] private float doublePlayChance = 0.35f;
    [SerializeField] private float doublePlayGapMinSeconds = 0.15f;
    [SerializeField] private float doublePlayGapMaxSeconds = 0.45f;

    private AudioSource machineSource;
    private AudioSource abandonedSource;
    private AudioSource pipeSource;
    private AudioSource waterDropSource;
    private Coroutine pipeRoutine;
    private Coroutine waterDropRoutine;
    private bool gameManagerSubscribed;
    private bool ambienceStoppedForBattle;
    private bool ambienceStoppedForGameplayBlock;

    private void Awake()
    {
        ConfigureAudioSources();
    }

    private void OnEnable()
    {
        TrySubscribeGameManager();

        if (!GameplayAudioGuard.IsBlocked)
            ambienceStoppedForGameplayBlock = false;

        if (ShouldPlayAmbience())
            StartAllAmbience();
        else
            StopAmbienceForBattle();
    }

    private void OnDisable()
    {
        UnsubscribeGameManager();
        StopAllAmbience();
        ambienceStoppedForBattle = false;
    }

    private void LateUpdate()
    {
        if (!gameManagerSubscribed)
            TrySubscribeGameManager();

        if (GameplayAudioGuard.IsBlocked)
        {
            if (!ambienceStoppedForGameplayBlock)
                StopForGameplayAudioBlock();
            return;
        }

        if (!pauseDuringBattle)
            return;

        if (IsBattleActive())
            StopAmbienceForBattle();
        else if (ambienceStoppedForBattle)
            ResumeAmbienceAfterBattle();
    }

    public void StopForGameplayAudioBlock()
    {
        ambienceStoppedForGameplayBlock = true;
        StopAllAmbience();
    }

    private void ConfigureAudioSources()
    {
        machineSource = gameObject.AddComponent<AudioSource>();
        machineSource.playOnAwake = false;
        machineSource.loop = true;
        machineSource.spatialBlend = 0f;
        machineSource.volume = machineVolume;

        abandonedSource = gameObject.AddComponent<AudioSource>();
        abandonedSource.playOnAwake = false;
        abandonedSource.loop = true;
        abandonedSource.spatialBlend = 0f;
        abandonedSource.volume = abandonedVolume;

        pipeSource = gameObject.AddComponent<AudioSource>();
        pipeSource.playOnAwake = false;
        pipeSource.loop = false;
        pipeSource.spatialBlend = 0f;
        pipeSource.volume = pipeVolume;

        waterDropSource = gameObject.AddComponent<AudioSource>();
        waterDropSource.playOnAwake = false;
        waterDropSource.loop = false;
        waterDropSource.spatialBlend = 0f;
        waterDropSource.volume = waterDropVolume;
    }

    private void StartAllAmbience()
    {
        StartAmbientLoops();
        StartPipeAmbience();
        StartWaterDropAmbience();
    }

    private void StopAllAmbience()
    {
        StopAmbientLoops();
        StopPipeAmbience();
        StopWaterDropAmbience();
    }

    private void StartAmbientLoops()
    {
        PlayLoop(machineSource, machineLoopClip, machineVolume);
        PlayLoop(abandonedSource, abandonedFactoryLoopClip, abandonedVolume);
    }

    private void StopAmbientLoops()
    {
        StopLoop(machineSource);
        StopLoop(abandonedSource);
    }

    private bool ShouldPlayAmbience()
    {
        if (GameplayAudioGuard.IsBlocked || ambienceStoppedForGameplayBlock)
            return false;

        return !pauseDuringBattle || !IsBattleActive();
    }

    private static bool IsBattleActive()
    {
        return GameManager.Instance != null && GameManager.Instance.IsInBattle;
    }

    private static void PlayLoop(AudioSource source, AudioClip clip, float volume)
    {
        if (source == null || clip == null)
            return;

        source.clip = clip;
        source.volume = volume;
        source.loop = true;

        if (!source.isPlaying)
            source.Play();
        else
            source.UnPause();
    }

    private static void StopLoop(AudioSource source)
    {
        if (source != null && source.isPlaying)
            source.Stop();
    }

    private void StartPipeAmbience()
    {
        if (pipeAmbienceClip == null)
            return;

        StopPipeAmbience();
        pipeRoutine = StartCoroutine(PlayPipeAmbienceRoutine());
    }

    private void StopPipeAmbience()
    {
        if (pipeRoutine != null)
        {
            StopCoroutine(pipeRoutine);
            pipeRoutine = null;
        }

        if (pipeSource != null && pipeSource.isPlaying)
            pipeSource.Stop();
    }

    private IEnumerator PlayPipeAmbienceRoutine()
    {
        float minInterval = Mathf.Max(0.5f, pipeIntervalMinSeconds);
        float maxInterval = Mathf.Max(minInterval, pipeIntervalMaxSeconds);

        if (pipeFirstPlayDelaySeconds > 0f)
            yield return new WaitForSeconds(pipeFirstPlayDelaySeconds);

        while (enabled)
        {
            PlayPipeAmbienceOnce();

            float waitSeconds = Random.Range(minInterval, maxInterval);
            yield return new WaitForSeconds(waitSeconds);
        }
    }

    private void PlayPipeAmbienceOnce()
    {
        if (pipeAmbienceClip == null || pipeSource == null)
            return;

        pipeSource.volume = pipeVolume;
        pipeSource.PlayOneShot(pipeAmbienceClip);
    }

    private void StartWaterDropAmbience()
    {
        if (!enableWaterDropAmbience || waterDropAmbienceClip == null)
            return;

        StopWaterDropAmbience();
        waterDropRoutine = StartCoroutine(PlayWaterDropAmbienceRoutine());
    }

    private void StopWaterDropAmbience()
    {
        if (waterDropRoutine != null)
        {
            StopCoroutine(waterDropRoutine);
            waterDropRoutine = null;
        }

        if (waterDropSource != null && waterDropSource.isPlaying)
            waterDropSource.Stop();
    }

    private IEnumerator PlayWaterDropAmbienceRoutine()
    {
        float minInterval = Mathf.Max(0.5f, waterDropIntervalMinSeconds);
        float maxInterval = Mathf.Max(minInterval, waterDropIntervalMaxSeconds);

        if (waterDropFirstPlayDelaySeconds > 0f)
            yield return new WaitForSeconds(waterDropFirstPlayDelaySeconds);

        while (enabled)
        {
            yield return PlayWaterDropAmbienceOnce();

            float waitSeconds = Random.Range(minInterval, maxInterval);
            yield return new WaitForSeconds(waitSeconds);
        }
    }

    private IEnumerator PlayWaterDropAmbienceOnce()
    {
        if (waterDropAmbienceClip == null || waterDropSource == null)
            yield break;

        waterDropSource.volume = waterDropVolume;
        waterDropSource.PlayOneShot(waterDropAmbienceClip);

        if (Random.value > doublePlayChance)
            yield break;

        float gapMin = Mathf.Max(0.05f, doublePlayGapMinSeconds);
        float gapMax = Mathf.Max(gapMin, doublePlayGapMaxSeconds);
        yield return new WaitForSeconds(Random.Range(gapMin, gapMax));

        waterDropSource.volume = waterDropVolume;
        waterDropSource.PlayOneShot(waterDropAmbienceClip);
    }

    private void StopAmbienceForBattle()
    {
        if (ambienceStoppedForBattle)
            return;

        ambienceStoppedForBattle = true;
        StopAllAmbience();
    }

    private void ResumeAmbienceAfterBattle()
    {
        if (!ambienceStoppedForBattle || !isActiveAndEnabled)
            return;

        ambienceStoppedForBattle = false;
        StartAllAmbience();
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
        if (!pauseDuringBattle)
            return;

        StopAmbienceForBattle();
    }

    private void HandleBattleEnded()
    {
        if (!pauseDuringBattle)
            return;

        ResumeAmbienceAfterBattle();
    }
}
