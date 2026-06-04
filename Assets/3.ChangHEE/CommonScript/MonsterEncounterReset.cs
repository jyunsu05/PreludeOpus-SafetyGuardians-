using System.Collections;
using UnityEngine;

/// <summary>
/// 도망/정화 직후 같은 몬스터와 겹쳐 있을 때 전투 UI가 바로 다시 뜨는 것을 막습니다.
/// 몬스터 프리팹에 MonsterController와 함께 붙여 주세요.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class MonsterEncounterReset : MonoBehaviour
{
    [SerializeField] private float retriggerCooldown = 1f;

    private Collider2D encounterCollider;
    private Coroutine cooldownRoutine;

    private void Awake()
    {
        // 몬스터에 콜라이더가 여러 개 있을 수 있으므로, 배틀 감지 역할을 하는 isTrigger 콜라이더를 우선적으로 찾아 지정합니다.
        Collider2D[] colliders = GetComponents<Collider2D>();
        foreach (Collider2D col in colliders)
        {
            if (col.isTrigger)
            {
                encounterCollider = col;
                break;
            }
        }

        // 만약 isTrigger 콜라이더를 못 찾았다면 첫 번째 콜라이더를 기본값으로 지정합니다.
        if (encounterCollider == null)
        {
            encounterCollider = GetComponent<Collider2D>();
        }
    }

    private void Start()
    {
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
        if (encounterCollider == null)
            return;

        if (cooldownRoutine != null)
            StopCoroutine(cooldownRoutine);

        encounterCollider.enabled = false;
        cooldownRoutine = StartCoroutine(ReenableColliderAfterCooldown());
    }

    private IEnumerator ReenableColliderAfterCooldown()
    {
        yield return new WaitForSeconds(retriggerCooldown);

        if (encounterCollider != null)
            encounterCollider.enabled = true;

        cooldownRoutine = null;
    }

    public void ForceEnableEncounterCollider()
    {
        if (cooldownRoutine != null)
        {
            StopCoroutine(cooldownRoutine);
            cooldownRoutine = null;
        }

        if (encounterCollider != null)
            encounterCollider.enabled = true;
    }

    public static void EnableAllEncounterCollidersInScene()
    {
        MonsterEncounterReset[] resets =
            FindObjectsByType<MonsterEncounterReset>(FindObjectsInactive.Include);

        for (int i = 0; i < resets.Length; i++)
        {
            if (resets[i] != null)
                resets[i].ForceEnableEncounterCollider();
        }
    }
}
