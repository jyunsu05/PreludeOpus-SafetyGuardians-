using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// MainGameScenes 안에서 오프닝 연출을 켜고 끄는 컨트롤러.
/// 평소: OpeningSequence 루트를 비활성(인스pector 체크 해제) → 메인 게임만 실행.
/// 전체 테스트: OpeningSequence 활성 → 오프닝 후 메인 콘텐츠 복원.
/// </summary>
public class OpeningSequenceController : MonoBehaviour
{
    [Header("Opening")]
    [SerializeField] OpeningStarWarsCrawl crawl;

    [Header("Hide During Opening")]
    [SerializeField] bool autoCollectGameplayRoots = true;
    [SerializeField] GameObject[] hideDuringOpening;

    [Header("Camera")]
    [SerializeField] Camera mainCamera;

    bool[] savedActiveStates;
    bool finished;
    bool pendingReplayStart;
    bool savedCameraState;
    CameraClearFlags savedClearFlags;
    Color savedBackgroundColor;

    void Awake()
    {
        if (crawl == null)
            crawl = GetComponentInChildren<OpeningStarWarsCrawl>(true);

        if (autoCollectGameplayRoots)
            hideDuringOpening = CollectGameplayRoots();

        DeactivateAllChaptersBeforeOpening();
        HideMainContent();
        ApplyBlackCameraBackground();

        if (crawl != null)
            crawl.ConfigureInSceneMode(this);
    }

    void OnEnable()
    {
        if (!pendingReplayStart)
            return;

        pendingReplayStart = false;
        StartCrawlReplayIfReady();
    }

    /// <summary>게임오버 → 처음부터 시작 등으로 오프닝을 다시 재생할 때 호출합니다.</summary>
    public void PrepareForReplay()
    {
        finished = false;
        pendingReplayStart = true;

        if (autoCollectGameplayRoots)
            hideDuringOpening = CollectGameplayRoots();

        DeactivateAllChaptersBeforeOpening();
        HideMainContent();
        ApplyBlackCameraBackground();

        if (crawl != null)
        {
            if (!crawl.gameObject.activeSelf)
                crawl.gameObject.SetActive(true);

            crawl.ConfigureInSceneMode(this);
        }

        if (isActiveAndEnabled)
        {
            pendingReplayStart = false;
            StartCrawlReplayIfReady();
        }
    }

    void StartCrawlReplayIfReady()
    {
        if (crawl == null || !crawl.isActiveAndEnabled)
            return;

        crawl.RestartForReplay();
    }

    static void DeactivateAllChaptersBeforeOpening()
    {
        ChapterManager chapterManager = ChapterManager.Instance;
        if (chapterManager == null)
            chapterManager = FindAnyObjectByType<ChapterManager>(FindObjectsInactive.Include);

        chapterManager?.DeactivateAllChaptersForOpening();
    }

    GameObject[] CollectGameplayRoots()
    {
        var collected = new List<GameObject>();
        var scene = gameObject.scene;

        foreach (var root in scene.GetRootGameObjects())
        {
            if (root == null || root == gameObject || root.transform.IsChildOf(transform))
                continue;

            if (ShouldHideDuringOpening(root))
                collected.Add(root);
        }

        return collected.ToArray();
    }

    static bool ShouldHideDuringOpening(GameObject root)
    {
        string name = root.name;
        if (name == "Player" || name == "Canvas" || name == "Managers")
            return true;

        return name.StartsWith("FactoryStage") || name.StartsWith("FactoryMap");
    }

    void HideMainContent()
    {
        if (hideDuringOpening == null || hideDuringOpening.Length == 0)
            return;

        savedActiveStates = new bool[hideDuringOpening.Length];
        for (int i = 0; i < hideDuringOpening.Length; i++)
        {
            GameObject target = hideDuringOpening[i];
            if (target == null)
                continue;

            savedActiveStates[i] = target.activeSelf;
            target.SetActive(false);
        }
    }

    public void FinishOpening()
    {
        if (finished)
            return;

        finished = true;
        RestoreCameraBackground();
        RestoreGameplayWorldAfterOpening();

        gameObject.SetActive(false);

        if (GameManager.Instance != null)
            GameManager.Instance.StartNewGameAfterOpening();
        else
            EnsureChapterOneAfterOpeningFallback();
    }

    static void EnsureChapterOneAfterOpeningFallback()
    {
        ChapterManager chapterManager = ChapterManager.Instance;
        if (chapterManager == null)
            chapterManager = FindAnyObjectByType<ChapterManager>(FindObjectsInactive.Include);

        chapterManager?.BeginNewPlaySession();
    }

    void ApplyBlackCameraBackground()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (mainCamera == null)
            return;

        savedClearFlags = mainCamera.clearFlags;
        savedBackgroundColor = mainCamera.backgroundColor;
        savedCameraState = true;

        mainCamera.clearFlags = CameraClearFlags.SolidColor;
        mainCamera.backgroundColor = Color.black;
    }

    void RestoreCameraBackground()
    {
        if (!savedCameraState || mainCamera == null)
            return;

        mainCamera.clearFlags = savedClearFlags;
        mainCamera.backgroundColor = savedBackgroundColor;
        savedCameraState = false;
    }

    void RestoreMainContent()
    {
        if (hideDuringOpening == null || savedActiveStates == null)
            return;

        for (int i = 0; i < hideDuringOpening.Length; i++)
        {
            GameObject target = hideDuringOpening[i];
            if (target == null)
                continue;

            bool restoreActive = savedActiveStates[i];
            if (GameManager.ShouldForceActiveAfterOpening(target.name))
                restoreActive = true;

            target.SetActive(restoreActive);
        }
    }

    /// <summary>오프닝 루트가 꺼진 뒤 메인 플레이에 필요한 루트·매니저를 반드시 켭니다.</summary>
    void RestoreGameplayWorldAfterOpening()
    {
        RestoreMainContent();

        if (GameManager.Instance != null && !GameManager.Instance.gameObject.activeSelf)
            GameManager.Instance.gameObject.SetActive(true);

        GameManager.ActivateChapterMapsHierarchy();
    }
}
