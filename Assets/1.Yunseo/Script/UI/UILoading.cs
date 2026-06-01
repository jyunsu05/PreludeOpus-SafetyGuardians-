using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using UnityEngine.SceneManagement;

public class UILoading : MonoBehaviour
{
    [SerializeField] private GameObject loadingPanel; // 로딩 패널 오브젝트
    [SerializeField] private Slider loadingProgressBar; // 로딩 진행 바
    [SerializeField] private TextMeshProUGUI loadingProgressText; // 로딩 진행 텍스트
    [SerializeField] private TextMeshProUGUI loadingMessageText; // 로딩 상태 문구 텍스트
    [SerializeField] private float minDisplayTime = 2f; // 최소 로딩 화면 표시 시간
    [SerializeField] private TextMeshProUGUI InformationText;
    [SerializeField] private string mainFactorySceneName = "FactoryScene"; // 로딩 완료 후 이동할 메인 공장 씬 이름

    private float panelShownTime;
    private bool waitForTouchDismiss;
    private bool isSceneLoading;

    void Awake()
    {
        if (loadingPanel != null)
            loadingPanel.SetActive(false);

        if (InformationText != null)
            InformationText.gameObject.SetActive(false);

        SetProgress(0f);
        SetLoadingText("로딩중");
    }

    void Update()
    {
        SyncProgressTextFromSlider();

        if (isSceneLoading || loadingPanel == null || !loadingPanel.activeSelf || !CanDismissByInput())
            return;

        if (Input.GetMouseButtonDown(0))
        {
            LoadMainFactoryScene();
            return;
        }

        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began)
                LoadMainFactoryScene();
        }
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

        if (loadingPanel != null)
            loadingPanel.SetActive(true);

        SetProgress(initialProgress);
        SetLoadingText(message);
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

        if (loadingPanel != null)
            loadingPanel.SetActive(false);
    }

    private void LoadMainFactoryScene()
    {
        if (string.IsNullOrEmpty(mainFactorySceneName))
        {
            Debug.LogError("[UILoading] mainFactorySceneName이 비어 있습니다. 씬 이름을 확인해주세요.");
            return;
        }

        float progressPercent = 0f;
        if (loadingProgressBar != null)
        {
            float normalized = Mathf.InverseLerp(loadingProgressBar.minValue, loadingProgressBar.maxValue, loadingProgressBar.value);
            progressPercent = normalized * 100f;
        }

        Debug.Log($"[UILoading] 로딩 화면 클릭/터치 감지. {progressPercent:0.00}% 상태에서 '{mainFactorySceneName}' 씬으로 이동합니다.");

        // TODO: 씬 연결 완료 후 아래 2줄 주석 해제
        // isSceneLoading = true;
        // SceneManager.LoadScene(mainFactorySceneName);
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
}
