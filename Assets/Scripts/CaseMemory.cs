public static class CaseMemory
{
    public static CaseDescription SavedCase;
    public static CaseDescription SavedTranslatedCase;
    public static bool RestartingSameCase = false;
    public static int? NewSeed = null;

    public static void Clear()
    {
        SavedCase = null;
        SavedTranslatedCase = null;
        RestartingSameCase = false;
        NewSeed = null;
    }

    public static bool HasValidSavedCase =>
        SavedCase != null && SavedTranslatedCase != null && RestartingSameCase;
}