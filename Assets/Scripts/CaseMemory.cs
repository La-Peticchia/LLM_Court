public static class CaseMemory
{
    public static CaseDescription? SavedCase;
    public static CaseDescription? SavedTranslatedCase;
    public static bool RestartingSameCase = false;

    public static void Clear()
    {
        SavedCase = null;
        SavedTranslatedCase = null;
        RestartingSameCase = false;
    }

    public static bool HasValidSavedCase =>
        SavedCase.HasValue && SavedTranslatedCase.HasValue && RestartingSameCase;
}
