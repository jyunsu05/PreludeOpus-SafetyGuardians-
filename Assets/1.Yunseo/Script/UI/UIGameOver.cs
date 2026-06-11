using UnityEngine;
using UnityEngine.SceneManagement;

public class UIGameOver : MonoBehaviour
{
    private const string OpeningSceneName = "OpeningScene";

    [SerializeField] private AudioClip gameOverSoundClip;

    public void Show()
    {
        EnsureActiveInHierarchy();
        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        GameManager.Instance?.EnterGameOverFreeze();
        PlayGameOverSound();
        UIButtonClickSoundPlayer.Instance?.RegisterButtonsInHierarchy(transform);
        Debug.Log("[UIGameOver] 게임오버 화면 표시");
    }

    private void PlayGameOverSound()
    {
        if (gameOverSoundClip == null)
            return;

        UIButtonClickSoundPlayer.Instance?.PlayOneShotClip(gameOverSoundClip, allowWhenBlocked: true);
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }

    private void EnsureActiveInHierarchy()
    {
        Transform parent = transform.parent;
        while (parent != null)
        {
            if (!parent.gameObject.activeSelf)
                parent.gameObject.SetActive(true);

            parent = parent.parent;
        }
    }

    /// <summary>[처음부터 시작] — 세션 데이터 초기화 후 OpeningScene으로 이동.</summary>
    public void LoadOpeningScene()
    {
        Close();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.LoadOpeningScene();
            return;
        }

        Debug.LogWarning(
            "[UIGameOver] GameManager가 없어 세션 초기화 없이 OpeningScene을 로드합니다. " +
            "씬에 GameManager가 있는지 확인하세요.");

        if (!Application.CanStreamedLevelBeLoaded(OpeningSceneName))
        {
            Debug.LogError(
                $"[UIGameOver] '{OpeningSceneName}' 씬을 로드할 수 없습니다. " +
                "Build Settings에 씬이 포함되어 있는지 확인하세요.");
            return;
        }

        SceneManager.LoadScene(OpeningSceneName);
    }

    /// <summary>LoadOpeningScene 별칭 — 기존 버튼 바인딩 호환용.</summary>
    public void OnRestartGame() => LoadOpeningScene();

    /// <summary>[현재 챕터에서 시작] — 오프닝 없이 세션·월드 전량 리셋 후 현재 챕터만 재생성(처음부터 시작과 동일 패턴).</summary>
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

