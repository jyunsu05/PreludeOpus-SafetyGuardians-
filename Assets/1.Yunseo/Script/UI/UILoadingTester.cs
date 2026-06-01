using UnityEngine;

public class UILoadingTester : MonoBehaviour
{
    [Header("--- 테스트 대상 ---")]
    [SerializeField] private UILoading uiLoading;

    [Header("--- 진행률 설정 ---")]
    [SerializeField] [Range(0f, 1f)] private float testProgress;
    [SerializeField] private float autoProgressSpeed = 0.25f;

    private bool autoProgress;

    void Start()
    {
        if (uiLoading == null)
            uiLoading = FindAnyObjectByType<UILoading>();

        if (uiLoading == null)
        {
            Debug.LogError("[UILoadingTester] UILoading을 찾을 수 없습니다. 인스펙터에 연결하거나 씬에 UILoading을 배치하세요.");
            enabled = false;
            return;
        }

        Debug.Log("[UILoadingTester] 준비 완료. Space: 시작, A: 자동 진행 On/Off, Up/Down: 수동 조절, R: 리셋, M: 메시지 변경");
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            testProgress = 0f;
            uiLoading.ShowLoading("로딩 테스트 시작", testProgress);
            Debug.Log("[UILoadingTester] 로딩 패널 표시");
        }

        if (Input.GetKeyDown(KeyCode.A))
        {
            autoProgress = !autoProgress;
            Debug.Log($"[UILoadingTester] 자동 진행: {(autoProgress ? "ON" : "OFF")}");
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            autoProgress = false;
            testProgress = 0f;
            uiLoading.SetProgressWithText(testProgress, "로딩 초기화");
            Debug.Log("[UILoadingTester] 진행률 리셋");
        }

        if (Input.GetKeyDown(KeyCode.M))
        {
            uiLoading.SetLoadingText("리소스를 불러오는 중입니다...");
            Debug.Log("[UILoadingTester] 메시지 변경");
        }

        if (Input.GetKey(KeyCode.UpArrow))
        {
            testProgress = Mathf.Clamp01(testProgress + Time.deltaTime * 0.5f);
            ApplyProgress();
        }

        if (Input.GetKey(KeyCode.DownArrow))
        {
            testProgress = Mathf.Clamp01(testProgress - Time.deltaTime * 0.5f);
            ApplyProgress();
        }

        if (autoProgress)
        {
            float next = Mathf.Clamp01(testProgress + autoProgressSpeed * Time.deltaTime);
            if (!Mathf.Approximately(next, testProgress))
            {
                testProgress = next;
                ApplyProgress();
            }

            if (testProgress >= 1f)
            {
                autoProgress = false;
                Debug.Log("[UILoadingTester] 100% 도달. 이제 화면 클릭/터치로 다음 동작을 테스트하세요.");
            }
        }
    }

    private void ApplyProgress()
    {
        uiLoading.SetProgress(testProgress);
    }
}
