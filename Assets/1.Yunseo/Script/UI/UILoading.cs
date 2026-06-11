using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class UILoading : MonoBehaviour
{
    private static UILoading instance;

    [SerializeField] private GameObject loadingPanel; // 로딩 패널 오브젝트
    [SerializeField] private Slider loadingProgressBar; // 로딩 진행 바
    [SerializeField] private TextMeshProUGUI loadingProgressText; // 로딩 진행 텍스트
    [SerializeField] private TextMeshProUGUI loadingMessageText; // 로딩 상태 문구 텍스트
    [SerializeField] private float minDisplayTime = 2f; // 최소 로딩 화면 표시 시간
    [SerializeField] private TextMeshProUGUI InformationText;

    [Header("--- 다음 공장 챕터 ---")]
    [SerializeField] private ChapterManager chapterManager;
    [SerializeField] private FactoryChapterController factoryChapterController;

    [Header("--- Loading Sounds ---")]
    [SerializeField] private AudioClip loadingStartClip;
    [SerializeField] private AudioClip loadingCompleteClip;
    [SerializeField] private AudioClip loadingButtonClickClip;

    private float panelShownTime;
    private bool waitForTouchDismiss;
    private bool isSceneLoading;
    private Coroutine autoProgressRoutine;
    private Coroutine loadingSoundRoutine;
    private Coroutine advanceAfterClickRoutine;
    private AudioSource loadingSequenceAudioSource;
    private readonly HashSet<int> registeredLoadingButtonIds = new HashSet<int>();

    public static bool IsLoadingScreenVisible
    {
        get
        {
            UILoading loading = ResolveInstance();
            return loading != null && loading.IsPanelVisible;
        }
    }

    public bool IsPanelVisible => loadingPanel != null && loadingPanel.activeInHierarchy;

    void Awake()
    {
        instance = this;

        if (loadingPanel != null)
            loadingPanel.SetActive(false);

        if (InformationText != null)
            InformationText.gameObject.SetActive(false);

        ConfigureProgressBar();
        SetProgress(0f);
        SetLoadingText("로딩중");
    }

    void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private static UILoading ResolveInstance()
    {
        if (instance != null)
            return instance;

        instance = FindAnyObjectByType<UILoading>(FindObjectsInactive.Include);
        return instance;
    }

    private void ConfigureProgressBar()
    {
        if (loadingProgressBar == null)
            return;

        loadingProgressBar.interactable = false;

        Navigation navigation = loadingProgressBar.navigation;
        navigation.mode = Navigation.Mode.None;
        loadingProgressBar.navigation = navigation;
    }

    void Update()
    {
        SyncProgressTextFromSlider();

        if (isSceneLoading || advanceAfterClickRoutine != null ||
            loadingPanel == null || !loadingPanel.activeSelf || !CanDismissByInput())
            return;

        if (WasLoadingInputPressed())
            BeginAdvanceToNextFactoryChapter();
    }

    private bool CanDismissByInput()
    {
        if (loadingProgressBar != null)
            return loadingProgressBar.value >= loadingProgressBar.maxValue - 0.0001f;

        return waitForTouchDismiss;
    }

    public void ShowLoading(string message = "로딩중", float initialProgress = 0f)
    {
        panelShownTime = Time.time;
        waitForTouchDismiss = false;
        if (InformationText != null)
            InformationText.gameObject.SetActive(false);

        gameObject.SetActive(true);

        if (loadingPanel != null)
            loadingPanel.SetActive(true);

        ConfigureProgressBar();
        StopLoadingSounds();
        PlayLoadingSounds();
        RegisterLoadingButtons();
        SetProgress(initialProgress);
        SetLoadingText(message);
    }

    public void ShowLoadingWithAutoProgress(string message = "로딩중", float duration = 2f)
    {
        ShowLoading(message, 0f);

        if (autoProgressRoutine != null)
            StopCoroutine(autoProgressRoutine);

        autoProgressRoutine = StartCoroutine(AutoProgressRoutine(duration));
    }

    public void SetProgress(float normalizedProgress)
    {
        float clamped = Mathf.Clamp01(normalizedProgress);
        if (loadingProgressBar != null)
        {
            float sliderValue = Mathf.Lerp(loadingProgressBar.minValue, loadingProgressBar.maxValue, clamped);
            loadingProgressBar.value = sliderValue;
            waitForTouchDismiss = sliderValue >= loadingProgressBar.maxValue;
        }
        else
        {
            waitForTouchDismiss = clamped >= 1f;
        }

        SyncProgressTextFromSlider();
    }

    private void SyncProgressTextFromSlider()
    {
        if (loadingProgressText == null || loadingProgressBar == null)
            return;

        float normalized = Mathf.InverseLerp(loadingProgressBar.minValue, loadingProgressBar.maxValue, loadingProgressBar.value);
        loadingProgressText.text = $"{normalized * 100f:0.00}%";
        UpdateInformationTextVisibility(normalized);
    }

    private void UpdateInformationTextVisibility(float normalizedProgress)
    {
        if (InformationText == null)
            return;

        bool shouldShow = normalizedProgress >= 0.9999f;
        if (InformationText.gameObject.activeSelf != shouldShow)
            InformationText.gameObject.SetActive(shouldShow);
    }

    public void SetLoadingText(string message)
    {
        if (loadingMessageText != null && !string.IsNullOrEmpty(message))
            loadingMessageText.text = message;
    }

    public void SetProgressWithText(float normalizedProgress, string message)
    {
        SetProgress(normalizedProgress);
        SetLoadingText(message);
    }

    public void HideLoading()
    {
        waitForTouchDismiss = false;
        isSceneLoading = false;

        if (autoProgressRoutine != null)
        {
            StopCoroutine(autoProgressRoutine);
            autoProgressRoutine = null;
        }

        if (advanceAfterClickRoutine != null)
        {
            StopCoroutine(advanceAfterClickRoutine);
            advanceAfterClickRoutine = null;
        }

        StopLoadingSounds();

        if (loadingPanel != null)
            loadingPanel.SetActive(false);
    }

    private IEnumerator AutoProgressRoutine(float duration)
    {
        if (duration <= 0f)
        {
            SetProgress(1f);
            autoProgressRoutine = null;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            SetProgress(elapsed / duration);
            yield return null;
        }

        SetProgress(1f);
        autoProgressRoutine = null;
    }

    static bool WasLoadingInputPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Touchscreen.current != null &&
            Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            return true;

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            return true;

        return false;
#else
        if (Input.GetMouseButtonDown(0))
            return true;

        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
            return true;

        return false;
#endif
    }

    private void BeginAdvanceToNextFactoryChapter()
    {
        if (isSceneLoading || advanceAfterClickRoutine != null)
            return;

        PlayLoadingButtonClickSound();
        advanceAfterClickRoutine = StartCoroutine(AdvanceAfterClickSoundRoutine());
    }

    private IEnumerator AdvanceAfterClickSoundRoutine()
    {
        float delay = loadingButtonClickClip != null
            ? Mathf.Clamp(loadingButtonClickClip.length * 0.35f, 0.05f, 0.2f)
            : 0.05f;

        yield return new WaitForSecondsRealtime(delay);
        advanceAfterClickRoutine = null;
        AdvanceToNextFactoryChapter();
    }

    private void AdvanceToNextFactoryChapter()
    {
        if (isSceneLoading)
            return;

        isSceneLoading = true;

        float progressPercent = GetProgressPercent();

        ChapterManager manager = ResolveChapterManager();
        if (manager != null)
        {
            if (!manager.LoadNextChapter(out string chapterMessage))
            {
                SetLoadingText(chapterMessage);
                Debug.LogWarning($"[UILoading] {progressPercent:0.00}% — {chapterMessage}");
                isSceneLoading = false;
                return;
            }

            Debug.Log($"[UILoading] {progressPercent:0.00}% — {chapterMessage}");
            HideLoading();
            isSceneLoading = false;
            return;
        }

        FactoryChapterController controller = ResolveFactoryChapterController();
        if (controller == null)
        {
            Debug.LogError("[UILoading] ChapterManager 또는 FactoryChapterController를 찾을 수 없습니다.");
            isSceneLoading = false;
            return;
        }

        if (!controller.TryAdvanceToNextChapter(out string resultMessage))
        {
            SetLoadingText(resultMessage);
            Debug.LogWarning($"[UILoading] {progressPercent:0.00}% — {resultMessage}");
            isSceneLoading = false;
            return;
        }

        Debug.Log($"[UILoading] {progressPercent:0.00}% — {resultMessage}");
        HideLoading();
        isSceneLoading = false;
    }

    private ChapterManager ResolveChapterManager()
    {
        if (chapterManager != null)
            return chapterManager;

        return ChapterManager.EnsureInstance();
    }

    private FactoryChapterController ResolveFactoryChapterController()
    {
        if (factoryChapterController != null)
            return factoryChapterController;

        factoryChapterController = FactoryChapterController.EnsureInstance();
        return factoryChapterController;
    }

    private float GetProgressPercent()
    {
        if (loadingProgressBar == null)
            return waitForTouchDismiss ? 100f : 0f;

        float normalized = Mathf.InverseLerp(
            loadingProgressBar.minValue,
            loadingProgressBar.maxValue,
            loadingProgressBar.value);
        return normalized * 100f;
    }

    public void HideLoadingWithMinimumTime()
    {
        float elapsed = Time.time - panelShownTime;
        float remain = minDisplayTime - elapsed;

        if (remain <= 0f)
        {
            HideLoading();
            return;
        }

        StartCoroutine(HideAfterDelay(remain));
    }

    private IEnumerator HideAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        HideLoading();
    }

    private void EnsureLoadingSequenceAudioSource()
    {
        if (loadingSequenceAudioSource != null)
            return;

        AudioSource[] sources = GetComponents<AudioSource>();
        loadingSequenceAudioSource = sources.Length > 0 ? sources[0] : gameObject.AddComponent<AudioSource>();

        loadingSequenceAudioSource.playOnAwake = false;
        loadingSequenceAudioSource.loop = false;
        loadingSequenceAudioSource.spatialBlend = 0f;
        loadingSequenceAudioSource.volume = 1f;
    }

    private void PreloadClip(AudioClip clip)
    {
        if (clip == null || clip.preloadAudioData || clip.loadState != AudioDataLoadState.Unloaded)
            return;

        clip.LoadAudioData();
    }

    private void PlayLoadingSounds()
    {
        PreloadClip(loadingStartClip);
        PreloadClip(loadingCompleteClip);
        loadingSoundRoutine = StartCoroutine(PlayLoadingSoundSequenceRoutine());
    }

    private IEnumerator PlayLoadingSoundSequenceRoutine()
    {
        yield return PlayClipAndWait(loadingStartClip);
        yield return PlayClipAndWait(loadingCompleteClip);
        loadingSoundRoutine = null;
    }

    private IEnumerator PlayClipAndWait(AudioClip clip)
    {
        if (clip == null)
            yield break;

        EnsureLoadingSequenceAudioSource();
        loadingSequenceAudioSource.PlayOneShot(clip);
        yield return new WaitForSecondsRealtime(clip.length);
    }

    private void StopLoadingSounds()
    {
        if (loadingSoundRoutine != null)
        {
            StopCoroutine(loadingSoundRoutine);
            loadingSoundRoutine = null;
        }

        if (loadingSequenceAudioSource != null && loadingSequenceAudioSource.isPlaying)
            loadingSequenceAudioSource.Stop();
    }

    private void RegisterLoadingButtons()
    {
        if (loadingButtonClickClip == null)
            return;

        Button[] buttons = GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
            RegisterLoadingButton(buttons[i]);
    }

    private void RegisterLoadingButton(Button button)
    {
        if (button == null || loadingButtonClickClip == null)
            return;

        int id = button.GetInstanceID();
        if (registeredLoadingButtonIds.Contains(id))
            return;

        registeredLoadingButtonIds.Add(id);
        button.onClick.AddListener(PlayLoadingButtonClickSound);
    }

    private void PlayLoadingButtonClickSound()
    {
        if (loadingButtonClickClip == null)
            return;

        PreloadClip(loadingButtonClickClip);
        UIButtonClickSoundPlayer.PlaySurvivingOneShot(loadingButtonClickClip);
    }
}
