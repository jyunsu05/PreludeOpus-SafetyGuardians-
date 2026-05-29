using UnityEngine;
using UnityEngine.SceneManagement;

public class UIGameOver : MonoBehaviour
{
    [SerializeField] private string startSceneName = "MainGameScenes";

    private static bool pendingManagerReset;

    void Awake()
    {
        gameObject.SetActive(false);
    }

    /// <summary>
    /// [GameManager / PlayerOxygen 등 팀원 호출용]
    /// 게임오버 발생 시 이 패널을 켜세요.
    /// 예: gameOverObject.SetActive(true);
    /// 또는 UIGameOver 참조.Show();
    /// </summary>
    public void Show()
    {
        gameObject.SetActive(true);
    }

    /// <summary>
    /// [GameManager 등 팀원 호출용]
    /// 게임오버 패널을 끌 때 사용하세요.
    /// 예: gameObject.SetActive(false);
    /// </summary>
    public void Close()
    {
        gameObject.SetActive(false);
    }

    // [처음부터 다시] 버튼 OnClick 연결
    public void OnRestartGame()
    {
        LoadSceneWithReset(startSceneName);
    }

    // [현재 맵 재시작] 버튼 OnClick 연결
    public void OnRetryLevel()
    {
        LoadSceneWithReset(SceneManager.GetActiveScene().name);
    }

    private void LoadSceneWithReset(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("[UIGameOver] 씬 이름이 비어 있습니다.");
            return;
        }

        if (Application.CanStreamedLevelBeLoaded(sceneName) == false)
        {
            Debug.LogError($"[UIGameOver] '{sceneName}' 씬을 로드할 수 없습니다. File → Build Profiles에서 씬을 추가했는지 확인하세요.");
            return;
        }

        pendingManagerReset = true;
        SceneManager.sceneLoaded += OnSceneLoadedReset;
        SceneManager.LoadScene(sceneName);
    }

    // 씬 전환 직후 호출 (DontDestroyOnLoad 매니저는 씬 로드 후에도 유지되므로 여기서 초기화)
    private static void OnSceneLoadedReset(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= OnSceneLoadedReset;

        if (!pendingManagerReset) return;
        pendingManagerReset = false;

        ResetAllManagers();
    }

    private static void ResetAllManagers()
    {
        if (PollutionManager.Instance != null)
            PollutionManager.Instance.ResetPollution();

        if (InventoryManager.Instance != null)
            InventoryManager.Instance.ResetAll();

        if (GameManager.Instance != null)
            GameManager.Instance.ResetToField();

        Debug.Log("[UIGameOver] 모든 매니저 상태 초기화 완료");
    }
}
