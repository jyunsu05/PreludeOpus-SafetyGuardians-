using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class MonsterAnimationController : MonoBehaviour
{
    [Header("Animation")]
    [SerializeField] private string isMovingParameter = "IsMoving";
    [SerializeField] private float movingThreshold = 0.01f;

    [Header("Facing")]
    [Tooltip("현재 몬스터의 기본 이미지는 왼쪽을 바라본 상태라고 가정합니다.")]
    [SerializeField] private bool defaultFacingRight = false;

    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private Animator animator;
    private bool facingRight;
    private bool hasFacingDirection;
    private int isMovingHash;
    private bool hasIsMovingParameter;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();

        facingRight = defaultFacingRight;
        hasFacingDirection = false;
        isMovingHash = Animator.StringToHash(isMovingParameter);
        hasIsMovingParameter = HasAnimatorBoolParameter(isMovingParameter);

        ApplyFacing(defaultFacingRight);
    }

    private void Update()
    {
        if (rb == null || spriteRenderer == null)
            return;

        Vector2 velocity = rb.linearVelocity;
        bool isMoving = velocity.sqrMagnitude > movingThreshold * movingThreshold;

        if (hasIsMovingParameter && animator != null)
            animator.SetBool(isMovingHash, isMoving);

        if (!isMoving)
            return;

        UpdateFacingFromVelocity(velocity);
        ApplyFacing(facingRight);
    }

    private void UpdateFacingFromVelocity(Vector2 velocity)
    {
        if (Mathf.Abs(velocity.x) <= movingThreshold)
        {
            if (!hasFacingDirection)
                facingRight = defaultFacingRight;

            return;
        }

        facingRight = velocity.x > 0f;
        hasFacingDirection = true;
    }

    private void ApplyFacing(bool faceRight)
    {
        // 현재 아트는 기본이 왼쪽 바라봄이므로, 오른쪽으로 향할 때만 flipX를 켭니다.
        spriteRenderer.flipX = faceRight;
    }

    private bool HasAnimatorBoolParameter(string parameterName)
    {
        if (animator == null || string.IsNullOrEmpty(parameterName))
            return false;

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            if (parameter.type == AnimatorControllerParameterType.Bool && parameter.name == parameterName)
                return true;
        }

        return false;
    }
}
