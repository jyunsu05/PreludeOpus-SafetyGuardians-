using UnityEngine;

public enum SessionGrade
{
    S,
    A,
    B,
    C,
    D
}

/// <summary>
/// 세션 성과 점수(0~100) 및 S~D 등급·칭호 계산. Spec §7.1~7.3.
/// </summary>
public static class SessionGradeCalculator
{
    public const float PurifyScorePerMonster = 10f;
    public const float OxygenScoreFactor = 0.5f;
    public const float EscapePenaltyPerCount = 5f;

    public const int GradeSMinScore = 85;
    public const int GradeAMinScore = 70;
    public const int GradeBMinScore = 55;
    public const int GradeCMinScore = 40;

    public const float GradeSMinOxygenPercent = 50f;
    public const float GradeAMinOxygenPercent = 35f;
    public const float GradeBMinOxygenPercent = 20f;
    public const float GradeCMinOxygenPercent = 10f;

    public const int GradeSMaxEscapeCount = 3;
    public const int GradeAMaxEscapeCount = 5;
    public const int GradeBMaxEscapeCount = 7;

    public static int CalculateScore(int purifiedMonsters, float remainingOxygenPercent, int escapeCount)
    {
        float raw = purifiedMonsters * PurifyScorePerMonster
                    + remainingOxygenPercent * OxygenScoreFactor
                    - escapeCount * EscapePenaltyPerCount;
        return Mathf.Clamp(Mathf.RoundToInt(raw), 0, 100);
    }

    public static SessionGrade DetermineGrade(int score, float remainingOxygenPercent, int escapeCount)
    {
        if (score >= GradeSMinScore
            && remainingOxygenPercent >= GradeSMinOxygenPercent
            && escapeCount <= GradeSMaxEscapeCount)
            return SessionGrade.S;

        if (score >= GradeAMinScore
            && remainingOxygenPercent >= GradeAMinOxygenPercent
            && escapeCount <= GradeAMaxEscapeCount)
            return SessionGrade.A;

        if (score >= GradeBMinScore
            && remainingOxygenPercent >= GradeBMinOxygenPercent
            && escapeCount <= GradeBMaxEscapeCount)
            return SessionGrade.B;

        if (score >= GradeCMinScore
            && remainingOxygenPercent >= GradeCMinOxygenPercent)
            return SessionGrade.C;

        return SessionGrade.D;
    }

    public static SessionGrade FromStatBlock(StatBlock block)
    {
        int score = CalculateScore(
            block.purifiedMonsters,
            block.finalOxygenPercent,
            block.escapeCount);
        return DetermineGrade(score, block.finalOxygenPercent, block.escapeCount);
    }

    public static int CalculateScoreFromStatBlock(StatBlock block)
    {
        return CalculateScore(
            block.purifiedMonsters,
            block.finalOxygenPercent,
            block.escapeCount);
    }

    public static string GetTitle(SessionGrade grade)
    {
        switch (grade)
        {
            case SessionGrade.S: return "완벽한 수호자";
            case SessionGrade.A: return "베테랑 수호자";
            case SessionGrade.B: return "현장 수호자";
            case SessionGrade.C: return "간신히 해낸 수호자";
            case SessionGrade.D: return "버틴 수호자";
            default: return "버틴 수호자";
        }
    }

#if UNITY_EDITOR
    [UnityEditor.MenuItem("SafetyGuardians/Debug/Session Grade Calculator Self-Test")]
    private static void RunSelfTest()
    {
        var cases = new[]
        {
            (8, 65f, 2, SessionGrade.S),
            (7, 40f, 4, SessionGrade.A),
            (6, 25f, 6, SessionGrade.B),
            (5, 15f, 8, SessionGrade.C),
            (4, 8f, 10, SessionGrade.D),
        };

        foreach (var (purified, oxygen, escape, expected) in cases)
        {
            int score = CalculateScore(purified, oxygen, escape);
            SessionGrade grade = DetermineGrade(score, oxygen, escape);
            Debug.Log(
                $"[SessionGradeCalculator] purified={purified}, oxygen={oxygen}%, escape={escape} "
                + $"→ score={score}, grade={grade} (expected {expected})");
        }
    }
#endif
}
