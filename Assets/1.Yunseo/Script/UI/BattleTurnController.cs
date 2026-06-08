using System;
using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// 산소/오염도 기반 턴제 배틀 상태 머신. PLAYER_TURN ↔ MONSTER_TURN을 제어합니다.
/// </summary>
public class BattleTurnController : MonoBehaviour
{
    public enum BattleTurnPhase
    {
        PlayerTurn,
        MonsterTurn
    }

    public enum MonsterActionType
    {
        AttackPlayer,
        IncreaseCaptureRate
    }

    [Header("--- 연동 ---")]
    [SerializeField] private UIBattleManager battleManager;
    [SerializeField] private EnemyStatus enemyStatus;
    [SerializeField] private TextMeshProUGUI battleLogText;

    [Header("--- 데미지 계산 ---")]
    [SerializeField] private BattleDamageCalculator damageCalculator = new BattleDamageCalculator();

    [Header("--- 플레이어 정화 보상 ---")]
    [SerializeField] private float playerPurifySuccessOxygenReward = 30f;

    [Header("--- 몬스터 AI 확률 (합계 100 권장) ---")]
    [SerializeField] private int weightAttack = 50;
    [SerializeField] private int weightCaptureUp = 50;

    [Header("--- 연출 ---")]
    [SerializeField] private float monsterTurnDelay = 0.6f;

    public event Action<BattleTurnPhase> OnTurnPhaseChanged;
    public event Action<string> OnBattleLog;

    public BattleTurnPhase CurrentPhase { get; private set; } = BattleTurnPhase.PlayerTurn;
    public EnemyStatus EnemyStatus => enemyStatus;
    public BattleDamageCalculator DamageCalculator => damageCalculator;
    public bool IsPlayerTurn => CurrentPhase == BattleTurnPhase.PlayerTurn;
    public bool IsResolvingTurn { get; private set; }

    private PlayerOxygen playerOxygen;
    private Coroutine monsterTurnRoutine;
    private bool isBattleActive;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnDisable()
    {
        if (enemyStatus != null)
            enemyStatus.OnVulnerableLog -= HandleVulnerableLog;

        StopMonsterTurnRoutine();
        isBattleActive = false;
        IsResolvingTurn = false;
    }

    public void BindPlayerOxygen(PlayerOxygen preferred)
    {
        if (preferred != null)
            playerOxygen = preferred;
    }

    public void BeginBattle()
    {
        ResolveReferences();
        ResolvePlayerOxygen();
        StopMonsterTurnRoutine();

        if (enemyStatus != null)
        {
            enemyStatus.OnVulnerableLog -= HandleVulnerableLog;
            enemyStatus.OnVulnerableLog += HandleVulnerableLog;
            string baseLabel = battleManager != null ? battleManager.GetDifficultyDisplayText() : string.Empty;
            enemyStatus.ResetForBattle(baseLabel);
        }

        isBattleActive = true;
        IsResolvingTurn = false;
        SetPhase(BattleTurnPhase.PlayerTurn);
        LogAction("전투 시작 — 플레이어 턴");
    }

    public int CalculateAmplifiedContaminationDamage(int baseDamage)
    {
        return damageCalculator.CalculatePlayerContaminationDamage(baseDamage, enemyStatus);
    }

    public bool TryResolvePlayerPurify(int baseDamage, int finalDamage)
    {
        if (!CanAcceptPlayerAction())
            return false;

        if (finalDamage <= 0)
            return false;

        IsResolvingTurn = true;

        float multiplier = damageCalculator.GetContaminationDamageMultiplier(enemyStatus);
        if (multiplier > 1.01f)
            LogAction($"플레이어 정화! 오염도 -{finalDamage} (기본 {baseDamage} x{multiplier:0.##} 취약)");
        else
            LogAction($"플레이어 정화! 오염도 -{finalDamage}");

        if (!isBattleActive)
        {
            IsResolvingTurn = false;
            return true;
        }

        EndPlayerTurn();
        return true;
    }

    private void EndPlayerTurn()
    {
        if (!isBattleActive)
        {
            IsResolvingTurn = false;
            return;
        }

        SetPhase(BattleTurnPhase.MonsterTurn);
        monsterTurnRoutine = StartCoroutine(MonsterTurnRoutine());
    }

    private IEnumerator MonsterTurnRoutine()
    {
        LogAction("몬스터 턴");
        yield return new WaitForSeconds(monsterTurnDelay);

        if (!isBattleActive)
        {
            IsResolvingTurn = false;
            yield break;
        }

        MonsterActionType action = PickRandomMonsterAction();
        ExecuteMonsterAction(action);

        IsResolvingTurn = false;

        if (!isBattleActive)
            yield break;

        SetPhase(BattleTurnPhase.PlayerTurn);
        LogAction("플레이어 턴");
    }

    public MonsterActionType PickRandomMonsterAction()
    {
        int totalWeight = Mathf.Max(1, weightAttack + weightCaptureUp);
        int roll = UnityEngine.Random.Range(0, totalWeight);

        if (roll < weightAttack)
            return MonsterActionType.AttackPlayer;

        return MonsterActionType.IncreaseCaptureRate;
    }

    private void ExecuteMonsterAction(MonsterActionType action)
    {
        switch (action)
        {
            case MonsterActionType.AttackPlayer:
                ExecuteMonsterAttack();
                break;
            case MonsterActionType.IncreaseCaptureRate:
                ExecuteMonsterCaptureRateUp();
                break;
        }
    }

    private void ExecuteMonsterAttack()
    {
        float damage = damageCalculator.CalculateMonsterOxygenDamage(enemyStatus);
        int stacks = enemyStatus != null ? enemyStatus.CaptureStacks : 0;
        ApplyOxygenDamage(damage);

        LogAction($"몬스터 공격! 산소 -{damage:0.#} (포획 스택 {stacks})");
        Debug.Log($"[BattleTurnController] Monster Attack — oxygen -{damage:0.#}, stacks={stacks}");
    }

    private void ExecuteMonsterCaptureRateUp()
    {
        if (enemyStatus == null)
        {
            LogAction("몬스터 포획도 상승 — EnemyStatus 미연결");
            return;
        }

        if (!enemyStatus.TryIncreaseCaptureStacks())
            LogAction("몬스터 포획 스택 — 이미 최대치");
    }

    private bool CanAcceptPlayerAction()
    {
        return isBattleActive && IsPlayerTurn && !IsResolvingTurn;
    }

    private void ApplyOxygenDamage(float amount)
    {
        ResolvePlayerOxygen();

        if (playerOxygen == null)
        {
            Debug.LogWarning("[BattleTurnController] PlayerOxygen 없음 — 몬스터 공격 산소 차감 생략");
            return;
        }

        bool survived = playerOxygen.ApplyBattleOxygenCost(amount);
        if (!survived)
        {
            isBattleActive = false;
            LogAction("산소 고갈 — 패배");
        }
    }

    public void NotifyBattleWon()
    {
        isBattleActive = false;
        StopMonsterTurnRoutine();
        GrantPurifySuccessOxygenReward();
        LogAction($"오염도 0 — 승리! 산소 +{playerPurifySuccessOxygenReward:0.#}");
    }

    private void GrantPurifySuccessOxygenReward()
    {
        if (playerPurifySuccessOxygenReward <= 0f)
            return;

        ResolvePlayerOxygen();
        if (playerOxygen == null)
        {
            Debug.LogWarning("[BattleTurnController] PlayerOxygen 없음 — 정화 성공 산소 회복 생략");
            return;
        }

        playerOxygen.ApplyBattleOxygenRestore(playerPurifySuccessOxygenReward);
    }

    public void NotifyBattleEnded()
    {
        isBattleActive = false;
        StopMonsterTurnRoutine();
        IsResolvingTurn = false;
    }

    private void SetPhase(BattleTurnPhase phase)
    {
        CurrentPhase = phase;
        OnTurnPhaseChanged?.Invoke(phase);
    }

    private void StopMonsterTurnRoutine()
    {
        if (monsterTurnRoutine != null)
        {
            StopCoroutine(monsterTurnRoutine);
            monsterTurnRoutine = null;
        }
    }

    private void ResolveReferences()
    {
        if (battleManager == null)
            battleManager = GetComponent<UIBattleManager>();

        if (battleManager == null)
            battleManager = FindAnyObjectByType<UIBattleManager>(FindObjectsInactive.Include);

        if (enemyStatus == null)
            enemyStatus = GetComponent<EnemyStatus>();

        if (enemyStatus == null && battleManager != null)
            enemyStatus = battleManager.GetComponent<EnemyStatus>();

        if (enemyStatus == null)
            enemyStatus = FindAnyObjectByType<EnemyStatus>(FindObjectsInactive.Include);
    }

    private void ResolvePlayerOxygen()
    {
        if (!IsSceneInstance(playerOxygen))
            playerOxygen = PlayerOxygen.ResolveRuntime();
    }

    private static bool IsSceneInstance(PlayerOxygen oxygen)
    {
        return oxygen != null && oxygen.gameObject.scene.IsValid();
    }

    private void HandleVulnerableLog(string message)
    {
        LogAction(message);
    }

    private void LogAction(string message)
    {
        Debug.Log($"[BattleTurnController] {message}");
        OnBattleLog?.Invoke(message);

        if (battleLogText != null)
            battleLogText.text = message;
    }
}
