using UnityEngine;

/// <summary>
/// 전투 중 정화 성공(OnContaminationEmpty) 시, 이번에 만난 공장 몬스터를 제거합니다.
/// MonsterSpawner 오브젝트에 붙여 두세요.
/// </summary>
public class MonsterBattleTracker : MonoBehaviour
{
    public static MonsterBattleTracker Instance { get; private set; }

    private GameObject currentBattleMonster;
    private GameObject purifiedMonsterSnapshot;
    private bool purifiedThisBattle;
    private UIBattleManager battleManager;
    private MonsterSpawner monsterSpawner;
    private GameObject battleSceneUI;
    private bool wasBattleUIActive;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        monsterSpawner = GetComponent<MonsterSpawner>();
        if (monsterSpawner == null)
            monsterSpawner = FindAnyObjectByType<MonsterSpawner>();

        TryBindBattleManager();
        TryFindBattleUI();

        if (GameManager.Instance != null)
            GameManager.Instance.OnBattleEnded += OnBattleEnded;
    }

    private void Update()
    {
        if (battleManager == null)
            TryBindBattleManager();

        if (battleSceneUI == null)
            TryFindBattleUI();

        CheckBattleUIOpened();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        UnbindBattleManager();

        if (GameManager.Instance != null)
            GameManager.Instance.OnBattleEnded -= OnBattleEnded;
    }

    public void RegisterBattleMonster(GameObject monster)
    {
        if (monster == null)
            return;

        if (purifiedThisBattle)
            return;

        if (currentBattleMonster == monster)
            return;

        currentBattleMonster = monster;
        purifiedMonsterSnapshot = null;
        Debug.Log($"[MonsterBattleTracker] 전투 몬스터 등록: {monster.name}");
    }

    private void TryBindBattleManager()
    {
        if (battleManager != null)
            return;

        battleManager = FindAnyObjectByType<UIBattleManager>(FindObjectsInactive.Include);
        if (battleManager != null)
            battleManager.OnContaminationEmpty += OnContaminationEmpty;
    }

    private void UnbindBattleManager()
    {
        if (battleManager == null)
            return;

        battleManager.OnContaminationEmpty -= OnContaminationEmpty;
        battleManager = null;
    }

    private void OnContaminationEmpty()
    {
        purifiedThisBattle = true;
        purifiedMonsterSnapshot = currentBattleMonster;
        Debug.Log($"[MonsterBattleTracker] 몬스터 정화 성공. 대상: {purifiedMonsterSnapshot?.name ?? "없음"}");
    }

    private void OnBattleEnded()
    {
        GameObject target = purifiedThisBattle ? purifiedMonsterSnapshot : null;

        if (purifiedThisBattle && target != null)
        {
            if (monsterSpawner != null)
                monsterSpawner.RemoveSpawnedMonster(target);

            Destroy(target);
            Debug.Log($"[MonsterBattleTracker] 정화된 몬스터를 공장에서 제거했습니다: {target.name}");
        }
        else if (purifiedThisBattle)
        {
            Debug.LogWarning("[MonsterBattleTracker] 정화됐지만 제거할 몬스터가 등록되지 않았습니다.");
        }

        currentBattleMonster = null;
        purifiedMonsterSnapshot = null;
        purifiedThisBattle = false;
    }

    private void TryFindBattleUI()
    {
        if (battleSceneUI != null)
            return;

        var battleManagerObject = FindAnyObjectByType<UIBattleManager>(FindObjectsInactive.Include);
        if (battleManagerObject != null)
            battleSceneUI = battleManagerObject.gameObject;
    }

    private void CheckBattleUIOpened()
    {
        if (battleSceneUI == null)
            return;

        bool active = battleSceneUI.activeInHierarchy;
        if (active && !wasBattleUIActive)
            TryRegisterOverlappingMonster();

        wasBattleUIActive = active;
    }

    private void TryRegisterOverlappingMonster()
    {
        if (purifiedThisBattle)
            return;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
            return;

        Collider2D playerCollider = player.GetComponent<Collider2D>();
        float radius = 0.6f;
        if (playerCollider != null)
            radius = Mathf.Max(playerCollider.bounds.extents.x, playerCollider.bounds.extents.y) + 0.15f;

        Collider2D[] hits = Physics2D.OverlapCircleAll(player.transform.position, radius);
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].CompareTag("Monster"))
            {
                RegisterBattleMonster(hits[i].gameObject);
                return;
            }
        }
    }
}
