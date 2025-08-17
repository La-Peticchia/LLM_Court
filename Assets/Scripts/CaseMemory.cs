public static class CaseMemory
{
    public static CaseDescription SavedCase;
    public static CaseDescription SavedTranslatedCase;
    public static bool RestartingSameCase = false;
    public static int? NewAISeed = null;
    public static int? OriginalSeed = null; 

    public static void Clear()
    {
        SavedCase = null;
        SavedTranslatedCase = null;
        RestartingSameCase = false;
        NewAISeed = null;
        OriginalSeed = null;
    }

    public static bool HasValidSavedCase =>
        SavedCase != null && SavedTranslatedCase != null && RestartingSameCase;
}