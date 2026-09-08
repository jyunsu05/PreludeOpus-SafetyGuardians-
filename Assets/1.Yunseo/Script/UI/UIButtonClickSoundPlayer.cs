using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UIButtonClickSoundPlayer : MonoBehaviour
{
    public static UIButtonClickSoundPlayer Instance { get; private set; }

    [SerializeField] private AudioClip clickClip;
    [Tooltip("현재 씬 BGM(공장/배틀) 대비 클릭음 비율입니다.")]
    [SerializeField] [Range(0f, 1f)] private float clickVolume = 0.55f;
    private const float BattleClickSfxBgmRatio = 0.45f;
    [SerializeField] private AudioClip factoryItemAcquireClip;
    [SerializeField] private AudioClip battleItemPopupClip;

    public AudioClip ClickClip => clickClip;
    public float ClickVolume => clickVolume;

    private AudioSource audioSource;
    private AudioSource trackedAudioSource;
    private Coroutine trackedClipRoutine;
    private readonly HashSet<int> registeredButtonIds = new HashSet<int>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        EnsureAudioSources();
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void Start()
    {
        RegisterAllButtonsInLoadedScenes();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            Instance = null;
        }
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        GameplayAudioGuard.Unblock();
        RegisterAllButtonsInLoadedScenes();
    }

    private void EnsureAudioSources()
    {
        audioSource ??= FindOrCreateUiAudioSource("UiOneShotAudio");
        trackedAudioSource ??= FindOrCreateUiAudioSource("UiTrackedAudio");
    }

    private AudioSource FindOrCreateUiAudioSource(string childName)
    {
        Transform child = transform.Find(childName);
        if (child != null && child.TryGetComponent(out AudioSource existing))
            return existing;

        GameObject host = new GameObject(childName);
        host.transform.SetParent(transform, false);
        AudioSource source = host.AddComponent<AudioSource>();
        ConfigureUiAudioSource(source);
        return source;
    }

    private IEnumerator PlayOneShotClipWhenReady(AudioClip clip, float volume, bool allowWhenBlocked)
    {
        yield return AudioClipLoadUtility.WaitUntilLoaded(clip);
        if (!AudioClipLoadUtility.IsReadyToPlay(clip))
            yield break;

        if (!allowWhenBlocked && !GameplayAudioGuard.CanPlay)
            yield break;

        EnsureAudioSources();
        if (audioSource == null)
            yield break;

        audioSource.PlayOneShot(clip, Mathf.Max(0f, volume));
    }

    private IEnumerator PlayTrackedClipWhenReady(AudioClip clip, float volume, bool loop)
    {
        yield return AudioClipLoadUtility.WaitUntilLoaded(clip);
        if (!AudioClipLoadUtility.IsReadyToPlay(clip) || !GameplayAudioGuard.CanPlay)
            yield break;

        EnsureAudioSources();
        if (trackedAudioSource == null)
            yield break;

        StopTrackedClip();
        trackedAudioSource.clip = clip;
        trackedAudioSource.loop = loop;
        trackedAudioSource.volume = Mathf.Max(0f, volume);
        trackedAudioSource.Play();
    }

    private static void ConfigureUiAudioSource(AudioSource source)
    {
        if (source == null)
            return;

        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.volume = 1f;
    }

    public void ForceStopAll()
    {
        StopTrackedClip();

        if (audioSource != null && audioSource.isPlaying)
            audioSource.Stop();
    }

    public void PlayOneShotClip(AudioClip clip, float volume = 1f, bool allowWhenBlocked = false)
    {
        if (clip == null || (!allowWhenBlocked && !GameplayAudioGuard.CanPlay))
            return;

        StartCoroutine(PlayOneShotClipWhenReady(clip, volume, allowWhenBlocked));
    }

    public void PlayTrackedClip(AudioClip clip, float volume = 1f, bool loop = false)
    {
        if (clip == null || !GameplayAudioGuard.CanPlay)
            return;

        StartCoroutine(PlayTrackedClipWhenReady(clip, volume, loop));
    }

    public void PlayTrackedClipForDuration(AudioClip clip, float duration, float volume = 1f)
    {
        if (clip == null || !GameplayAudioGuard.CanPlay)
            return;

        StopTrackedClip();
        trackedClipRoutine = StartCoroutine(PlayTrackedClipForDurationRoutine(clip, duration, volume));
    }

    public void StopTrackedClip()
    {
        if (trackedClipRoutine != null)
        {
            StopCoroutine(trackedClipRoutine);
            trackedClipRoutine = null;
        }

        if (trackedAudioSource != null)
        {
            if (trackedAudioSource.isPlaying)
                trackedAudioSource.Stop();

            trackedAudioSource.loop = false;
        }
    }

    private IEnumerator PlayTrackedClipForDurationRoutine(AudioClip clip, float duration, float volume)
    {
        PlayTrackedClip(clip, volume);
        yield return new WaitForSecondsRealtime(Mathf.Max(0.01f, duration));

        if (trackedAudioSource != null &&
            trackedAudioSource.isPlaying &&
            trackedAudioSource.clip == clip)
        {
            trackedAudioSource.Stop();
        }

        trackedClipRoutine = null;
    }

    public void RegisterAllButtonsInLoadedScenes()
    {
        Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < buttons.Length; i++)
            RegisterButton(buttons[i]);

        if (UIManager.Instance != null)
            RegisterButtonsInHierarchy(UIManager.Instance.transform);
    }

    public void RegisterButtonsInHierarchy(Transform root)
    {
        if (root == null)
            return;

        Button[] buttons = root.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
            RegisterButton(buttons[i]);
    }

    public void RegisterButton(Button button)
    {
        if (button == null || clickClip == null || ShouldSkipAutoRegistration(button))
            return;

        int id = button.GetInstanceID();
        if (registeredButtonIds.Contains(id))
            return;

        registeredButtonIds.Add(id);
        button.onClick.AddListener(PlayClickSound);
    }

    public void PlayClickSound()
    {
        // 게임오버 등으로 GameplayAudioGuard가 막혀 있어도 UI 버튼 클릭음은 재생합니다.
        PlayOneShotClip(clickClip, ResolveActiveClickVolume(), allowWhenBlocked: GameplayAudioGuard.IsBlocked);
    }

    public void PlayClickSound(bool allowWhenBlocked)
    {
        PlayOneShotClip(clickClip, ResolveActiveClickVolume(), allowWhenBlocked: allowWhenBlocked);
    }

    private float ResolveActiveClickVolume()
    {
        if (IsAcquisitionPopupVisible())
            return ResolveAcquisitionPopupClickVolume();

        if (GameManager.Instance != null && GameManager.Instance.IsBattleSceneUiOpen)
            return GameManager.Instance.GetBattleSfxVolume(BattleClickSfxBgmRatio);

        if (GameManager.Instance != null)
            return GameManager.Instance.GetFactorySfxVolume(clickVolume);

        return Mathf.Clamp01(0.5f * clickVolume);
    }

    private float ResolveAcquisitionPopupClickVolume()
    {
        if (GameManager.Instance != null)
            return GameManager.Instance.GetBattleSfxVolume(0.62f);

        return Mathf.Clamp01(0.5f * 0.62f);
    }

    private static bool IsAcquisitionPopupVisible()
    {
        UIAcquisitionPopup[] popups =
            Object.FindObjectsByType<UIAcquisitionPopup>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        for (int i = 0; i < popups.Length; i++)
        {
            if (popups[i] != null && popups[i].isActiveAndEnabled)
                return true;
        }

        return false;
    }

    public static float ResolveClickVolume(float volumeOverride = -1f)
    {
        if (volumeOverride >= 0f)
            return Mathf.Clamp01(volumeOverride);

        return Instance != null ? Instance.clickVolume : 1f;
    }

    /// <summary>
    /// 씬 전환 직후에도 들리도록 DontDestroyOnLoad 오브젝트에서 원샷을 재생합니다.
    /// volumeOverride를 지정하지 않으면 UIButtonClickSoundPlayer의 clickVolume을 사용합니다.
    /// </summary>
    public static void PlaySurvivingOneShot(AudioClip clip, float volumeOverride = -1f)
    {
        if (clip == null)
            return;

        if (Instance != null)
        {
            Instance.StartCoroutine(Instance.PlaySurvivingOneShotRoutine(clip, volumeOverride));
            return;
        }

        GameObject host = new GameObject("UiClickOneShot");
        DontDestroyOnLoad(host);
        host.AddComponent<SurvivingOneShotAudioPlayer>().Play(clip, ResolveClickVolume(volumeOverride));
    }

    private IEnumerator PlaySurvivingOneShotRoutine(AudioClip clip, float volumeOverride)
    {
        yield return AudioClipLoadUtility.WaitUntilLoaded(clip);
        if (!AudioClipLoadUtility.IsReadyToPlay(clip))
            yield break;

        float volume = ResolveClickVolume(volumeOverride);

        GameObject host = new GameObject("UiClickOneShot");
        DontDestroyOnLoad(host);

        AudioSource source = host.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.volume = 1f;
        source.PlayOneShot(clip, volume);

        Object.Destroy(host, clip.length + 0.25f);
    }

    private sealed class SurvivingOneShotAudioPlayer : MonoBehaviour
    {
        public void Play(AudioClip clip, float volume)
        {
            StartCoroutine(PlayRoutine(clip, volume));
        }

        private IEnumerator PlayRoutine(AudioClip clip, float volume)
        {
            yield return AudioClipLoadUtility.WaitUntilLoaded(clip);
            if (!AudioClipLoadUtility.IsReadyToPlay(clip))
            {
                Destroy(gameObject);
                yield break;
            }

            AudioSource source = GetComponent<AudioSource>();
            if (source == null)
                source = gameObject.AddComponent<AudioSource>();

            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.volume = 1f;
            source.PlayOneShot(clip, volume);

            Destroy(gameObject, clip.length + 0.25f);
        }
    }

    private static void EnsureClipLoaded(AudioClip clip)
    {
        if (clip == null)
            return;

        if (!clip.preloadAudioData && clip.loadState == AudioDataLoadState.Unloaded)
            clip.LoadAudioData();
    }

    static bool ShouldSkipAutoRegistration(Button button)
    {
        if (button.GetComponentInParent<OpeningStarWarsCrawl>(true) != null)
            return true;

        if (button.GetComponentInParent<UILoading>(true) != null)
            return true;

        // UIGameStartScreen이 직접 클릭음을 재생합니다.
        return button.GetComponentInParent<UIGameStartScreen>(true) != null;
    }

    public void PlayFactoryItemAcquireSound()
    {
        float volume = GameManager.Instance != null
            ? GameManager.Instance.GetFactorySfxVolume(0.55f)
            : Mathf.Clamp01(0.5f * 0.55f);
        PlayOneShotClip(factoryItemAcquireClip, volume);
    }

    public void PlayBattleItemPopupSound()
    {
        float volume = GameManager.Instance != null
            ? GameManager.Instance.GetBattleSfxVolume(0.55f)
            : Mathf.Clamp01(0.5f * 0.55f);
        PlayOneShotClip(battleItemPopupClip, volume, allowWhenBlocked: true);
    }

    public void PlayAcquisitionPopupClickSound()
    {
        PlayOneShotClip(clickClip, ResolveAcquisitionPopupClickVolume(), allowWhenBlocked: true);
    }
}
