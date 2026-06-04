public static class BattleEncounterContext
{
    private static string currentEncounteredMonsterId;
    private static UnityEngine.GameObject currentEncounteredMonsterObject;
    private static bool fleeExitPending;

    /// <summary>도망으로 전투가 끝날 때 true. 오염도 저장 대신 전투 진입 시점으로 되돌립니다.</summary>
    public static void MarkFleeExit() => fleeExitPending = true;

    public static bool IsFleeExitPending => fleeExitPending;

    public static void ClearFleeExit() => fleeExitPending = false;

    /// <summary>전체 리셋 시 배틀·도망·충돌 몬스터 참조를 모두 비웁니다.</summary>
    public static void ResetAll()
    {
        fleeExitPending = false;
        currentEncounteredMonsterId = null;
        currentEncounteredMonsterObject = null;
    }

    public static void SetEncounteredMonsterId(string monsterId)
    {
        currentEncounteredMonsterId = string.IsNullOrWhiteSpace(monsterId) ? null : monsterId;
    }

    public static void SetEncounteredMonsterObject(UnityEngine.GameObject monster)
    {
        currentEncounteredMonsterObject = monster;
    }

    public static string ConsumeEncounteredMonsterId()
    {
        string monsterId = currentEncounteredMonsterId;
        currentEncounteredMonsterId = null;
        return monsterId;
    }

    public static UnityEngine.GameObject PeekEncounteredMonsterObject()
    {
        return currentEncounteredMonsterObject;
    }

    public static UnityEngine.GameObject ConsumeEncounteredMonsterObject()
    {
        UnityEngine.GameObject monster = currentEncounteredMonsterObject;
        currentEncounteredMonsterObject = null;
        return monster;
    }
}