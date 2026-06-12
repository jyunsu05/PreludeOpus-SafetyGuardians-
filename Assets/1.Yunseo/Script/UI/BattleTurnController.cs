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

        RestoreContamination,

        IncreaseCaptureRate,

        DoNothing

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

    [SerializeField] private int weightAttack = 25;

    [SerializeField] private int weightRestoreContamination = 25;

    [SerializeField] private int weightCaptureUp = 25;

    [SerializeField] private int weightDoNothing = 25;



    [Header("--- 연출 ---")]

    [SerializeField] private float monsterTurnDelay = 0.6f;

    [Header("--- 피드백 로그 ---")]
    [Tooltip("정화 불가 등 플레이어 피드백 메시지 최소 표시 시간(초). 턴 로그는 이후에 이어서 표시됩니다.")]
    [SerializeField] private float playerFeedbackMinDisplayDuration = 4f;



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

    private float feedbackProtectedUntil;

    private string pendingBattleLogMessage;

    private Coroutine feedbackHoldRoutine;



    private void Awake()

    {

        ResolveReferences();

    }



    private void OnDisable()

    {

        StopMonsterTurnRoutine();

        ClearFeedbackHoldState();

        isBattleActive = false;

        IsResolvingTurn = false;

        ResetEnemyStatusAfterBattle();

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

            string baseLabel = battleManager != null ? battleManager.GetDifficultyDisplayText() : string.Empty;

            enemyStatus.ResetForBattle(baseLabel);

        }



        ClearFeedbackHoldState();

        isBattleActive = true;

        IsResolvingTurn = false;

        SetPhase(BattleTurnPhase.PlayerTurn);

        LogTurnPhase("플레이어 턴");

    }



    public bool CanPlayerUseItem() => CanAcceptPlayerAction();



    /// <summary>아이템 사용 시작 시 중복 클릭을 막기 위해 플레이어 행동을 잠급니다.</summary>

    public bool TryLockPlayerAction()

    {

        if (!CanAcceptPlayerAction())

            return false;



        IsResolvingTurn = true;

        return true;

    }



    public void ReleasePlayerActionLock()

    {

        if (CurrentPhase == BattleTurnPhase.PlayerTurn)

            IsResolvingTurn = false;

    }



    public int CalculateAmplifiedContaminationDamage(int baseDamage)

    {

        return damageCalculator.CalculatePlayerContaminationDamage(baseDamage, enemyStatus);

    }



    public bool TryResolvePlayerPurify(int baseDamage, int finalDamage)

    {

        if (finalDamage <= 0 || !isBattleActive)

            return false;



        CommitPlayerPurifyTurn(baseDamage, finalDamage);

        return true;

    }



    /// <summary>정화/아이템 효과 적용 직후 플레이어 턴을 종료하고 몬스터 턴으로 넘깁니다.</summary>

    public void CommitPlayerPurifyTurn(int baseDamage, int finalDamage)

    {

        if (finalDamage <= 0 || !isBattleActive)

        {

            ReleasePlayerActionLock();

            return;

        }



        if (IsBattleResolved())

        {

            ReleasePlayerActionLock();

            return;

        }



        float multiplier = damageCalculator.GetContaminationDamageMultiplier(enemyStatus);

        if (multiplier > 1.01f)

            LogAction($"플레이어 정화 — 오염도 -{finalDamage} (기본 {baseDamage} x{multiplier:0.##})");

        else

            LogAction($"플레이어 정화 — 오염도 -{finalDamage}");



        EndPlayerTurn();

    }



    /// <summary>산소 회복 등 보조 아이템 사용 후 플레이어 턴을 종료합니다.</summary>

    public void CommitPlayerSupportItem(string itemId, int effectPower)

    {

        if (!isBattleActive)

        {

            ReleasePlayerActionLock();

            return;

        }



        LogAction($"아이템 사용 — {itemId} (+{effectPower})");

        EndPlayerTurn();

    }



    private void EndPlayerTurn()

    {

        if (!isBattleActive)

        {

            IsResolvingTurn = false;

            return;

        }



        if (IsBattleResolved())

        {

            IsResolvingTurn = false;

            return;

        }



        SetPhase(BattleTurnPhase.MonsterTurn);

        monsterTurnRoutine = StartCoroutine(MonsterTurnRoutine());

    }



    private IEnumerator MonsterTurnRoutine()

    {

        LogTurnPhase("몬스터 턴");

        yield return new WaitForSecondsRealtime(monsterTurnDelay);



        if (IsBattleResolved())

        {

            IsResolvingTurn = false;

            yield break;

        }



        MonsterActionType action = PickRandomMonsterAction();

        if (!IsBattleResolved())
        {
            if (action == MonsterActionType.AttackPlayer)
                yield return ExecuteMonsterAttackRoutine();
            else
                ExecuteMonsterAction(action);
        }



        yield return new WaitForSecondsRealtime(monsterTurnDelay);



        IsResolvingTurn = false;



        if (IsBattleResolved())

            yield break;



        SetPhase(BattleTurnPhase.PlayerTurn);

        LogTurnPhase("플레이어 턴");

    }



    public MonsterActionType PickRandomMonsterAction()

    {

        int totalWeight = Mathf.Max(
            1,
            weightAttack + weightRestoreContamination + weightCaptureUp + weightDoNothing);

        int roll = UnityEngine.Random.Range(0, totalWeight);



        if (roll < weightAttack)

            return MonsterActionType.AttackPlayer;



        roll -= weightAttack;

        if (roll < weightRestoreContamination)

            return MonsterActionType.RestoreContamination;



        roll -= weightRestoreContamination;

        if (roll < weightCaptureUp)

            return MonsterActionType.IncreaseCaptureRate;



        return MonsterActionType.DoNothing;

    }



    private void ExecuteMonsterAction(MonsterActionType action)

    {

        if (IsBattleResolved())

            return;



        switch (action)

        {

            case MonsterActionType.AttackPlayer:

                ApplyMonsterAttackDamage();

                break;

            case MonsterActionType.RestoreContamination:

                ExecuteMonsterPurifyDamageReduction();

                break;

            case MonsterActionType.IncreaseCaptureRate:

                ExecuteMonsterCaptureRateUp();

                break;

            case MonsterActionType.DoNothing:

                ExecuteMonsterDoNothing();

                break;

        }

    }



    private IEnumerator ExecuteMonsterAttackRoutine()

    {

        if (IsBattleResolved())

            yield break;



        if (battleManager != null)

            yield return battleManager.PlayPlayerHitEffectRoutine();

        else

            yield return new WaitForSecondsRealtime(0.65f);



        if (IsBattleResolved())

            yield break;



        ApplyMonsterAttackDamage();

    }



    private void ApplyMonsterAttackDamage()

    {

        if (IsBattleResolved())

            return;



        float damage = damageCalculator.CalculateMonsterOxygenDamage(enemyStatus);

        int stacks = enemyStatus != null ? enemyStatus.CaptureStacks : 0;

        ApplyOxygenDamage(damage);



        LogMonsterAction(MonsterActionType.AttackPlayer, $"산소 -{damage:0.#} (포획 스택 {stacks})");

        Debug.Log($"[BattleTurnController] Monster Attack — oxygen -{damage:0.#}, stacks={stacks}");

    }



    private void ExecuteMonsterDoNothing()

    {

        if (IsBattleResolved())

            return;



        LogMonsterAction(MonsterActionType.DoNothing, "행동 없음");

    }



    private void ExecuteMonsterPurifyDamageReduction()

    {

        if (IsBattleResolved())

            return;



        if (enemyStatus == null)

        {

            LogMonsterAction(MonsterActionType.RestoreContamination, "EnemyStatus 미연결");

            return;

        }



        if (!enemyStatus.TryIncreasePurifyReductionStacks())

        {

            LogMonsterAction(MonsterActionType.RestoreContamination, "이미 최대치");

            return;

        }



        LogMonsterAction(

            MonsterActionType.RestoreContamination,

            $"플레이어 정화 피해 x{enemyStatus.GetPlayerPurifyDamageMultiplier():0.##}");

    }



    private void ExecuteMonsterCaptureRateUp()

    {

        if (IsBattleResolved())

            return;



        if (enemyStatus == null)

        {

            LogMonsterAction(MonsterActionType.IncreaseCaptureRate, "EnemyStatus 미연결");

            return;

        }



        if (!enemyStatus.TryIncreaseCaptureStacks())

        {

            LogMonsterAction(MonsterActionType.IncreaseCaptureRate, "이미 최대치");

            return;

        }



        float damageMultiplier = enemyStatus.GetContaminationDamageTakenMultiplier();

        LogMonsterAction(

            MonsterActionType.IncreaseCaptureRate,

            $"포획 스택 {enemyStatus.CaptureStacks}, 정화 취약 x{damageMultiplier:0.##}");

    }



    private bool CanAcceptPlayerAction()

    {

        return isBattleActive && IsPlayerTurn && !IsResolvingTurn && !IsBattleResolved();

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

        ResetEnemyStatusAfterBattle();

    }



    private bool IsBattleResolved()

    {

        return !isBattleActive || (battleManager != null && battleManager.HasBattleWon);

    }



    private void ResetEnemyStatusAfterBattle()

    {

        if (battleManager != null)

        {

            battleManager.ResetBattleSessionState();

            return;

        }



        if (enemyStatus != null)

            enemyStatus.ResetForBattle();

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



        IsResolvingTurn = false;

    }



    private void ResolveReferences()

    {

        if (battleManager == null || !battleManager.isActiveAndEnabled)

            battleManager = GetComponent<UIBattleManager>();



        if (battleManager == null || !battleManager.isActiveAndEnabled)

            battleManager = GetComponentInParent<UIBattleManager>();



        if (battleManager == null || !battleManager.isActiveAndEnabled)

        {

            UIBattleManager[] managers = FindObjectsByType<UIBattleManager>(FindObjectsInactive.Include);

            for (int i = 0; i < managers.Length; i++)

            {

                UIBattleManager candidate = managers[i];

                if (candidate != null && candidate.isActiveAndEnabled)

                {

                    battleManager = candidate;

                    break;

                }

            }

        }



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



    private void LogTurnPhase(string phaseName)

    {

        LogAction($"▶ {phaseName}");

    }



    private void LogMonsterAction(MonsterActionType action, string detail)

    {

        LogAction($"몬스터 행동: {GetMonsterActionDisplayName(action)} — {detail}");

    }



    private static string GetMonsterActionDisplayName(MonsterActionType action)

    {

        switch (action)

        {

            case MonsterActionType.AttackPlayer:

                return "공격";

            case MonsterActionType.RestoreContamination:

                return "정화 감소";

            case MonsterActionType.IncreaseCaptureRate:

                return "포획 상승";

            case MonsterActionType.DoNothing:

                return "휴식";

            default:

                return action.ToString();

        }

    }



    /// <summary>플레이어 행동 불가 등 즉각 안내를 배틀 로그에 표시합니다.</summary>
    public void ShowPlayerFeedback(string message)
    {
        if (string.IsNullOrEmpty(message))
            return;

        Debug.Log($"[BattleTurnController] {message}");
        OnBattleLog?.Invoke(message);

        ApplyBattleLogText(message);
        feedbackProtectedUntil = Time.unscaledTime + Mathf.Max(0f, playerFeedbackMinDisplayDuration);
        RestartFeedbackHoldRoutine();
    }

    private void LogAction(string message)

    {

        Debug.Log($"[BattleTurnController] {message}");

        OnBattleLog?.Invoke(message);



        if (Time.unscaledTime < feedbackProtectedUntil)
        {
            pendingBattleLogMessage = message;
            return;
        }

        ApplyBattleLogText(message);

    }

    private void ApplyBattleLogText(string message)
    {
        if (battleLogText != null)
            battleLogText.text = message;
    }

    private void RestartFeedbackHoldRoutine()
    {
        if (feedbackHoldRoutine != null)
            StopCoroutine(feedbackHoldRoutine);

        feedbackHoldRoutine = StartCoroutine(ReleaseFeedbackProtectionRoutine());
    }

    private IEnumerator ReleaseFeedbackProtectionRoutine()
    {
        float wait = feedbackProtectedUntil - Time.unscaledTime;
        if (wait > 0f)
            yield return new WaitForSecondsRealtime(wait);

        feedbackHoldRoutine = null;
        feedbackProtectedUntil = 0f;

        if (string.IsNullOrEmpty(pendingBattleLogMessage))
            yield break;

        string pending = pendingBattleLogMessage;
        pendingBattleLogMessage = null;
        ApplyBattleLogText(pending);
    }

    private void ClearFeedbackHoldState()
    {
        if (feedbackHoldRoutine != null)
        {
            StopCoroutine(feedbackHoldRoutine);
            feedbackHoldRoutine = null;
        }

        feedbackProtectedUntil = 0f;
        pendingBattleLogMessage = null;
    }

}


