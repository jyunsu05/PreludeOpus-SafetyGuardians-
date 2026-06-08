using System;
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
        public readonly bool isVulnerable;
        public readonly float attackMultiplier;
        public readonly float damageTakenMultiplier;

        public StatusSnapshot(int captureStacks, bool isVulnerable, float attackMultiplier, float damageTakenMultiplier)
        {
            this.captureStacks = captureStacks;
            this.isVulnerable = isVulnerable;
            this.attackMultiplier = attackMultiplier;
            this.damageTakenMultiplier = damageTakenMultiplier;
        }
    }

    [Header("--- 포획 스택 ---")]
    [SerializeField] private int captureStacks;
    [SerializeField] private int stacksPerCaptureAction = 1;
    [SerializeField] private int maxCaptureStacks = 10;

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

        captureStacks = 0;
        PublishStatus("전투 시작 — 포획 스택 0");
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
            IsVulnerable,
            GetAttackMultiplier(),
            GetContaminationDamageTakenMultiplier(damageTakenBonusPerStack));
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

        if (snapshot.captureStacks <= 0)
        {
            statusText.text = baseStatusLabel;
            return;
        }

        string stackInfo = $"포획 {snapshot.captureStacks} · 취약 x{snapshot.damageTakenMultiplier:0.##}";
        statusText.text = string.IsNullOrEmpty(baseStatusLabel)
            ? stackInfo
            : $"{baseStatusLabel} · {stackInfo}";
    }
}
