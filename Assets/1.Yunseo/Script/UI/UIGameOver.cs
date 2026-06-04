using UnityEngine;

public class UIGameOver : MonoBehaviour
{
    void Awake()
    {
        gameObject.SetActive(false);
    }

    public void Show()
    {
        GameManager.Instance?.ResetAllSystems();
        gameObject.SetActive(true);
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }

    /// <summary>[처음부터 시작] — 전체 데이터 초기화 후 OpeningSequenceRoot 활성화(없으면 오프닝 씬 로드).</summary>
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

    /// <summary>[현재 챕터에서 시작] — 오프닝 없이 체크포인트 오염도 복구 후 현재 챕터 스폰으로 이동.</summary>
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
