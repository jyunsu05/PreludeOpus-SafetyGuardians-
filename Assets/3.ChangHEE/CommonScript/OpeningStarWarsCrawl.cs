using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(RectTransform))]
public class OpeningStarWarsCrawl : MonoBehaviour
{
    enum CrawlPhase
    {
        Scrolling,
        WaitingForInput
    }

    [Header("Timing")]
    [SerializeField] float initialDelay = 0.8f;

    [Header("Press Key Timing")]
    [Tooltip("크롤 시작 후 몇 초에 '아무 키나'를 시작할지. 0이면 아래 자동(글자 사라짐) 모드.")]
    [SerializeField] float pressKeyAtSeconds = 0f;
    [Tooltip("스토리 글자가 화면에서 사라진 뒤, '아무 키나' 페이드 시작까지 추가 대기(초). pressKeyAtSeconds=0 일 때만 사용.")]
    [SerializeField] float pressKeyDelayAfterTextGone = 0f;
    [Tooltip("'아무 키나'가 서서히 나타나는 시간(초).")]
    [SerializeField] float pressKeyFadeInSeconds = 1.5f;
    [Tooltip("'아무 키나'가 완전히 보이는 시간(초).")]
    [SerializeField] float pressKeyHoldSeconds = 2.5f;
    [Tooltip("'아무 키나'가 서서히 사라지는 시간(초).")]
    [SerializeField] float pressKeyFadeOutSeconds = 1.5f;

    [Header("Timer Log (측정용)")]
    [SerializeField] bool logCrawlTimerToConsole = false;
    [SerializeField] float logIntervalSeconds = 0.5f;

    [Header("Text")]
    [SerializeField] TMP_FontAsset storyFont;
    [Range(28f, 84f)]
    [SerializeField] float fontSize = 72.3f;
    [SerializeField] FontWeight fontWeight = FontWeight.Bold;
    [SerializeField] Color textColor = new Color(1f, 0.82f, 0.12f, 1f);
    [SerializeField] float lineSpacing = 10f;
    [SerializeField] float textBoxWidth = 1200f;

    [Header("Credits Scroll")]
    [Tooltip("스토리 텍스트가 잘리는 영역. 비우면 자식 'Mask'를 찾거나 자동 생성합니다.")]
    [SerializeField] RectTransform creditsMask;
    [SerializeField] float crawlSpeed = 55f;
    [SerializeField] float startOffsetFromBottom = 40f;
    [Tooltip("마지막 줄이 화면 위로 사라진 뒤 추가 대기(px). 0에 가까울수록 빨리 '아무 키나'가 뜹니다.")]
    [SerializeField] float finishPadding = 0f;
    [SerializeField] bool fadeAtTop = true;
    [SerializeField] float topFadeRange = 220f;
    [Tooltip("크롤 종료 후 마우스 휠 스크롤 속도(px).")]
    [SerializeField] float manualScrollWheelSpeed = 80f;
    [Tooltip("크롤 종료 후 드래그 스크롤 감도. 1이면 손가락/마우스 이동량과 동일.")]
    [SerializeField] float manualScrollDragScale = 1f;

    [Header("Game Start Button")]
    public GameObject gameStartButton;

    [Header("Tutorial Panel")]
    [Tooltip("게임 시작 버튼 클릭 시 숨길 오프닝 UI 루트 오브젝트입니다.\n비워두면 gameStartButton만 숨깁니다.")]
    public GameObject openingUIRoot;
    [Tooltip("게임 시작 버튼 클릭 시 표시할 튜토리얼 패널입니다.\n비워두면 기존처럼 바로 씬 전환합니다.")]
    public GameObject tutorialPanel;

    [Header("UI Sound")]
    [SerializeField] AudioClip buttonClickClip;
    [SerializeField] [Range(0f, 1f)] float buttonClickVolume = 1f;

    [Header("BGM")]
    [SerializeField] AudioClip openingBgmClip;
    [SerializeField] [Range(0f, 1f)] float openingBgmVolume = 0.7f;

    [Header("Transition")]
    [SerializeField] TransitionMode transitionMode = TransitionMode.LoadScene;
    [SerializeField] string nextSceneName = "MainGameScenes";
    [SerializeField] string pressAnyKeyMessage = "아무 키나 누르세요";
    [SerializeField] float pressAnyKeyFontSize = 36f;
    [Tooltip("오프닝 시작 시 화면이 밝아지는 시간(초). 0이면 페이드 인 없음.")]
    [SerializeField] float entryFadeInSeconds = 1.5f;
    [Tooltip("다음 스테이지로 넘어가기 전 화면 전체가 어두워지는 시간(초). 0이면 즉시 전환.")]
    [SerializeField] float exitFadeOutSeconds = 1.5f;
    [SerializeField] Color exitFadeColor = Color.black;

    public enum TransitionMode
    {
        LoadScene,
        InScene
    }

    const string CreditsMaskObjectName = "Mask";
    const string CreditsObjectName = "StoryCreditsText";
    const string PressAnyKeyObjectName = "PressAnyKeyText";
    const string ExitFadeOverlayObjectName = "ExitFadeOverlay";

    static Sprite whiteSprite;

    RectTransform canvasRect;
    Canvas rootCanvas;
    OpeningSequenceController inSceneController;
    RectTransform creditsMaskRect;
    TextMeshProUGUI creditsText;
    RectTransform creditsRect;
    TextMeshProUGUI pressAnyKeyText;
    Image exitFadeOverlay;
    CanvasGroup openingCanvasGroup;
    float creditsHeight;
    float textScrollHeight;
    float runtimeStartY;
    float elapsed;
    float scrollOffset;
    float crawlTimer;
    float pressKeyElapsed;
    float nextLogTime;
    bool crawlTimerStarted;
    bool loggedTextGone;
    float textVisuallyGoneAt = -1f;
    CrawlPhase phase = CrawlPhase.Scrolling;
    bool openingFinished;
    bool gameStartButtonShown;
    Coroutine activeFadeRoutine;
    bool appHasFocus = true;
    float unfocusedRealtime = -1f;
    bool suppressSkipUntilPointerReleased;
    bool manualReviewMode;
    bool isDraggingManualScroll;
    float manualDragStartPointerY;
    float manualDragStartScrollOffset;
    AudioSource openingBgmSource;

    const string StoryText =
        "인류는 끊임없는 발전과 풍요라는 달콤한 과실을 따기 위해, 매일같이 과학의 한계를 시험대 위에 올렸다. " +
        "구 도심 외곽에 거대하게 솟아오른 '프로메테우스 화학 공장'은 그 오만한 진보의 심장부였다. " +
        "신문명을 이끌어갈 혁신적인 에너지원을 발명해 내겠다는 일념 하에, 통제 불가능할 정도로 위험하고 과도한 실험들이 밤낮을 잊은 채 반복되었다.\n\n" +
        "그러나 한계를 모르는 인간의 욕망은 결국 파멸의 도화선에 불을 붙였다.\n\n" +
        "어느 날 밤, 무리하게 가동되던 핵심 실험로가 임계점을 넘어서며 사상 유례없는 연쇄 대폭발을 일으켰다. " +
        "그것은 지옥의 문이 열리는 소리였다. 그 순간, 인류의 기술로는 감당할 수조차 없는 미지의 정제되지 않은 화학 물질들이 " +
        "차가운 공장 바닥과 대기 중으로 무차별하게 쏟아져 나왔다.";

    public void ConfigureInSceneMode(OpeningSequenceController controller)
    {
        inSceneController = controller;
        transitionMode = TransitionMode.InScene;
    }

    /// <summary>오프닝 루트를 다시 켤 때 크롤 연출을 처음부터 재시작합니다.</summary>
    public void RestartForReplay()
    {
        if (!isActiveAndEnabled)
        {
            Debug.LogWarning(
                "[OpeningStarWarsCrawl] RestartForReplay는 OpeningCanvas가 활성화된 뒤에 호출해야 합니다.");
            return;
        }

        if (activeFadeRoutine != null)
        {
            StopCoroutine(activeFadeRoutine);
            activeFadeRoutine = null;
        }

        if (exitFadeOverlay != null)
            exitFadeOverlay.gameObject.SetActive(false);

        ResetCrawl();
        BeginEntryFade();
    }

    void Awake()
    {
        canvasRect = GetComponent<RectTransform>();
        rootCanvas = GetComponent<Canvas>();
        EnsureFullScreenLayout();
        BuildCreditsText();
        BuildPressAnyKeyText();
        BuildExitFadeOverlay();
        EnsureOpeningCanvasGroup();
        PreloadButtonClickClip();
        GameplayAudioGuard.Unblock();
        BindGameStartButton();
        ResetCrawl();
        BeginEntryFade();
        StartOpeningBgm();
    }

    void OnDestroy()
    {
        StopOpeningBgm();
    }

    void PreloadButtonClickClip()
    {
        EnsureClipLoaded(buttonClickClip);
    }

    void StartOpeningBgm()
    {
        if (openingBgmClip == null)
            return;

        EnsureClipLoaded(openingBgmClip);

        openingBgmSource = gameObject.AddComponent<AudioSource>();
        openingBgmSource.playOnAwake = false;
        openingBgmSource.loop = true;
        openingBgmSource.spatialBlend = 0f;
        openingBgmSource.volume = openingBgmVolume;
        openingBgmSource.clip = openingBgmClip;
        openingBgmSource.Play();
    }

    void StopOpeningBgm()
    {
        if (openingBgmSource == null)
            return;

        if (openingBgmSource.isPlaying)
            openingBgmSource.Stop();
    }

    static void EnsureClipLoaded(AudioClip clip)
    {
        if (clip == null || clip.preloadAudioData)
            return;

        if (clip.loadState == AudioDataLoadState.Unloaded)
            clip.LoadAudioData();
    }

    void PlayGameStartClickSound()
    {
        AudioClip clip = buttonClickClip ?? UIButtonClickSoundPlayer.Instance?.ClickClip;
        if (clip != null)
        {
            UIButtonClickSoundPlayer.PlaySurvivingOneShot(clip, buttonClickVolume);
            return;
        }

        UIButtonClickSoundPlayer.Instance?.PlayClickSound(allowWhenBlocked: true);
    }

    float GetGameStartClickSoundDelay()
    {
        if (buttonClickClip == null)
            return 0.08f;

        return Mathf.Clamp(buttonClickClip.length * 0.35f, 0.08f, 0.2f);
    }

    void BindGameStartButton()
    {
        if (gameStartButton == null)
            return;

        Button button = gameStartButton.GetComponent<Button>();
        if (button == null)
        {
            Debug.LogWarning("[OpeningStarWarsCrawl] gameStartButton에 Button 컴포넌트가 없습니다.");
            return;
        }

        button.onClick.RemoveListener(OnGameStartButtonClicked);
        button.onClick.AddListener(OnGameStartButtonClicked);
    }

    /// <summary>인스펙터 [게임 시작] 버튼 OnClick에도 연결 가능합니다.</summary>
    public void OnGameStartButtonClicked()
    {
        if (openingFinished)
            return;

        // tutorialPanel이 연결되어 있으면 튜토리얼 화면으로 전환합니다.
        if (tutorialPanel != null)
        {
            Debug.Log("[OpeningStarWarsCrawl] [게임 시작] 버튼 클릭 — 튜토리얼 패널을 표시합니다.");
            PlayGameStartClickSound();

            if (openingUIRoot != null)
                openingUIRoot.SetActive(false);
            else if (gameStartButton != null)
                gameStartButton.SetActive(false);

            tutorialPanel.SetActive(true);
            return;
        }

        // tutorialPanel이 없으면 기존처럼 바로 씬 전환합니다.
        Debug.Log("[OpeningStarWarsCrawl] [게임 시작] 버튼 클릭 — 메인 게임 씬으로 이동합니다.");
        openingFinished = true;
        StopActiveFadeRoutine();
        StartCoroutine(PlayClickSoundThenTransition());
    }

    /// <summary>튜토리얼 패널의 [공장 들어가기] 버튼 OnClick에 연결합니다.</summary>
    public void OnEnterFactoryButtonClicked()
    {
        if (openingFinished)
            return;

        Debug.Log("[OpeningStarWarsCrawl] [공장 들어가기] 버튼 클릭 — 메인 게임 씬으로 이동합니다.");
        openingFinished = true;
        StopActiveFadeRoutine();

        // tutorialPanel을 즉시 숨기지 않습니다.
        // exitFade가 화면 전체를 검은색으로 덮은 뒤 씬 전환하므로
        // 씬 로드 대기 중 오프닝 배경이 노출되는 현상을 방지합니다.
        StartCoroutine(PlayClickSoundThenExitTransition());
    }

    IEnumerator PlayClickSoundThenExitTransition()
    {
        PlayGameStartClickSound();
        yield return new WaitForSecondsRealtime(GetGameStartClickSoundDelay());

        if (exitFadeOutSeconds > 0f)
            activeFadeRoutine = StartCoroutine(ExitFadeThenTransition());
        else
            CompleteOpeningTransition();
    }

    IEnumerator PlayClickSoundThenTransition()
    {
        PlayGameStartClickSound();
        yield return new WaitForSecondsRealtime(GetGameStartClickSoundDelay());
        CompleteOpeningTransition();
    }

    void OnApplicationFocus(bool hasFocus)
    {
        HandleApplicationFocusChange(hasFocus);
    }

    void OnApplicationPause(bool paused)
    {
        HandleApplicationFocusChange(!paused);
    }

    void HandleApplicationFocusChange(bool hasFocus)
    {
        if (appHasFocus == hasFocus)
            return;

        appHasFocus = hasFocus;

        if (openingFinished || gameStartButtonShown)
            return;

        if (!hasFocus)
        {
            if (unfocusedRealtime < 0f)
                unfocusedRealtime = Time.realtimeSinceStartup;
            return;
        }

        if (unfocusedRealtime < 0f)
            return;

        float unfocusedDuration = Time.realtimeSinceStartup - unfocusedRealtime;
        unfocusedRealtime = -1f;

        if (unfocusedDuration > 0f)
            AdvanceCrawlByDelta(unfocusedDuration);

        suppressSkipUntilPointerReleased = true;
    }

    void Update()
    {
        if (openingFinished)
            return;

        if (phase == CrawlPhase.WaitingForInput)
        {
            UpdateManualScroll();
            return;
        }

        if (suppressSkipUntilPointerReleased)
        {
            if (!IsAnyPointerPressed())
                suppressSkipUntilPointerReleased = false;
        }
        else if (phase == CrawlPhase.Scrolling && WasScreenTouched())
        {
            SkipCrawlToEnd();
            return;
        }

        AdvanceCrawlByDelta(Time.unscaledDeltaTime);
    }

    void AdvanceCrawlByDelta(float deltaTime)
    {
        if (deltaTime <= 0f)
            return;

        elapsed += deltaTime;
        if (elapsed < initialDelay)
            return;

        if (!crawlTimerStarted)
        {
            crawlTimerStarted = true;
            crawlTimer = 0f;
            nextLogTime = 0f;
            if (logCrawlTimerToConsole)
                Debug.Log("[OpeningTimer] 크롤 시작 — 0.0초 (글자 스크롤 시작)");
        }

        crawlTimer += deltaTime;
        LogCrawlTimerIfNeeded();

        if (phase != CrawlPhase.Scrolling || !crawlTimerStarted)
            return;

        scrollOffset += crawlSpeed * deltaTime;
        UpdateCreditsTransform();
        TrackTextVisuallyGone();
        LogTextGoneIfNeeded();

        if (ShouldShowPressKey())
            CompleteCrawlAndShowButton();
    }

    void SkipCrawlToEnd()
    {
        if (phase != CrawlPhase.Scrolling)
            return;

        EnsureCrawlTimerStarted();
        scrollOffset = GetFinalScrollOffset();
        UpdateCreditsTransform();
        CompleteCrawlAndShowButton();

        if (logCrawlTimerToConsole)
            Debug.Log($"[OpeningTimer] 화면 터치 스킵 — {crawlTimer:F1}초");
    }

    void EnsureCrawlTimerStarted()
    {
        if (crawlTimerStarted)
            return;

        elapsed = Mathf.Max(elapsed, initialDelay);
        crawlTimerStarted = true;
        crawlTimer = 0f;
        nextLogTime = 0f;

        if (logCrawlTimerToConsole)
            Debug.Log("[OpeningTimer] 크롤 시작 — 0.0초 (글자 스크롤 시작)");
    }

    float GetFinalScrollOffset()
    {
        float finalY = GetTopY() + finishPadding + textScrollHeight;
        return Mathf.Max(0f, finalY - runtimeStartY);
    }

    void TrackTextVisuallyGone()
    {
        if (textVisuallyGoneAt >= 0f || !IsCreditsVisuallyGone())
            return;

        textVisuallyGoneAt = crawlTimer;
    }

    void LogCrawlTimerIfNeeded()
    {
        if (!logCrawlTimerToConsole || logIntervalSeconds <= 0f)
            return;

        if (crawlTimer < nextLogTime)
            return;

        nextLogTime += logIntervalSeconds;
        Debug.Log($"[OpeningTimer] {crawlTimer:F1}초");
    }

    void LogTextGoneIfNeeded()
    {
        if (!logCrawlTimerToConsole || loggedTextGone || creditsText == null)
            return;

        if (creditsText.color.a > 0.05f)
            return;

        loggedTextGone = true;
        Debug.Log($"[OpeningTimer] 글자 사라짐(화면상) — {crawlTimer:F1}초");
    }

    bool IsCreditsVisuallyGone()
    {
        if (!crawlTimerStarted || creditsText == null)
            return false;

        return creditsText.color.a <= 0.05f;
    }

    bool ShouldShowPressKey()
    {
        if (pressKeyAtSeconds > 0f)
            return crawlTimer >= pressKeyAtSeconds;

        if (IsCrawlFinished())
            return true;

        if (textVisuallyGoneAt >= 0f)
            return crawlTimer >= textVisuallyGoneAt + pressKeyDelayAfterTextGone;

        return false;
    }

    bool IsCrawlFinished()
    {
        if (creditsRect == null)
            return false;

        float bottomOfText = creditsRect.anchoredPosition.y - textScrollHeight;
        return bottomOfText >= GetTopY() + finishPadding;
    }

    void CompleteCrawlAndShowButton()
    {
        if (gameStartButtonShown)
            return;

        gameStartButtonShown = true;
        phase = CrawlPhase.WaitingForInput;
        manualReviewMode = true;
        scrollOffset = ClampScrollOffset(scrollOffset);
        UpdateCreditsTransform();
        EnsureMaskDragReceiver();

        if (logCrawlTimerToConsole)
            Debug.Log($"[OpeningTimer] 크롤 완료 — [게임 시작] 버튼 표시 ({crawlTimer:F1}초)");

        if (pressAnyKeyText != null)
            pressAnyKeyText.gameObject.SetActive(false);

        if (gameStartButton != null)
        {
            gameStartButton.SetActive(true);
            EnableGameStartInteraction();
            Debug.Log("[OpeningStarWarsCrawl] [게임 시작] 버튼이 활성화되었습니다.");
        }
        else
        {
            Debug.LogWarning("[OpeningStarWarsCrawl] gameStartButton이 연결되지 않았습니다.");
        }
    }

    void UpdateManualScroll()
    {
        float wheelDelta = GetScrollWheelDelta();
        if (Mathf.Abs(wheelDelta) > 0.01f)
        {
            scrollOffset = ClampScrollOffset(scrollOffset + wheelDelta * manualScrollWheelSpeed);
            UpdateCreditsTransform();
        }

        if (WasPointerPressedThisFrame() && IsPointerOverCreditsMask())
        {
            isDraggingManualScroll = true;
            manualDragStartPointerY = GetPointerScreenY();
            manualDragStartScrollOffset = scrollOffset;
        }

        if (isDraggingManualScroll && IsAnyPointerPressed())
        {
            float deltaY = GetPointerScreenY() - manualDragStartPointerY;
            scrollOffset = ClampScrollOffset(manualDragStartScrollOffset + deltaY * manualScrollDragScale);
            UpdateCreditsTransform();
            return;
        }

        isDraggingManualScroll = false;
    }

    void EnsureMaskDragReceiver()
    {
        if (creditsMaskRect == null)
            return;

        Image dragReceiver = creditsMaskRect.GetComponent<Image>();
        if (dragReceiver == null)
        {
            dragReceiver = creditsMaskRect.gameObject.AddComponent<Image>();
            dragReceiver.color = new Color(0f, 0f, 0f, 0f);
        }

        dragReceiver.raycastTarget = true;
    }

    bool IsPointerOverCreditsMask()
    {
        if (creditsMaskRect == null)
            return false;

        Canvas canvas = creditsMaskRect.GetComponentInParent<Canvas>();
        Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;

        return RectTransformUtility.RectangleContainsScreenPoint(
            creditsMaskRect,
            GetPointerScreenPosition(),
            eventCamera);
    }

    float ClampScrollOffset(float offset)
    {
        float min = GetMinScrollOffset();
        float max = GetMaxScrollOffset();
        if (min > max)
            return Mathf.Clamp(offset, max, min);

        return Mathf.Clamp(offset, min, max);
    }

    float GetMinScrollOffset()
    {
        float yAtBottom = GetBottomY() + startOffsetFromBottom + textScrollHeight;
        return yAtBottom - runtimeStartY;
    }

    float GetMaxScrollOffset()
    {
        float yAtTop = GetTopY() - finishPadding;
        return yAtTop - runtimeStartY;
    }

    void EnableGameStartInteraction()
    {
        if (openingCanvasGroup == null)
            return;

        openingCanvasGroup.interactable = true;
        openingCanvasGroup.blocksRaycasts = true;
    }

    void UpdatePressKeyFade()
    {
        if (pressAnyKeyText == null)
            return;

        float alpha = GetPressKeyAlpha();
        Color c = pressAnyKeyText.color;
        c.r = textColor.r;
        c.g = textColor.g;
        c.b = textColor.b;
        c.a = alpha * textColor.a;
        pressAnyKeyText.color = c;
    }

    float GetPressKeyAlpha()
    {
        float t = pressKeyElapsed;
        if (t <= 0f)
            return 0f;

        if (t < pressKeyFadeInSeconds)
            return SmoothFade(t / pressKeyFadeInSeconds);

        if (t < pressKeyFadeInSeconds + pressKeyHoldSeconds)
            return 1f;

        float fadeOutStart = pressKeyFadeInSeconds + pressKeyHoldSeconds;
        if (t < fadeOutStart + pressKeyFadeOutSeconds)
            return 1f - SmoothFade((t - fadeOutStart) / pressKeyFadeOutSeconds);

        return 0f;
    }

    static float SmoothFade(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    void FinishOpening()
    {
        if (openingFinished)
            return;

        openingFinished = true;
        StopActiveFadeRoutine();

        if (exitFadeOutSeconds <= 0f)
        {
            CompleteOpeningTransition();
            return;
        }

        activeFadeRoutine = StartCoroutine(ExitFadeThenTransition());
    }

    void BeginEntryFade()
    {
        if (entryFadeInSeconds <= 0f)
        {
            SetOpeningCanvasAlpha(1f);
            HideFadeOverlay();
            return;
        }

        // 첫 프레임부터 카메라(스카이박스) 색이 비치지 않도록 완전 검은 막을 먼저 올립니다.
        ShowFadeOverlay();
        SetFadeOverlayAlpha(1f);
        SetOpeningCanvasAlpha(0f);
        activeFadeRoutine = StartCoroutine(EntryFadeIn());
    }

    IEnumerator EntryFadeIn()
    {
        // StarGlow 등 캔버스 자식이 붙은 뒤, 내용은 준비해 두고 검은 막만 걷습니다.
        yield return null;

        SetOpeningCanvasAlpha(1f);
        ShowFadeOverlay();
        SetFadeOverlayAlpha(1f);
        EnsureFadeOverlayOnTop();

        float duration = Mathf.Max(0.01f, entryFadeInSeconds);
        float fadeElapsed = 0f;

        while (fadeElapsed < duration)
        {
            fadeElapsed += Time.deltaTime;
            float reveal = SmoothFade(fadeElapsed / duration);
            SetFadeOverlayAlpha(1f - reveal);
            EnsureFadeOverlayOnTop();
            yield return null;
        }

        SetOpeningCanvasAlpha(1f);
        HideFadeOverlay();
        activeFadeRoutine = null;
    }

    void EnsureOpeningCanvasGroup()
    {
        openingCanvasGroup = GetComponent<CanvasGroup>();
        if (openingCanvasGroup == null)
            openingCanvasGroup = gameObject.AddComponent<CanvasGroup>();

        openingCanvasGroup.interactable = true;
        openingCanvasGroup.blocksRaycasts = false;
    }

    void DisableGameStartInteraction()
    {
        if (openingCanvasGroup == null)
            return;

        openingCanvasGroup.interactable = true;
        openingCanvasGroup.blocksRaycasts = false;
    }

    void SetOpeningCanvasAlpha(float alpha)
    {
        if (openingCanvasGroup == null)
            return;

        openingCanvasGroup.alpha = Mathf.Clamp01(alpha);
    }

    IEnumerator ExitFadeThenTransition()
    {
        ShowFadeOverlay();

        float startAlpha = exitFadeOverlay != null ? exitFadeOverlay.color.a : 0f;
        float duration = Mathf.Max(0.01f, exitFadeOutSeconds);
        float fadeElapsed = 0f;

        while (fadeElapsed < duration)
        {
            fadeElapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(startAlpha, 1f, SmoothFade(fadeElapsed / duration));
            SetFadeOverlayAlpha(alpha);
            yield return null;
        }

        SetFadeOverlayAlpha(1f);
        activeFadeRoutine = null;
        CompleteOpeningTransition();
    }

    void StopActiveFadeRoutine()
    {
        if (activeFadeRoutine == null)
            return;

        StopCoroutine(activeFadeRoutine);
        activeFadeRoutine = null;
    }

    void ShowFadeOverlay()
    {
        if (exitFadeOverlay == null)
            return;

        exitFadeOverlay.gameObject.SetActive(true);
        EnsureFadeOverlayOnTop();
    }

    void EnsureFadeOverlayOnTop()
    {
        if (exitFadeOverlay == null)
            return;

        exitFadeOverlay.transform.SetAsLastSibling();
    }

    void HideFadeOverlay()
    {
        if (exitFadeOverlay == null)
            return;

        SetFadeOverlayAlpha(0f);
        exitFadeOverlay.gameObject.SetActive(false);
    }

    void SetFadeOverlayAlpha(float alpha)
    {
        if (exitFadeOverlay == null)
            return;

        Color color = exitFadeColor;
        color.a = Mathf.Clamp01(alpha);
        exitFadeOverlay.color = color;
    }

    void CompleteOpeningTransition()
    {
        if (transitionMode == TransitionMode.InScene)
        {
            if (inSceneController != null)
                inSceneController.FinishOpening();
            else
                gameObject.SetActive(false);

            return;
        }

        LoadNextScene();
    }

    void LoadNextScene()
    {
        if (string.IsNullOrWhiteSpace(nextSceneName))
        {
            Debug.LogWarning("[OpeningStarWarsCrawl] nextSceneName is empty.");
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(nextSceneName))
        {
            Debug.LogError(
                $"[OpeningStarWarsCrawl] '{nextSceneName}' 씬을 불러올 수 없습니다. " +
                "File > Build Settings 에 MainGameScenes 가 포함되어 있는지 확인하세요.");
            return;
        }

        Debug.Log($"[OpeningStarWarsCrawl] '{nextSceneName}' 씬으로 이동합니다.");
        SceneManager.LoadScene(nextSceneName);
    }

    void EnsureFullScreenLayout()
    {
        if (canvasRect == null)
            return;

        canvasRect.anchorMin = Vector2.zero;
        canvasRect.anchorMax = Vector2.one;
        canvasRect.offsetMin = Vector2.zero;
        canvasRect.offsetMax = Vector2.zero;
        canvasRect.localScale = Vector3.one;

        if (rootCanvas != null)
            rootCanvas.sortingOrder = 100;
    }

    static bool WasScreenTouched()
    {
#if ENABLE_INPUT_SYSTEM
        if (Touchscreen.current != null &&
            Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            return true;

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            return true;
#else
        if (Input.GetMouseButtonDown(0))
            return true;

        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
            return true;
#endif
        return false;
    }

    static bool IsAnyPointerPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null && Mouse.current.leftButton.isPressed)
            return true;

        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
            return true;
#else
        if (Input.GetMouseButton(0))
            return true;
#endif
        return false;
    }

    static bool WasPointerPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            return true;

        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            return true;
#else
        if (Input.GetMouseButtonDown(0))
            return true;

        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
            return true;
#endif
        return false;
    }

    static float GetScrollWheelDelta()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
            return Mouse.current.scroll.ReadValue().y;

        return 0f;
#else
        return Input.mouseScrollDelta.y;
#endif
    }

    static Vector2 GetPointerScreenPosition()
    {
#if ENABLE_INPUT_SYSTEM
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
            return Touchscreen.current.primaryTouch.position.ReadValue();

        if (Mouse.current != null)
            return Mouse.current.position.ReadValue();
#else
        return Input.mousePosition;
#endif
        return Vector2.zero;
    }

    static float GetPointerScreenY()
    {
        return GetPointerScreenPosition().y;
    }

    void EnsureCreditsMask()
    {
        if (creditsMask != null)
        {
            creditsMaskRect = creditsMask;
            EnsureMaskClipping(creditsMaskRect.gameObject);
            return;
        }

        Transform existing = transform.Find(CreditsMaskObjectName);
        if (existing != null)
        {
            creditsMaskRect = existing as RectTransform;
            EnsureMaskClipping(creditsMaskRect.gameObject);
            return;
        }

        GameObject maskGo = new GameObject(CreditsMaskObjectName, typeof(RectTransform), typeof(RectMask2D));
        maskGo.transform.SetParent(transform, false);

        creditsMaskRect = maskGo.GetComponent<RectTransform>();
        creditsMaskRect.anchorMin = Vector2.zero;
        creditsMaskRect.anchorMax = Vector2.one;
        creditsMaskRect.offsetMin = Vector2.zero;
        creditsMaskRect.offsetMax = Vector2.zero;
        creditsMaskRect.pivot = new Vector2(0.5f, 0.5f);

        PlaceCreditsMaskAboveBackground();
    }

    static void EnsureMaskClipping(GameObject maskObject)
    {
        if (maskObject.GetComponent<RectMask2D>() != null || maskObject.GetComponent<Mask>() != null)
            return;

        maskObject.AddComponent<RectMask2D>();
    }

    void PlaceCreditsMaskAboveBackground()
    {
        if (creditsMaskRect == null)
            return;

        Transform background = transform.Find("Background_MIilkway");
        if (background == null)
            background = transform.Find("Background_Milkyway");

        int targetIndex = background != null ? background.GetSiblingIndex() + 1 : 0;
        creditsMaskRect.SetSiblingIndex(targetIndex);
    }

    void BuildCreditsText()
    {
        EnsureCreditsMask();

        Transform old = creditsMaskRect.Find(CreditsObjectName);
        if (old != null)
            Destroy(old.gameObject);

        GameObject go = new GameObject(CreditsObjectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(creditsMaskRect, false);

        creditsText = go.GetComponent<TextMeshProUGUI>();
        creditsRect = creditsText.rectTransform;
        creditsRect.anchorMin = new Vector2(0.5f, 0.5f);
        creditsRect.anchorMax = new Vector2(0.5f, 0.5f);
        creditsRect.pivot = new Vector2(0.5f, 1f);
        creditsRect.sizeDelta = new Vector2(textBoxWidth, 1000f);

        if (storyFont != null)
            creditsText.font = storyFont;

        creditsText.text = StoryText;
        creditsText.fontSize = fontSize;
        creditsText.fontWeight = fontWeight;
        creditsText.color = textColor;
        creditsText.alignment = TextAlignmentOptions.Top;
        creditsText.lineSpacing = lineSpacing;
        creditsText.enableWordWrapping = true;
        creditsText.overflowMode = TextOverflowModes.Overflow;
        creditsText.raycastTarget = false;
        creditsText.maskable = true;

        creditsText.ForceMeshUpdate();
        textScrollHeight = creditsText.preferredHeight;
        creditsHeight = textScrollHeight + 120f;
        creditsRect.sizeDelta = new Vector2(textBoxWidth, creditsHeight);
    }

    void BuildExitFadeOverlay()
    {
        Transform old = transform.Find(ExitFadeOverlayObjectName);
        if (old != null)
            Destroy(old.gameObject);

        GameObject go = new GameObject(ExitFadeOverlayObjectName, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform, false);

        RectTransform overlayRect = go.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        exitFadeOverlay = go.GetComponent<Image>();
        exitFadeOverlay.sprite = GetWhiteSprite();
        exitFadeOverlay.type = Image.Type.Simple;
        exitFadeOverlay.raycastTarget = false;

        Color startColor = exitFadeColor;
        startColor.a = 0f;
        exitFadeOverlay.color = startColor;
        go.SetActive(false);
    }

    static Sprite GetWhiteSprite()
    {
        if (whiteSprite != null)
            return whiteSprite;

        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();

        whiteSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
        return whiteSprite;
    }

    void BuildPressAnyKeyText()
    {
        Transform old = transform.Find(PressAnyKeyObjectName);
        if (old != null)
            Destroy(old.gameObject);

        GameObject go = new GameObject(PressAnyKeyObjectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(transform, false);

        pressAnyKeyText = go.GetComponent<TextMeshProUGUI>();
        RectTransform pressRect = pressAnyKeyText.rectTransform;
        pressRect.anchorMin = new Vector2(0.5f, 0f);
        pressRect.anchorMax = new Vector2(0.5f, 0f);
        pressRect.pivot = new Vector2(0.5f, 0f);
        pressRect.anchoredPosition = new Vector2(0f, 48f);
        pressRect.sizeDelta = new Vector2(900f, 80f);

        if (storyFont != null)
            pressAnyKeyText.font = storyFont;

        pressAnyKeyText.text = pressAnyKeyMessage;
        pressAnyKeyText.fontSize = pressAnyKeyFontSize;
        pressAnyKeyText.fontWeight = FontWeight.Bold;
        pressAnyKeyText.enableWordWrapping = false;
        pressAnyKeyText.alignment = TextAlignmentOptions.Center;
        pressAnyKeyText.raycastTarget = false;

        Color startColor = textColor;
        startColor.a = 0f;
        pressAnyKeyText.color = startColor;
        pressAnyKeyText.gameObject.SetActive(false);
    }

    void OnValidate()
    {
        fontSize = Mathf.Clamp(fontSize, 28f, 84f);

        if (!Application.isPlaying)
            return;

        if (creditsText == null || creditsRect == null)
            return;

        creditsText.fontSize = fontSize;
        creditsText.fontWeight = fontWeight;
        creditsText.lineSpacing = lineSpacing;
        creditsRect.sizeDelta = new Vector2(textBoxWidth, Mathf.Max(creditsHeight, 1f));

        if (pressAnyKeyText != null)
        {
            pressAnyKeyText.text = pressAnyKeyMessage;
            pressAnyKeyText.fontSize = pressAnyKeyFontSize;
        }
    }

    void ResetCrawl()
    {
        elapsed = 0f;
        scrollOffset = 0f;
        crawlTimer = 0f;
        pressKeyElapsed = 0f;
        nextLogTime = 0f;
        crawlTimerStarted = false;
        loggedTextGone = false;
        textVisuallyGoneAt = -1f;
        phase = CrawlPhase.Scrolling;
        openingFinished = false;
        gameStartButtonShown = false;
        appHasFocus = Application.isFocused;
        unfocusedRealtime = -1f;
        suppressSkipUntilPointerReleased = false;
        manualReviewMode = false;
        isDraggingManualScroll = false;
        runtimeStartY = GetBottomY() - startOffsetFromBottom;
        UpdateCreditsTransform();

        if (pressAnyKeyText != null)
            pressAnyKeyText.gameObject.SetActive(false);

        if (gameStartButton != null)
            gameStartButton.SetActive(false);

        DisableGameStartInteraction();
    }

    void UpdateCreditsTransform()
    {
        if (creditsRect == null || creditsText == null)
            return;

        float y = runtimeStartY + scrollOffset;
        creditsRect.anchoredPosition = new Vector2(0f, y);

        Color c = textColor;
        if (!manualReviewMode && fadeAtTop)
        {
            float bottomOfText = y - textScrollHeight;
            float fadeStart = GetBottomY();
            if (bottomOfText > fadeStart)
            {
                float t = Mathf.Clamp01((bottomOfText - fadeStart) / Mathf.Max(1f, topFadeRange));
                c.a *= 1f - t;
            }
        }

        creditsText.color = c;
    }

    float GetHalfHeight()
    {
        Canvas.ForceUpdateCanvases();

        RectTransform scrollArea = creditsMaskRect != null ? creditsMaskRect : canvasRect;
        float height = scrollArea != null ? scrollArea.rect.height : 0f;
        if (height < 1f)
            height = Screen.height;

        return height * 0.5f;
    }

    float GetTopY()
    {
        return GetHalfHeight();
    }

    float GetBottomY()
    {
        return -GetHalfHeight();
    }
}
