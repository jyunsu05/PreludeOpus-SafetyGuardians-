using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class MonsterController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float detectRange = 5f;

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
        StopMoving();
        chaseBlockedUntil = Time.time + chasePauseAfterBattle;
    }

    private void FixedUpdate()
    {
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
        Vector2 direction = ((Vector2)player.position - rb.position).normalized;
        rb.linearVelocity = direction * moveSpeed;
    }

    private void StopMoving()
    {
        rb.linearVelocity = Vector2.zero;
    }
}
