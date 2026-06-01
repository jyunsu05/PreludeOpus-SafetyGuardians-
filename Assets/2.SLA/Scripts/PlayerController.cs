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

    private const string MonsterIdSlime = "M-001";
    private const string MonsterIdFungus = "M-002";
    private const string MonsterIdFire = "M-003";

    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
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

        postFleeGraceRoutine = null;
        isBattleEntryLocked = false;
    }

    // 몬스터와 충돌 시 전투씬창 UI 활성화
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isBattleEntryLocked)
            return;

        if (IsMonsterCollider(other))
        {
            BattleEncounterContext.SetEncounteredMonsterId(ResolveMonsterId(other));

            if (battleSceneUI != null)
                battleSceneUI.SetActive(true);
            else
                Debug.LogWarning("[PlayerController] battleSceneUI가 연결되지 않았습니다.");

            if (mainHUD != null)
                mainHUD.SetActive(false);
        }
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

        string objectName = other.gameObject.name;
        return !string.IsNullOrEmpty(objectName) &&
               (objectName.Contains("슬라임") || objectName.Contains("곰팡") || objectName.Contains("불") ||
                objectName.ToLowerInvariant().Contains("slime") || objectName.ToLowerInvariant().Contains("fungus") || objectName.ToLowerInvariant().Contains("fire"));
    }

    private string ResolveMonsterId(Collider2D other)
    {
        if (other == null)
            return null;

        string objectName = other.gameObject.name;
        if (string.IsNullOrEmpty(objectName))
            return null;

        string lower = objectName.ToLowerInvariant();

        if (objectName.Contains("슬라임") || lower.Contains("slime"))
            return MonsterIdSlime;

        if (objectName.Contains("곰팡") || lower.Contains("fungus"))
            return MonsterIdFungus;

        if (objectName.Contains("불") || lower.Contains("fire"))
            return MonsterIdFire;

        return null;
    }
}