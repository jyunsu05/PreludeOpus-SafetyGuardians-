public static class BattleEncounterContext
{
    private static string currentEncounteredMonsterId;
    private static UnityEngine.GameObject currentEncounteredMonsterObject;

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