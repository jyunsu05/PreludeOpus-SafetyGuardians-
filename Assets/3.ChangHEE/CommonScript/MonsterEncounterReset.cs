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
        encounterCollider = GetComponent<Collider2D>();
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
}
