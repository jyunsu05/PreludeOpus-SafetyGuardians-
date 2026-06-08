using System.Collections;
using UnityEngine;

/// <summary>
/// 전투 중 정화 성공(OnContaminationEmpty) 시, 이번에 만난 공장 몬스터를 제거합니다.
/// MonsterSpawner 오브젝트에 붙여 두세요.
/// </summary>
public class MonsterBattleTracker : MonoBehaviour
{
    public static MonsterBattleTracker Instance { get; private set; }

    [Header("PlayerController와 동일한 전투 UI 루트")]
    [SerializeField] private GameObject battleSceneUI;

    private GameObject currentBattleMonster;
    private GameObject purifiedMonsterSnapshot;
    private bool purifiedThisBattle;
    private UIBattleManager battleManager;
    private MonsterSpawner monsterSpawner;
    private bool wasBattleUIActive;
    private bool gameManagerSubscribed;
    private Coroutine registerRetryRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            if (!Instance.isActiveAndEnabled)
            {
                Destroy(Instance);
                Instance = this;
                return;
            }

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
        TrySubscribeGameManager();
    }

    private void Update()
    {
        if (battleManager == null)
            TryBindBattleManager();

        if (battleSceneUI == null)
            TryFindBattleUI();

        if (!gameManagerSubscribed)
            TrySubscribeGameManager();

        CheckBattleUIOpened();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        if (registerRetryRoutine != null)
            StopCoroutine(registerRetryRoutine);

        UnbindBattleManager();
        UnsubscribeGameManager();
    }

    /// <summary>게임오버·챕터 재시작 등에서 전투 추적 상태를 비웁니다.</summary>
    public void ResetBattleTrackingState()
    {
        purifiedThisBattle = false;
        currentBattleMonster = null;
        purifiedMonsterSnapshot = null;
    }

    public static void ResetInstanceBattleTrackingState()
    {
        if (Instance != null)
            Instance.ResetBattleTrackingState();
    }

    /// <summary>정화 완료 후 필드 몬스터를 제거합니다(MonsterBattleTracker 없을 때 fallback).</summary>
    public static void TryRemoveEncounteredMonsterFromField()
    {
        GameObject target = BattleEncounterContext.PeekEncounteredMonsterObject();
        if (target == null)
            return;

        MonsterSpawner spawner = FindSpawnerForMonster(target);
        spawner?.RemoveSpawnedMonster(target);
        Destroy(target);

        BattleEncounterContext.ConsumeEncounteredMonsterObject();
        ResetInstanceBattleTrackingState();
        Debug.Log($"[MonsterBattleTracker] 정화된 필드 몬스터를 제거했습니다: {target.name}");
    }

    private static MonsterSpawner FindSpawnerForMonster(GameObject monster)
    {
        if (monster == null)
            return null;

        MonsterSpawner spawner = monster.GetComponentInParent<MonsterSpawner>();
        if (spawner != null)
            return spawner;

        return FindAnyObjectByType<MonsterSpawner>();
    }

    public void RegisterBattleMonster(GameObject monster)
    {
        if (monster == null)
            return;

        if (purifiedThisBattle)
            ResetBattleTrackingState();

        if (currentBattleMonster == monster)
            return;

        currentBattleMonster = monster;
        purifiedMonsterSnapshot = null;
        BattleEncounterContext.SetEncounteredMonsterObject(monster);
        Debug.Log($"[MonsterBattleTracker] 전투 몬스터 등록: {monster.name}");
    }

    private void TrySubscribeGameManager()
    {
        if (gameManagerSubscribed || GameManager.Instance == null)
            return;

        GameManager.Instance.OnBattleEnded -= OnBattleEnded;
        GameManager.Instance.OnBattleEnded += OnBattleEnded;
        gameManagerSubscribed = true;
    }

    private void UnsubscribeGameManager()
    {
        if (!gameManagerSubscribed || GameManager.Instance == null)
            return;

        GameManager.Instance.OnBattleEnded -= OnBattleEnded;
        gameManagerSubscribed = false;
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
        EnsureBattleMonsterRegistered();

        purifiedThisBattle = true;
        purifiedMonsterSnapshot = currentBattleMonster;
        PollutionManager.Instance?.OnMonsterPurified();
        Debug.Log($"[MonsterBattleTracker] 몬스터 정화 성공. 대상: {purifiedMonsterSnapshot?.name ?? "없음"}");
    }

    private void OnBattleEnded()
    {
        if (purifiedThisBattle)
            TryRemoveEncounteredMonsterFromField();
        else
            ResetBattleTrackingState();
    }

    private void TryFindBattleUI()
    {
        if (battleSceneUI != null)
            return;

        UIBattleManager[] managers = FindObjectsByType<UIBattleManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < managers.Length; i++)
        {
            Transform candidate = managers[i].transform;
            while (candidate != null)
            {
                string name = candidate.name;
                if (name.Contains("UIBattle") || name.Contains("Battlescene"))
                {
                    battleSceneUI = candidate.gameObject;
                    return;
                }

                if (candidate.parent != null && candidate.parent.name == "Canvas")
                {
                    battleSceneUI = candidate.gameObject;
                    return;
                }

                candidate = candidate.parent;
            }
        }
    }

    private void CheckBattleUIOpened()
    {
        if (battleSceneUI == null)
            return;

        bool active = battleSceneUI.activeInHierarchy;
        if (active && !wasBattleUIActive)
            BeginBattleMonsterRegistration();

        wasBattleUIActive = active;
    }

    private void BeginBattleMonsterRegistration()
    {
        EnsureBattleMonsterRegistered();

        if (currentBattleMonster != null)
            return;

        if (registerRetryRoutine != null)
            StopCoroutine(registerRetryRoutine);

        registerRetryRoutine = StartCoroutine(RetryRegisterBattleMonster());
    }

    private IEnumerator RetryRegisterBattleMonster()
    {
        for (int i = 0; i < 5; i++)
        {
            EnsureBattleMonsterRegistered();
            if (currentBattleMonster != null)
            {
                registerRetryRoutine = null;
                yield break;
            }

            yield return null;
        }

        if (currentBattleMonster == null)
            Debug.LogWarning("[MonsterBattleTracker] 전투 UI는 열렸지만 등록할 몬스터를 찾지 못했습니다.");

        registerRetryRoutine = null;
    }

    private void EnsureBattleMonsterRegistered()
    {
        if (currentBattleMonster != null || purifiedThisBattle)
            return;

        GameObject fromContext = BattleEncounterContext.PeekEncounteredMonsterObject();
        if (fromContext != null)
        {
            RegisterBattleMonster(fromContext);
            return;
        }

        GameObject overlappingMonster = FindOverlappingMonsterNearPlayer();
        if (overlappingMonster != null)
        {
            RegisterBattleMonster(overlappingMonster);
            return;
        }

        GameObject battleMonster = FindMonsterForCurrentBattle();
        if (battleMonster != null)
            RegisterBattleMonster(battleMonster);
    }

    private GameObject FindOverlappingMonsterNearPlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
            return null;

        Collider2D playerCollider = player.GetComponent<Collider2D>();
        float radius = 3f;
        if (playerCollider != null)
        {
            radius = Mathf.Max(
                radius,
                Mathf.Max(playerCollider.bounds.extents.x, playerCollider.bounds.extents.y) + 1f);
        }

        Collider2D[] hits = Physics2D.OverlapCircleAll(player.transform.position, radius);
        GameObject closestMonster = null;
        float closestDistance = float.MaxValue;

        for (int i = 0; i < hits.Length; i++)
        {
            GameObject monster = ResolveMonsterObject(hits[i]);
            if (monster == null)
                continue;

            float distance = Vector2.Distance(player.transform.position, monster.transform.position);
            if (distance >= closestDistance)
                continue;

            closestMonster = monster;
            closestDistance = distance;
        }

        return closestMonster;
    }

    private GameObject FindMonsterForCurrentBattle()
    {
        if (battleManager == null)
            battleManager = FindAnyObjectByType<UIBattleManager>(FindObjectsInactive.Include);

        MonsterData data = battleManager != null ? battleManager.GetCurrentMonsterData() : null;
        if (data == null || string.IsNullOrEmpty(data.id))
            return null;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        Vector2 origin = player != null ? player.transform.position : Vector2.zero;

        MonsterController[] monsters = FindObjectsByType<MonsterController>(FindObjectsSortMode.None);
        GameObject closestMonster = null;
        float closestDistance = float.MaxValue;

        for (int i = 0; i < monsters.Length; i++)
        {
            GameObject candidate = monsters[i].gameObject;
            if (!MatchesMonsterId(candidate, data.id))
                continue;

            float distance = Vector2.Distance(origin, candidate.transform.position);
            if (distance >= closestDistance)
                continue;

            closestMonster = candidate;
            closestDistance = distance;
        }

        return closestMonster;
    }

    private static bool MatchesMonsterId(GameObject candidate, string monsterId)
    {
        if (candidate == null || string.IsNullOrEmpty(monsterId))
            return false;

        string objectName = candidate.name.ToLowerInvariant();

        switch (monsterId)
        {
            case "M-001":
                return objectName.Contains("slime") || objectName.Contains("m001") || candidate.name.Contains("슬라임");
            case "M-002":
                return objectName.Contains("mold") || objectName.Contains("fungus") || objectName.Contains("m002") ||
                       candidate.name.Contains("곰팡");
            case "M-003":
                return objectName.Contains("fire") || objectName.Contains("m003") || candidate.name.Contains("불");
            default:
                return false;
        }
    }

    private static GameObject ResolveMonsterObject(Collider2D collider)
    {
        if (collider == null)
            return null;

        if (IsMonsterObject(collider.gameObject))
            return collider.gameObject;

        if (collider.attachedRigidbody != null && IsMonsterObject(collider.attachedRigidbody.gameObject))
            return collider.attachedRigidbody.gameObject;

        Transform parent = collider.transform.parent;
        if (parent != null && IsMonsterObject(parent.gameObject))
            return parent.gameObject;

        Transform root = collider.transform.root;
        if (root != null && IsMonsterObject(root.gameObject))
            return root.gameObject;

        return null;
    }

    private static bool IsMonsterObject(GameObject candidate)
    {
        if (candidate == null)
            return false;

        if (candidate.GetComponent<MonsterController>() != null)
            return true;

        try
        {
            if (candidate.CompareTag("Monster"))
                return true;
        }
        catch (UnityException)
        {
            // Monster 태그가 없으면 이름으로 판별합니다.
        }

        string objectName = candidate.name;
        if (string.IsNullOrEmpty(objectName))
            return false;

        string lowerObjectName = objectName.ToLowerInvariant();
        return objectName.Contains("슬라임") || objectName.Contains("곰팡") || objectName.Contains("불") ||
               lowerObjectName.Contains("slime") || lowerObjectName.Contains("m001") ||
               lowerObjectName.Contains("fungus") || lowerObjectName.Contains("mold") || lowerObjectName.Contains("m002") ||
               lowerObjectName.Contains("fire") || lowerObjectName.Contains("m003");
    }
}
