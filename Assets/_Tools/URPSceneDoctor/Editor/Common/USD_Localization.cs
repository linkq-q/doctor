namespace URPSceneDoctor.Editor
{
    // Backward-compatible wrapper. New code should use USD_Loc directly.
    public static class USD_Localization
    {
        public static bool IsZh() => USD_Loc.CurrentLang() == "zh";
        public static string T(string key) => USD_Loc.T(key);
        public static string Label(string zh, string en) => USD_Loc.CurrentLang() == "zh" ? zh : en;
    }
}
