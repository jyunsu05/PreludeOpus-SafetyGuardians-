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
    [SerializeField] float crawlSpeed = 55f;
    [SerializeField] float startOffsetFromBottom = 40f;
    [Tooltip("마지막 줄이 화면 위로 사라진 뒤 추가 대기(px). 0에 가까울수록 빨리 '아무 키나'가 뜹니다.")]
    [SerializeField] float finishPadding = 0f;
    [SerializeField] bool fadeAtTop = true;
    [SerializeField] float topFadeRange = 220f;

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

    const string CreditsObjectName = "StoryCreditsText";
    const string PressAnyKeyObjectName = "PressAnyKeyText";
    const string ExitFadeOverlayObjectName = "ExitFadeOverlay";

    static Sprite whiteSprite;

    RectTransform canvasRect;
    Canvas rootCanvas;
    OpeningSequenceController inSceneController;
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
    Coroutine activeFadeRoutine;

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

    void Awake()
    {
        canvasRect = GetComponent<RectTransform>();
        rootCanvas = GetComponent<Canvas>();
        EnsureFullScreenLayout();
        BuildCreditsText();
        BuildPressAnyKeyText();
        BuildExitFadeOverlay();
        EnsureOpeningCanvasGroup();
        ResetCrawl();
        BeginEntryFade();
    }

    void Update()
    {
        if (openingFinished)
            return;

        // 화면 연출은 예전과 동일하게 진행. 키는 스크롤 중·'아무 키나' 표시 중 언제든 다음으로 스킵.
        if (WasAnyKeyPressed())
        {
            FinishOpening();
            return;
        }

        AdvanceCrawlTimer();

        if (phase == CrawlPhase.Scrolling)
            UpdateScroll();

        if (phase == CrawlPhase.WaitingForInput)
        {
            pressKeyElapsed += Time.deltaTime;
            UpdatePressKeyFade();
        }
    }

    void AdvanceCrawlTimer()
    {
        elapsed += Time.deltaTime;
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

        crawlTimer += Time.deltaTime;
        LogCrawlTimerIfNeeded();
    }

    void UpdateScroll()
    {
        if (!crawlTimerStarted)
            return;

        scrollOffset += crawlSpeed * Time.deltaTime;
        UpdateCreditsTransform();
        TrackTextVisuallyGone();
        LogTextGoneIfNeeded();

        if (ShouldShowPressKey())
            BeginWaitingForInput();
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

    void BeginWaitingForInput()
    {
        if (phase == CrawlPhase.WaitingForInput)
            return;

        phase = CrawlPhase.WaitingForInput;
        pressKeyElapsed = 0f;
        if (logCrawlTimerToConsole)
            Debug.Log($"[OpeningTimer] '아무 키나' 페이드 시작 — {crawlTimer:F1}초");

        HideCreditsText();

        if (pressAnyKeyText != null)
        {
            pressAnyKeyText.gameObject.SetActive(true);
            UpdatePressKeyFade();
        }
    }

    void HideCreditsText()
    {
        if (creditsText == null)
            return;

        Color hidden = textColor;
        hidden.a = 0f;
        creditsText.color = hidden;
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

    static bool WasAnyKeyPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
            return true;

        if (Mouse.current != null &&
            (Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame))
            return true;
#endif
        return Input.anyKeyDown;
    }

    void BuildCreditsText()
    {
        Transform old = transform.Find(CreditsObjectName);
        if (old != null)
            Destroy(old.gameObject);

        GameObject go = new GameObject(CreditsObjectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(transform, false);

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
        runtimeStartY = GetBottomY() - startOffsetFromBottom;
        UpdateCreditsTransform();

        if (pressAnyKeyText != null)
            pressAnyKeyText.gameObject.SetActive(false);
    }

    void UpdateCreditsTransform()
    {
        if (creditsRect == null || creditsText == null)
            return;

        float y = runtimeStartY + scrollOffset;
        creditsRect.anchoredPosition = new Vector2(0f, y);

        Color c = textColor;
        if (fadeAtTop)
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

        float height = canvasRect != null ? canvasRect.rect.height : 0f;
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
