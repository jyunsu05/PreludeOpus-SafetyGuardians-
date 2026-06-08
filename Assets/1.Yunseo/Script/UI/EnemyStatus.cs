using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// 전투 중 몬스터의 포획도 스택, 공격 버프, 정화 취약점 상태를 관리합니다.
/// </summary>
public class EnemyStatus : MonoBehaviour
{
    public readonly struct StatusSnapshot
    {
        public readonly int captureStacks;
        public readonly int purifyReductionStacks;
        public readonly bool isVulnerable;
        public readonly float attackMultiplier;
        public readonly float damageTakenMultiplier;
        public readonly float playerPurifyDamageMultiplier;

        public StatusSnapshot(
            int captureStacks,
            int purifyReductionStacks,
            bool isVulnerable,
            float attackMultiplier,
            float damageTakenMultiplier,
            float playerPurifyDamageMultiplier)
        {
            this.captureStacks = captureStacks;
            this.purifyReductionStacks = purifyReductionStacks;
            this.isVulnerable = isVulnerable;
            this.attackMultiplier = attackMultiplier;
            this.damageTakenMultiplier = damageTakenMultiplier;
            this.playerPurifyDamageMultiplier = playerPurifyDamageMultiplier;
        }
    }

    [Header("--- 포획 스택 ---")]
    [SerializeField] private int captureStacks;
    [SerializeField] private int stacksPerCaptureAction = 1;
    [SerializeField] private int maxCaptureStacks = 10;

    [Header("--- 정화 감소 (플레이어 정화 피해 디버프) ---")]
    [SerializeField] private int purifyReductionStacks;
    [SerializeField] private int stacksPerPurifyReductionAction = 1;
    [SerializeField] private int maxPurifyReductionStacks = 5;
    [Tooltip("스택 1개당 플레이어 정화 피해 감소율. 예: 0.1 → 스택 3이면 x0.7")]
    [SerializeField] private float purifyDamageReductionPerStack = 0.1f;
    [SerializeField] private float minPurifyDamageMultiplier = 0.5f;

    [Header("--- 밸런스 (스택당 배율) ---")]
    [Tooltip("스택 1개당 몬스터 공격력 배율 증가")]
    [SerializeField] private float attackBonusPerStack = 0.12f;

    [Tooltip("스택 1개당 플레이어 정화 피해 배율 증가 (취약점)")]
    [SerializeField] private float vulnerabilityPerStack = 0.167f;

    [Header("--- UI (선택, 1개만) ---")]
    [Tooltip("비어 있으면 UIBattleManager의 difficultyText를 자동 사용합니다.")]
    [SerializeField] private TextMeshProUGUI statusText;

    public event Action<StatusSnapshot> OnStatusChanged;
    public event Action<string> OnVulnerableLog;

    public int CaptureStacks => captureStacks;
    public int PurifyReductionStacks => purifyReductionStacks;
    public bool IsVulnerable => captureStacks > 0;

    private string baseStatusLabel = string.Empty;

    public void ConfigureStatusText(TextMeshProUGUI text, string baseLabel)
    {
        if (text != null)
            statusText = text;

        baseStatusLabel = baseLabel ?? string.Empty;
        PublishStatus();
    }

    public void ResetForBattle(string baseLabel = null)
    {
        if (baseLabel != null)
            baseStatusLabel = baseLabel;

        ResetAllBattleModifiers();
    }

    /// <summary>배틀 종료·새 전투 시작 시 포획/정화감소 스택을 모두 초기화합니다.</summary>
    public void ResetAllBattleModifiers()
    {
        captureStacks = 0;
        purifyReductionStacks = 0;
        PublishStatus("전투 상태 초기화 — 포획 0, 정화감소 0");
    }

    /// <summary>포획 스택을 0으로 되돌리고 UI를 갱신합니다.</summary>
    public void ResetCaptureStacks()
    {
        captureStacks = 0;
        PublishStatus("포획 스택 초기화 — 0");
    }

    /// <summary>몬스터 포획도 증가 행동. 공격 버프 + 정화 취약점이 함께 상승합니다.</summary>
    public bool TryIncreaseCaptureStacks(int amount = -1)
    {
        if (amount <= 0)
            amount = stacksPerCaptureAction;

        int before = captureStacks;
        captureStacks = Mathf.Min(maxCaptureStacks, captureStacks + amount);

        if (captureStacks == before)
            return false;

        float damageMultiplier = GetContaminationDamageTakenMultiplier(vulnerabilityPerStack);
        string log =
            $"포획 스택 {before} → {captureStacks}! 몬스터 강화 + 정화 취약 x{damageMultiplier:0.##}";

        PublishStatus(log);
        OnVulnerableLog?.Invoke(log);
        return true;
    }

    /// <summary>몬스터 정화 감소 행동 — 몬스터 오염도는 회복하지 않고 플레이어 정화 피해만 감소시킵니다.</summary>
    public bool TryIncreasePurifyReductionStacks(int amount = -1)
    {
        if (amount <= 0)
            amount = stacksPerPurifyReductionAction;

        int before = purifyReductionStacks;
        purifyReductionStacks = Mathf.Min(maxPurifyReductionStacks, purifyReductionStacks + amount);

        if (purifyReductionStacks == before)
            return false;

        string log =
            $"정화 감소 스택 {before} → {purifyReductionStacks}! " +
            $"플레이어 정화 피해 x{GetPlayerPurifyDamageMultiplier():0.##}";

        PublishStatus(log);
        OnVulnerableLog?.Invoke(log);
        return true;
    }

    public float GetPlayerPurifyDamageMultiplier()
    {
        float multiplier = 1f - purifyReductionStacks * purifyDamageReductionPerStack;
        return Mathf.Clamp(multiplier, minPurifyDamageMultiplier, 1f);
    }

    public float GetAttackMultiplier(float bonusPerStackOverride = -1f)
    {
        float bonus = bonusPerStackOverride >= 0f ? bonusPerStackOverride : attackBonusPerStack;
        return 1f + captureStacks * bonus;
    }

    public float GetContaminationDamageTakenMultiplier(float bonusPerStackOverride = -1f)
    {
        float bonus = bonusPerStackOverride >= 0f ? bonusPerStackOverride : vulnerabilityPerStack;
        return 1f + captureStacks * bonus;
    }

    public StatusSnapshot CreateSnapshot(float damageTakenBonusPerStack = -1f)
    {
        return new StatusSnapshot(
            captureStacks,
            purifyReductionStacks,
            IsVulnerable,
            GetAttackMultiplier(),
            GetContaminationDamageTakenMultiplier(damageTakenBonusPerStack),
            GetPlayerPurifyDamageMultiplier());
    }

    private void PublishStatus(string logMessage = null)
    {
        StatusSnapshot snapshot = CreateSnapshot();
        UpdateVisuals(snapshot);
        OnStatusChanged?.Invoke(snapshot);

        if (!string.IsNullOrEmpty(logMessage))
            Debug.Log($"[EnemyStatus] {logMessage}");
    }

    private void UpdateVisuals(StatusSnapshot snapshot)
    {
        if (statusText == null)
            return;

        if (snapshot.captureStacks <= 0 && snapshot.purifyReductionStacks <= 0)
        {
            statusText.text = baseStatusLabel;
            return;
        }

        var parts = new List<string>();
        if (snapshot.captureStacks > 0)
            parts.Add($"포획 {snapshot.captureStacks} · 취약 x{snapshot.damageTakenMultiplier:0.##}");
        if (snapshot.purifyReductionStacks > 0)
            parts.Add($"정화감소 {snapshot.purifyReductionStacks} · 피해 x{snapshot.playerPurifyDamageMultiplier:0.##}");

        string stackInfo = string.Join(" · ", parts);
        statusText.text = string.IsNullOrEmpty(baseStatusLabel)
            ? stackInfo
            : $"{baseStatusLabel} · {stackInfo}";
    }
}
