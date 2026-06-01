using UnityEngine;

/// <summary>
/// 플레이어와 충돌한 몬스터를 MonsterBattleTracker에 등록합니다.
/// 몬스터 프리팹에 MonsterController와 함께 붙여 주세요.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class MonsterBattleRegistration : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        TryRegister(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryRegister(other);
    }

    private void TryRegister(Collider2D other)
    {
        if (!other.CompareTag("Player"))
            return;

        if (MonsterBattleTracker.Instance != null)
            MonsterBattleTracker.Instance.RegisterBattleMonster(gameObject);
    }
}
