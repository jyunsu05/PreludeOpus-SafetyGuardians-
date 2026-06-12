using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class MonsterController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float detectRange = 5f;

    [Header("Separation")]
    [Tooltip("가까운 몬스터와 겹치지 않도록 밀어내는 범위")]
    [SerializeField] private float separationRadius = 0.8f;
    [Tooltip("서로 떨어지게 하는 힘의 세기")]
    [SerializeField] private float separationWeight = 1.2f;

    [Header("Battle")]
    [SerializeField] private float chasePauseAfterBattle = 1.5f;

    private Rigidbody2D rb;
    private Transform player;
    private float chaseBlockedUntil;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void Start()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
            player = playerObject.transform;
        else
            Debug.LogWarning("Player tag object was not found.");

        if (GameManager.Instance != null)
            GameManager.Instance.OnBattleEnded += OnBattleEnded;
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnBattleEnded -= OnBattleEnded;
    }

    private void OnBattleEnded()
    {
        if (rb != null)
            rb.simulated = true;

        StopMoving();
        chaseBlockedUntil = Time.time + chasePauseAfterBattle;
    }

    private void FixedUpdate()
    {
        if (IsFieldMovementFrozen() || IsInventoryPaused())
        {
            StopMoving();
            return;
        }

        if (Time.time < chaseBlockedUntil)
        {
            StopMoving();
            return;
        }

        if (player == null)
        {
            StopMoving();
            return;
        }

        float distance = Vector2.Distance(transform.position, player.position);
        if (distance > detectRange)
        {
            StopMoving();
            return;
        }

        MoveToPlayer();
    }

    private void MoveToPlayer()
    {
        Vector2 chaseDirection = ((Vector2)player.position - rb.position).normalized;
        Vector2 separationDirection = GetSeparationDirection();

        Vector2 finalDirection = (chaseDirection + separationDirection * separationWeight).normalized;
        rb.linearVelocity = finalDirection * moveSpeed;
    }

    private Vector2 GetSeparationDirection()
    {
        Collider2D[] nearbyColliders = Physics2D.OverlapCircleAll(rb.position, separationRadius);
        Vector2 pushDirection = Vector2.zero;
        int count = 0;

        foreach (Collider2D col in nearbyColliders)
        {
            if (col == null || col.gameObject == gameObject)
                continue;

            try
            {
                if (!col.CompareTag("Monster"))
                    continue;
            }
            catch (UnityException)
            {
                continue;
            }

            Vector2 awayFromOther = (Vector2)transform.position - (Vector2)col.transform.position;
            float distance = awayFromOther.magnitude;

            if (distance > 0.0001f)
            {
                pushDirection += awayFromOther / distance;
                count++;
            }
        }

        if (count == 0)
            return Vector2.zero;

        return pushDirection.normalized;
    }

    public void StopFieldMovementImmediate()
    {
        StopMoving();

        if (rb != null)
        {
            rb.angularVelocity = 0f;
            rb.simulated = false;
        }
    }

    public void PauseFieldMovementForInventory()
    {
        StopMoving();

        if (rb != null)
            rb.angularVelocity = 0f;
    }

    private static bool IsFieldMovementFrozen()
    {
        return GameManager.Instance != null && GameManager.Instance.IsFieldMovementFrozen;
    }

    private static bool IsInventoryPaused()
    {
        return GameManager.Instance != null && GameManager.Instance.IsInventoryPaused;
    }

    private void StopMoving()
    {
        if (rb == null)
            return;

        rb.linearVelocity = Vector2.zero;
    }
}
