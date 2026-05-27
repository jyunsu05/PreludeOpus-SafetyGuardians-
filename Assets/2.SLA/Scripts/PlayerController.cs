using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Animator), typeof(SpriteRenderer))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5.0f;
    
    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    
    private Vector2 movementInput;

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
}