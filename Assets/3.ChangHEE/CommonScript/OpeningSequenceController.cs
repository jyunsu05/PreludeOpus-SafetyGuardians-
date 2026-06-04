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
    bool savedCameraState;
    CameraClearFlags savedClearFlags;
    Color savedBackgroundColor;

    void Awake()
    {
        if (crawl == null)
            crawl = GetComponentInChildren<OpeningStarWarsCrawl>(true);

        if (autoCollectGameplayRoots)
            hideDuringOpening = CollectGameplayRoots();

        HideMainContent();
        ApplyBlackCameraBackground();

        if (crawl != null)
            crawl.ConfigureInSceneMode(this);
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
        RestoreMainContent();
        gameObject.SetActive(false);
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

            target.SetActive(savedActiveStates[i]);
        }
    }
}
