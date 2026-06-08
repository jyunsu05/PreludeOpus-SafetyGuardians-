using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// [레거시] 씬 내 FactoryMap_Chapter* 이름 검색 방식.
/// ChapterManager가 있으면 해당 컴포넌트로 위임합니다.
/// </summary>
public class FactoryChapterController : MonoBehaviour
{
    public static FactoryChapterController Instance { get; private set; }

    [SerializeField] private ChapterManager chapterManager;

    [SerializeField] private string chapterMapNamePrefix = "FactoryMap_Chapter";
    [SerializeField, Range(1, 10)] private int maxChapter = 3;

    private const string CurrentChapterPrefsKey = "SG_CurrentFactoryChapter";

    private GameObject[] chapterMapRoots;
    private int currentChapter = 1;

    public int CurrentChapter => chapterManager != null ? chapterManager.CurrentChapterIndex : currentChapter;
    public int MaxChapter => chapterManager != null ? chapterManager.ChapterCount : maxChapter;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        CacheChapterMapsInScene();

        if (PlayerPrefs.HasKey(CurrentChapterPrefsKey))
            ApplyChapter(PlayerPrefs.GetInt(CurrentChapterPrefsKey, 1), savePrefs: false);
        else
            ApplyChapter(DetectActiveChapterFromScene(), savePrefs: true);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public static FactoryChapterController EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        FactoryChapterController existing =
            FindAnyObjectByType<FactoryChapterController>(FindObjectsInactive.Include);
        if (existing != null)
            return existing;

        GameObject host = GameObject.Find("Managers") ?? new GameObject("FactoryChapterController");
        return host.AddComponent<FactoryChapterController>();
    }

    public bool TryAdvanceToNextChapter(out string resultMessage)
    {
        ChapterManager manager = ResolveChapterManager();
        if (manager != null)
            return manager.LoadNextChapter(out resultMessage);

        if (currentChapter >= maxChapter)
        {
            resultMessage = "마지막 공장 챕터입니다.";
            return false;
        }

        int nextChapter = currentChapter + 1;
        if (chapterMapRoots == null || nextChapter > chapterMapRoots.Length || chapterMapRoots[nextChapter - 1] == null)
        {
            resultMessage = $"다음 챕터 맵({chapterMapNamePrefix}{nextChapter})을 찾을 수 없습니다.";
            return false;
        }

        GameManager.Instance?.SaveFactoryCheckpoint();

        ApplyChapter(nextChapter, savePrefs: true);
        RefreshChapterGameplay();
        InventoryManager.Instance?.ClearInventory();
        resultMessage = $"{chapterMapNamePrefix}{nextChapter}(으)로 이동했습니다.";
        return true;
    }

    public void ResetToFirstChapter()
    {
        ChapterManager manager = ResolveChapterManager();
        if (manager != null)
        {
            manager.ResetToFirstChapter();
            return;
        }

        ApplyChapter(1, savePrefs: true);
        RefreshChapterGameplay();
    }

    public void ApplySavedChapter()
    {
        ChapterManager manager = ResolveChapterManager();
        if (manager != null)
        {
            manager.ApplySavedChapter();
            return;
        }

        int chapter = PlayerPrefs.GetInt(CurrentChapterPrefsKey, DetectActiveChapterFromScene());
        ApplyChapter(chapter, savePrefs: false);
    }

    private ChapterManager ResolveChapterManager()
    {
        if (chapterManager != null)
            return chapterManager;

        return ChapterManager.Instance;
    }

    private void ApplyChapter(int chapter, bool savePrefs)
    {
        CacheChapterMapsInScene();

        currentChapter = Mathf.Clamp(chapter, 1, maxChapter);

        for (int i = 0; i < chapterMapRoots.Length; i++)
        {
            GameObject mapRoot = chapterMapRoots[i];
            if (mapRoot == null)
                continue;

            bool shouldActivate = i + 1 == currentChapter;
            if (mapRoot.activeSelf != shouldActivate)
                mapRoot.SetActive(shouldActivate);
        }

        if (savePrefs)
        {
            PlayerPrefs.SetInt(CurrentChapterPrefsKey, currentChapter);
            PlayerPrefs.Save();
        }

        Debug.Log($"[FactoryChapterController] 활성 챕터: {chapterMapNamePrefix}{currentChapter}");
    }

    private void RefreshChapterGameplay()
    {
        GameManager.Instance?.ResetToField();
        GameManager.Instance?.ResetPlayerOxygenOnChapterTransition();

        if (UIManager.Instance != null)
            UIManager.Instance.CloseBattleUI();

        UIBattleManager.ResetSavedContaminationProgress();

        UIResult[] resultPanels = FindObjectsByType<UIResult>(FindObjectsInactive.Include);
        for (int i = 0; i < resultPanels.Length; i++)
            resultPanels[i]?.ResetStageResultState();

        RespawnSpawnersInScene();
        TryRepositionPlayerToActiveMap();
    }

    private static void RespawnSpawnersInScene()
    {
        MonsterSpawner[] monsterSpawners =
            FindObjectsByType<MonsterSpawner>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < monsterSpawners.Length; i++)
            monsterSpawners[i]?.RespawnCurrentStage();

        ItemSpawner[] itemSpawners =
            FindObjectsByType<ItemSpawner>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < itemSpawners.Length; i++)
            itemSpawners[i]?.RespawnCurrentStage();
    }

    private void TryRepositionPlayerToActiveMap()
    {
        GameObject activeMap = GetActiveChapterMap();
        if (activeMap == null)
            return;

        Transform spawn = FindChildByName(activeMap.transform, "PlayerSpawn");
        if (spawn == null)
            return;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
            return;

        player.transform.position = spawn.position;
    }

    private GameObject GetActiveChapterMap()
    {
        if (chapterMapRoots == null)
            return null;

        int index = currentChapter - 1;
        if (index < 0 || index >= chapterMapRoots.Length)
            return null;

        return chapterMapRoots[index];
    }

    private void CacheChapterMapsInScene()
    {
        if (chapterMapRoots == null || chapterMapRoots.Length != maxChapter)
            chapterMapRoots = new GameObject[maxChapter];

        for (int i = 0; i < chapterMapRoots.Length; i++)
            chapterMapRoots[i] = null;

        Scene scene = gameObject.scene;
        if (!scene.IsValid())
            scene = SceneManager.GetActiveScene();

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
            CacheChapterMapsRecursive(roots[i].transform);
    }

    private void CacheChapterMapsRecursive(Transform node)
    {
        if (node == null)
            return;

        for (int chapter = 1; chapter <= maxChapter; chapter++)
        {
            string expectedName = chapterMapNamePrefix + chapter;
            if (node.name != expectedName)
                continue;

            chapterMapRoots[chapter - 1] = node.gameObject;
        }

        for (int i = 0; i < node.childCount; i++)
            CacheChapterMapsRecursive(node.GetChild(i));
    }

    private int DetectActiveChapterFromScene()
    {
        CacheChapterMapsInScene();

        for (int i = 0; i < chapterMapRoots.Length; i++)
        {
            if (chapterMapRoots[i] != null && chapterMapRoots[i].activeSelf)
                return i + 1;
        }

        return 1;
    }

    private static Transform FindChildByName(Transform parent, string childName)
    {
        if (parent == null)
            return null;

        if (parent.name == childName)
            return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindChildByName(parent.GetChild(i), childName);
            if (found != null)
                return found;
        }

        return null;
    }

    public static void ClearSavedChapter()
    {
        PlayerPrefs.DeleteKey(CurrentChapterPrefsKey);
        PlayerPrefs.Save();
    }
}
