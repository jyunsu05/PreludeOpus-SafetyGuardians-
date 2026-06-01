public static class BattleEncounterContext
{
    private static string currentEncounteredMonsterId;

    public static void SetEncounteredMonsterId(string monsterId)
    {
        currentEncounteredMonsterId = string.IsNullOrWhiteSpace(monsterId) ? null : monsterId;
    }

    public static string ConsumeEncounteredMonsterId()
    {
        string monsterId = currentEncounteredMonsterId;
        currentEncounteredMonsterId = null;
        return monsterId;
    }
}