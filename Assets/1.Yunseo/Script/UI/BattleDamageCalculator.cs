using UnityEngine;

/// <summary>
/// 배틀 중 오염도/산소 피해량 계산. 밸런스 수치는 인스펙터에서 조정합니다.
/// </summary>
[System.Serializable]
public class BattleDamageCalculator
{
    [Header("--- 플레이어 정화 → 몬스터 (오염도 감소) ---")]
    [Tooltip("포획 스택 1개당 정화 피해 배율 증가. 예: 0.167 → 3스택 시 x1.5")]
    [SerializeField] private float contaminationDamageBonusPerCaptureStack = 0.167f;

    [Header("--- 몬스터 → 플레이어 (산소) ---")]
    [SerializeField] private float baseMonsterOxygenDamage = 8f;

    [Tooltip("포획 스택 1개당 몬스터 공격 배율 증가")]
    [SerializeField] private float monsterAttackBonusPerCaptureStack = 0.12f;

    public int CalculatePlayerContaminationDamage(int baseDamage, EnemyStatus enemyStatus)
    {
        if (baseDamage <= 0)
            return 0;

        float multiplier = enemyStatus != null
            ? enemyStatus.GetContaminationDamageTakenMultiplier(contaminationDamageBonusPerCaptureStack)
            : 1f;

        return Mathf.Max(1, Mathf.RoundToInt(baseDamage * multiplier));
    }

    public float CalculateMonsterOxygenDamage(EnemyStatus enemyStatus)
    {
        float multiplier = enemyStatus != null
            ? enemyStatus.GetAttackMultiplier(monsterAttackBonusPerCaptureStack)
            : 1f;

        return Mathf.Max(0f, baseMonsterOxygenDamage * multiplier);
    }

    public float GetContaminationDamageMultiplier(EnemyStatus enemyStatus)
    {
        return enemyStatus != null
            ? enemyStatus.GetContaminationDamageTakenMultiplier(contaminationDamageBonusPerCaptureStack)
            : 1f;
    }
}
