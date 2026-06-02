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
    
    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private Coroutine postFleeGraceRoutine;
    
    private Vector2 movementInput;
    private bool isBattleEntryLocked;
    private bool hasEnteredBattle;

    private const string MonsterIdSlime = "M-001";
    private const string MonsterIdFungus = "M-002";
    private const string MonsterIdFire = "M-003";

    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnBattleEnded -= HandleBattleEnded;
            GameManager.Instance.OnBattleEnded += HandleBattleEnded;
        }
    }

    private void Update()
    {
        // 키보드 입력을 받아옵니다.
        movementInput.x = Input.GetAxisRaw("Horizontal");
        movementInput.y = Input.GetAxisRaw("Vertical");
        movementInput = movementInput.normalized;

        // 움직이는 방향에 맞춰 캐릭터 이미지를 즉시 좌우 반전시킵니다.
        if (movementInput.x < 0)
        {
            spriteRenderer.flipX = false; // 원본 (왼쪽 바라봄)
        }
        else if (movementInput.x > 0)
        {
            spriteRenderer.flipX = true; // 가로 대칭 (오른쪽 바라봄)
        }
    }

    private void FixedUpdate()
    {
        // 물리 엔진 루프에서 요원을 부드럽게 이동시킵니다.
        rb.MovePosition(rb.position + movementInput * moveSpeed * Time.fixedDeltaTime);
    }

    /// <summary>
    /// 도망 직후 잠깐 전투 재진입을 막습니다.
    /// </summary>
    public void BeginPostFleeGraceWindow()
    {
        if (postFleeGraceRoutine != null)
            StopCoroutine(postFleeGraceRoutine);

        postFleeGraceRoutine = StartCoroutine(PostFleeGraceRoutine());
    }

    private void OnDisable()
    {
        if (postFleeGraceRoutine != null)
            StopCoroutine(postFleeGraceRoutine);

        if (GameManager.Instance != null)
            GameManager.Instance.OnBattleEnded -= HandleBattleEnded;

        postFleeGraceRoutine = null;
        isBattleEntryLocked = false;
        hasEnteredBattle = false;
    }

    // 몬스터와 충돌 시 전투씬창 UI 활성화
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isBattleEntryLocked)
            return;

        if (hasEnteredBattle)
            return;

        if (IsMonsterCollider(other))
        {
            string resolvedMonsterId = ResolveMonsterId(other);
            BattleEncounterContext.SetEncounteredMonsterId(resolvedMonsterId);

            string colliderName = other != null && other.gameObject != null ? other.gameObject.name : "(null)";
            Debug.LogWarning($"[PlayerController] 배틀 충돌 감지. collider='{colliderName}', resolvedMonsterId='{resolvedMonsterId ?? "null"}'");

            hasEnteredBattle = true;

            if (string.IsNullOrEmpty(resolvedMonsterId))
                Debug.LogWarning($"[PlayerController] 몬스터 ID 해석 실패: {other.gameObject.name}");

            if (battleSceneUI != null)
                battleSceneUI.SetActive(true);
            else
                Debug.LogWarning("[PlayerController] battleSceneUI가 연결되지 않았습니다.");

            if (mainHUD != null)
                mainHUD.SetActive(false);
        }
    }

    private void HandleBattleEnded()
    {
        hasEnteredBattle = false;
    }

    private IEnumerator PostFleeGraceRoutine()
    {
        isBattleEntryLocked = true;
        yield return new WaitForSecondsRealtime(postFleeGraceDuration);
        isBattleEntryLocked = false;
        postFleeGraceRoutine = null;
    }

    private bool IsMonsterCollider(Collider2D other)
    {
        if (other == null)
            return false;

        try
        {
            if (other.CompareTag("Monster"))
                return true;
        }
        catch (UnityException)
        {
            // Monster 태그가 아직 프로젝트에 등록되지 않은 경우를 안전하게 우회합니다.
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