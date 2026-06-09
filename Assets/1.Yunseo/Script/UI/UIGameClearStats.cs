using UnityEngine;
using UnityEngine.UI;
using UnityEngine.U2D;
using TMPro;

/// <summary>
/// 공장 3 클리어 성과 UI (메인 패널). Spec §9.2 — 상세 패널은 WP-5.
/// </summary>
public class UIGameClearStats : MonoBehaviour
{
    private static readonly string[] GradeSpriteNames =
    {
        "ClearIcon_0",
        "ClearIcon_1",
        "ClearIcon_2",
        "ClearIcon_3",
        "ClearIcon_4",
    };

    [Header("--- UI References ---")]
    [SerializeField] private Image gradeIcon;
    [SerializeField] private TextMeshProUGUI clearText;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button fromBeginningButton;
    [SerializeField] private Button viewDetailsButton;

    [Header("--- Grade Icon (ClearIcon Atlas) ---")]
    [SerializeField] private SpriteAtlas clearIconAtlas;

    [Header("--- Options ---")]
    [SerializeField] private bool showScoreInSummary = true;

    private bool hasShownMainPanel;

    private void Awake()
    {
        ResolveReferences();
        WireButtons();
    }

    private void Start()
    {
        if (Application.isPlaying && !hasShownMainPanel)
            gameObject.SetActive(false);
    }

    public void ResetShowState()
    {
        hasShownMainPanel = false;
    }

    public void ShowMain()
    {
        SaveSnapshotIfNeeded();
        BindClearRunPresentation();
        Show();
    }

    public void Show()
    {
        EnsureOnRootCanvas();
        transform.SetAsLastSibling();
        hasShownMainPanel = true;
        gameObject.SetActive(true);
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }

    public void OnCloseClick()
    {
        Close();

        if (GameManager.Instance != null)
            GameManager.Instance.LoadOpeningScene();
    }

    public void OnFromBeginningClick()
    {
        Close();
        PlaySessionStats.Instance?.ResetAll();

        if (GameManager.Instance != null)
            GameManager.Instance.LoadOpeningScene();
    }

    public void OnViewDetailsClick()
    {
        Debug.Log("[UIGameClearStats] 상세보기 UI는 아직 준비 중입니다. (WP-5)");
    }

    private void SaveSnapshotIfNeeded()
    {
        PlaySessionStats stats = PlaySessionStats.EnsureInstance();
        ChapterManager chapterManager = ChapterManager.Instance;
        if (stats == null || chapterManager == null)
            return;

        stats.SaveChapterSnapshot(chapterManager.CurrentChapterIndex, ResolveCurrentOxygenPercent());
    }

    private void BindClearRunPresentation()
    {
        PlaySessionStats stats = PlaySessionStats.EnsureInstance();
        if (stats == null)
        {
            SetGradeIcon(SessionGrade.D);
            if (clearText != null)
                clearText.text = "버틴 수호자";
            return;
        }

        SessionGrade grade = stats.GetMainGrade();
        SetGradeIcon(grade);

        if (clearText == null)
            return;

        StatBlock clearRun = stats.ClearRun;
        string summary = $"도망 {clearRun.escapeCount}회 · 산소 {clearRun.finalOxygenPercent:F0}%";
        if (showScoreInSummary)
            summary = $"{stats.GetMainScore()}점 · {summary}";

        clearText.text = $"{stats.GetMainTitle()}\n{summary}";
    }

    private void SetGradeIcon(SessionGrade grade)
    {
        if (gradeIcon == null)
            return;

        int index = (int)grade;
        Sprite sprite = null;
        if (clearIconAtlas != null && index >= 0 && index < GradeSpriteNames.Length)
            sprite = clearIconAtlas.GetSprite(GradeSpriteNames[index]);

        gradeIcon.sprite = sprite;
        gradeIcon.enabled = sprite != null;
        gradeIcon.preserveAspect = true;
    }

    private void ResolveReferences()
    {
        if (gradeIcon == null)
            gradeIcon = transform.Find("ClearIcon")?.GetComponent<Image>();

        if (clearText == null)
            clearText = transform.Find("ClearText")?.GetComponent<TextMeshProUGUI>();

        Transform buttonRoot = transform.Find("UIButtonContainer");
        if (buttonRoot == null)
            return;

        if (closeButton == null)
            closeButton = buttonRoot.Find("CloseButton")?.GetComponent<Button>();

        if (fromBeginningButton == null)
            fromBeginningButton = buttonRoot.Find("FromTheBeginningButton")?.GetComponent<Button>();

        if (viewDetailsButton == null)
            viewDetailsButton = buttonRoot.Find("ViewDetailsButton")?.GetComponent<Button>();
    }

    private void WireButtons()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(OnCloseClick);
            closeButton.onClick.AddListener(OnCloseClick);
        }

        if (fromBeginningButton != null)
        {
            fromBeginningButton.onClick.RemoveListener(OnFromBeginningClick);
            fromBeginningButton.onClick.AddListener(OnFromBeginningClick);
        }

        if (viewDetailsButton != null)
        {
            viewDetailsButton.onClick.RemoveListener(OnViewDetailsClick);
            viewDetailsButton.onClick.AddListener(OnViewDetailsClick);
        }
    }

    private void EnsureOnRootCanvas()
    {
        Canvas rootCanvas = GetComponentInParent<Canvas>(true);
        if (rootCanvas == null)
            rootCanvas = FindAnyObjectByType<Canvas>(FindObjectsInactive.Include);

        if (rootCanvas == null)
            return;

        Transform canvasTransform = rootCanvas.transform;
        if (transform.parent != canvasTransform)
            transform.SetParent(canvasTransform, false);

        if (!rootCanvas.gameObject.activeSelf)
            rootCanvas.gameObject.SetActive(true);
    }

    private static float ResolveCurrentOxygenPercent()
    {
        PlayerOxygen[] oxygenComponents =
            FindObjectsByType<PlayerOxygen>(FindObjectsInactive.Include);
        if (oxygenComponents.Length == 0)
            return 0f;

        PlayerOxygen oxygen = oxygenComponents[0];
        if (oxygen.maxOxygen <= 0f)
            return 0f;

        return Mathf.Clamp(oxygen.currentOxygen / oxygen.maxOxygen * 100f, 0f, 100f);
    }

#if UNITY_EDITOR
    [ContextMenu("Debug/Preview Show Main")]
    private void DebugPreviewShowMain()
    {
        PlaySessionStats stats = PlaySessionStats.EnsureInstance();
        stats.ResetAll();
        stats.BeginClearRun();
        stats.BeginCurrentChapterStats();
        stats.TryRecordPurification("debug_a");
        stats.TryRecordPurification("debug_b");
        stats.RecordEscape();
        stats.ClearRun.finalOxygenPercent = 63f;
        ShowMain();
    }
#endif
}
