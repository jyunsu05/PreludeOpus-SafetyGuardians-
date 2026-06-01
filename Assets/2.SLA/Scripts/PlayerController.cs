using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Animator), typeof(SpriteRenderer))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5.0f;
    [Header("전투씬창 UI 연결")]
    [SerializeField] private GameObject battleSceneUI;

    [Header("기본 UI(HUD) 연결")]
    [SerializeField] private GameObject mainHUD;
    
    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    
    private Vector2 movementInput;

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

    // 몬스터와 충돌 시 전투씬창 UI 활성화
    private void OnTriggerEnter2D(Collider2D other)
    {
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