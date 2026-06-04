using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Animator), typeof(SpriteRenderer))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5.0f;

    [Header("전투씬창 UI 연결")]
    [SerializeField] private GameObject battleSceneUI;

    [Header("기본 UI(HUD) 연결")]
    [SerializeField] private GameObject mainHUD;

    [Header("도망 후 재진입 방지")]
    [Tooltip("도망 직후 다시 전투에 들어가지 않도록 잠깐 막는 시간")]
    [SerializeField] private float postFleeGraceDuration = 0.75f;

    [Tooltip("전투가 끝난 뒤 이 거리 이상 벗어나야 다시 전투에 들어갈 수 있습니다.")]
    [SerializeField] private float postBattleReentryDistance = 1.0f;

    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private Coroutine postFleeGraceRoutine;

    private Vector2 movementInput;
    private bool isBattleEntryLocked;
    private bool hasEnteredBattle;
    private Vector2 lastBattleEndedPosition;
    private bool hasLastBattleEndedPosition;
    private bool isGameManagerSubscribed;
    // 전투 종료 직후 물리 복구가 필요한 상태인지 추적합니다.
    // Watchdog이 이 플래그가 true일 때만 작동하도록 범위를 좁혀,
    // 나중에 NPC 대화창·일시정지 메뉴 등이 추가되어도 물리를 강제로 켜버리는 부작용을 방지합니다.
    private bool isWaitingForPostBattlePhysicsRecovery;

    private const string MonsterIdSlime = "M-001";
    private const string MonsterIdFungus = "M-002";
    private const string MonsterIdFire = "M-003";

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void OnEnable()
    {
        TrySubscribeGameManager();
        EnsurePhysicsSimulated();
    }

    private void Start()
    {
        TrySubscribeGameManager();
        EnsurePhysicsSimulated();
    }

    private void OnDisable()
    {
        if (postFleeGraceRoutine != null)
            StopCoroutine(postFleeGraceRoutine);

        postFleeGraceRoutine = null;
        UnsubscribeGameManager();
    }

    private void OnDestroy()
    {
        UnsubscribeGameManager();
    }

    private void TrySubscribeGameManager()
    {
        if (isGameManagerSubscribed || GameManager.Instance == null)
            return;

        GameManager.Instance.OnBattleEnded -= HandleBattleEnded;
        GameManager.Instance.OnBattleEnded += HandleBattleEnded;
        isGameManagerSubscribed = true;
    }

    private void UnsubscribeGameManager()
    {
        if (!isGameManagerSubscribed || GameManager.Instance == null)
            return;

        GameManager.Instance.OnBattleEnded -= HandleBattleEnded;
        isGameManagerSubscribed = false;
    }

    private void Update()
    {
        if (!isGameManagerSubscribed)
            TrySubscribeGameManager();

        if (IsBattleActive() || IsFieldMovementFrozen())
        {
            movementInput = Vector2.zero;
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
                rb.simulated = false;
            }

            return;
        }

        // --- 물리 복구 감시자 (Watchdog) ---
        // 전투가 막 끝난 직후에만 작동합니다. 물리 복구가 확인되면 즉시 감시를 종료합니다.
        // (항상 작동하게 두면 NPC 대화창·일시정지 메뉴 등이 rb.simulated를 꺼도
        //  강제로 다시 켜버려 플레이어가 멋대로 움직이는 부작용이 생깁니다.)
        if (isWaitingForPostBattlePhysicsRecovery && rb != null)
        {
            if (!rb.simulated)
            {
                Debug.LogWarning("[PlayerController] 물리 시뮬레이션 복구됨 (Watchdog).");
                rb.simulated = true;
            }
            isWaitingForPostBattlePhysicsRecovery = false;
        }

        movementInput.x = Input.GetAxisRaw("Horizontal");
        movementInput.y = Input.GetAxisRaw("Vertical");
        movementInput = movementInput.normalized;

        if (movementInput.x < 0)
            spriteRenderer.flipX = false;
        else if (movementInput.x > 0)
            spriteRenderer.flipX = true;
    }

    private void FixedUpdate()
    {
        if (IsFieldMovementFrozen() || IsBattleActive())
            return;

        if (rb != null && rb.simulated)
            rb.MovePosition(rb.position + movementInput * moveSpeed * Time.fixedDeltaTime);
    }

    /// <summary>게임오버 시 즉시 이동·입력을 멈춥니다.</summary>
    public void StopFieldMovementImmediate()
    {
        movementInput = Vector2.zero;

        if (rb == null)
            return;

        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.simulated = false;
    }

    public void BeginPostFleeGraceWindow()
    {
        if (postFleeGraceRoutine != null)
            StopCoroutine(postFleeGraceRoutine);

        postFleeGraceRoutine = StartCoroutine(PostFleeGraceRoutine());
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!CanStartBattleFromCollision(other))
            return;

        string resolvedMonsterId = ResolveMonsterId(other);
        BattleEncounterContext.SetEncounteredMonsterId(resolvedMonsterId);

        string colliderName = other != null && other.gameObject != null ? other.gameObject.name : "(null)";
        Debug.Log($"[PlayerController] 배틀 충돌 감지. collider='{colliderName}', resolvedMonsterId='{resolvedMonsterId ?? "null"}'");

        hasEnteredBattle = true;

        if (string.IsNullOrEmpty(resolvedMonsterId))
            Debug.LogWarning($"[PlayerController] 몬스터 ID 해석 실패: {other.gameObject.name}");

        if (GameManager.Instance != null)
            GameManager.Instance.EnterBattle();

        if (battleSceneUI != null)
            battleSceneUI.SetActive(true);
        else
            Debug.LogWarning("[PlayerController] battleSceneUI가 연결되지 않았습니다.");

        if (mainHUD != null)
            mainHUD.SetActive(false);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (IsBattleActive() || IsFieldMovementFrozen())
            return;

        if (hasEnteredBattle && !IsBattleUiVisible())
            hasEnteredBattle = false;

        if (hasEnteredBattle || isBattleEntryLocked)
            return;

        if (!IsMonsterCollider(other))
            return;

        if (hasLastBattleEndedPosition && rb != null)
        {
            float movedDistance = Vector2.Distance(rb.position, lastBattleEndedPosition);
            if (movedDistance < postBattleReentryDistance)
                return;
        }

        OnTriggerEnter2D(other);
    }

    private bool CanStartBattleFromCollision(Collider2D other)
    {
        if (IsBattleActive() || IsFieldMovementFrozen())
            return false;

        if (hasEnteredBattle && !IsBattleUiVisible())
            hasEnteredBattle = false;

        if (isBattleEntryLocked || hasEnteredBattle)
            return false;

        if (!IsMonsterCollider(other))
            return false;

        if (other == null || !other.isTrigger)
            return false;

        if (hasLastBattleEndedPosition && rb != null)
        {
            float movedDistance = Vector2.Distance(rb.position, lastBattleEndedPosition);
            if (movedDistance < postBattleReentryDistance)
                return false;
        }

        return true;
    }

    /// <summary>게임오버·처음부터 다시 시작·챕터 리셋 후 필드 전투 진입 상태를 초기화합니다.</summary>
    public void ResetFieldBattleEntryState()
    {
        if (postFleeGraceRoutine != null)
        {
            StopCoroutine(postFleeGraceRoutine);
            postFleeGraceRoutine = null;
        }

        hasEnteredBattle = false;
        isBattleEntryLocked = false;
        hasLastBattleEndedPosition = false;
        isWaitingForPostBattlePhysicsRecovery = false;
        EnsurePhysicsSimulated();

        if (battleSceneUI != null && battleSceneUI.activeSelf)
            battleSceneUI.SetActive(false);

        if (mainHUD != null && !mainHUD.activeSelf)
            mainHUD.SetActive(true);
    }

    public static void ResetAllFieldBattleEntryStates()
    {
        PlayerController[] controllers =
            FindObjectsByType<PlayerController>(FindObjectsInactive.Include);

        for (int i = 0; i < controllers.Length; i++)
        {
            if (controllers[i] != null)
                controllers[i].ResetFieldBattleEntryState();
        }
    }

    private void HandleBattleEnded()
    {
        hasEnteredBattle = false;
        EnsurePhysicsSimulated();

        if (rb != null)
        {
            lastBattleEndedPosition = rb.position;
            hasLastBattleEndedPosition = true;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;

            // 전투 종료 직후 물리 복구 감시 대기 상태로 진입합니다.
            isWaitingForPostBattlePhysicsRecovery = true;
        }
        else
        {
            lastBattleEndedPosition = transform.position;
            hasLastBattleEndedPosition = true;
        }

        if (battleSceneUI != null && battleSceneUI.activeSelf)
            battleSceneUI.SetActive(false);

        if (mainHUD != null && !mainHUD.activeSelf)
            mainHUD.SetActive(true);
    }

    private IEnumerator PostFleeGraceRoutine()
    {
        isBattleEntryLocked = true;
        yield return new WaitForSecondsRealtime(postFleeGraceDuration);
        isBattleEntryLocked = false;
        postFleeGraceRoutine = null;
    }

    private bool IsBattleActive()
    {
        if (GameManager.Instance != null)
            return GameManager.Instance.IsInBattle;

        return IsBattleUiVisible();
    }

    private static bool IsFieldMovementFrozen()
    {
        return GameManager.Instance != null && GameManager.Instance.IsFieldMovementFrozen;
    }

    private bool IsBattleUiVisible()
    {
        if (battleSceneUI != null && battleSceneUI.activeInHierarchy)
            return true;

        if (UIManager.Instance != null && UIManager.Instance.IsBattleUiVisible())
            return true;

        return false;
    }

    private void EnsurePhysicsSimulated()
    {
        if (rb == null)
            return;

        if (!rb.simulated)
        {
            rb.simulated = true;
            Debug.LogWarning("[PlayerController] Rigidbody2D.simulated 복구됨.");
        }
    }

    private bool IsMonsterCollider(Collider2D other)
    {
        if (other == null)
            return false;

        // 아이템 픽업 컴포넌트가 붙어 있는 오브젝트는 이름과 무관하게 절대 몬스터가 아닙니다.
        // 이 검사가 없으면 "슬라임 해독제", "불꽃 소화기"처럼 이름에 몬스터 키워드가 들어간
        // 아이템을 몬스터로 오인해 hasEnteredBattle이 true로 굳어버리는 버그가 발생합니다.
        if (other.GetComponent<ItemPickup>() != null)
            return false;

        try
        {
            if (other.CompareTag("Monster"))
                return true;
        }
        catch (UnityException)
        {
        }

        return TryResolveMonsterNameHint(other, out string _);
    }

    private string ResolveMonsterId(Collider2D other)
    {
        if (!TryResolveMonsterNameHint(other, out string objectName))
            return null;

        string lower = objectName.ToLowerInvariant();

        if (objectName.Contains("슬라임") || lower.Contains("slime") || lower.Contains("m001"))
            return MonsterIdSlime;

        if (objectName.Contains("곰팡") || lower.Contains("fungus") || lower.Contains("mold") || lower.Contains("m002"))
            return MonsterIdFungus;

        if (objectName.Contains("불") || lower.Contains("fire") || lower.Contains("m003"))
            return MonsterIdFire;

        return null;
    }

    private bool TryResolveMonsterNameHint(Collider2D other, out string monsterNameHint)
    {
        monsterNameHint = null;
        if (other == null)
            return false;

        if (TryGetMatchedMonsterName(other.gameObject, out monsterNameHint))
            return true;

        if (other.attachedRigidbody != null && TryGetMatchedMonsterName(other.attachedRigidbody.gameObject, out monsterNameHint))
            return true;

        Transform tr = other.transform;
        if (tr != null)
        {
            if (tr.parent != null && TryGetMatchedMonsterName(tr.parent.gameObject, out monsterNameHint))
                return true;

            Transform root = tr.root;
            if (root != null && TryGetMatchedMonsterName(root.gameObject, out monsterNameHint))
                return true;
        }

        return false;
    }

    private bool TryGetMatchedMonsterName(GameObject candidate, out string matchedName)
    {
        matchedName = null;
        if (candidate == null)
            return false;

        string objectName = candidate.name;
        if (string.IsNullOrEmpty(objectName))
            return false;

        string lowerObjectName = objectName.ToLowerInvariant();
        bool isMonsterName = objectName.Contains("슬라임") || objectName.Contains("곰팡") || objectName.Contains("불") ||
                             lowerObjectName.Contains("slime") || lowerObjectName.Contains("m001") ||
                             lowerObjectName.Contains("fungus") || lowerObjectName.Contains("mold") || lowerObjectName.Contains("m002") ||
                             lowerObjectName.Contains("fire") || lowerObjectName.Contains("m003");

        if (!isMonsterName)
            return false;

        matchedName = objectName;
        return true;
    }
}
