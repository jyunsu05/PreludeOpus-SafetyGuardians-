using UnityEngine;

public class UIGameOver : MonoBehaviour
{
    void Awake()
    {
        gameObject.SetActive(false);
    }

    public void Show()
    {
        gameObject.SetActive(true);
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }

    /// <summary>[처음부터 다시] 버튼 OnClick — 전체 초기화 후 오프닝(또는 대체) 씬.</summary>
    public void OnRestartGame()
    {
        if (GameManager.Instance == null)
        {
            Debug.LogError("[UIGameOver] GameManager.Instance가 없습니다. 씬에 GameManager가 있는지 확인하세요.");
            return;
        }

        Close();
        GameManager.Instance.RequestRestart(isFullReset: true);
    }

    /// <summary>[현재 맵 재시작] 버튼 OnClick — 챕터 데이터만 초기화(오염도 체크포인트) 후 현재 챕터 재시작.</summary>
    public void OnRetryLevel()
    {
        if (GameManager.Instance == null)
        {
            Debug.LogError("[UIGameOver] GameManager.Instance가 없습니다. 씬에 GameManager가 있는지 확인하세요.");
            return;
        }

        Close();
        GameManager.Instance.RestartCurrentChapter();
    }
}
