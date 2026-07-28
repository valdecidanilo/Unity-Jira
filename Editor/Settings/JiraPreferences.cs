using UnityEditor;

namespace OxenteGames.JiraCommunication.Settings
{
    internal static class JiraPreferences
    {
        private const string BaseKey = "OxenteGames.JiraCommunication.";
        private const string BaseUrlKey = BaseKey + "BaseUrl";
        private const string EmailKey = BaseKey + "Email";
        private const string TokenSessionKey = BaseKey + "Token.Session";
        private const string LanguageKey = BaseKey + "Language";
        private const string PresetProjectKey = BaseKey + "Preset.ProjectKey";
        private const string PresetIssueTypeKey = BaseKey + "Preset.IssueTypeName";
        private const string PresetPriorityKey = BaseKey + "Preset.PriorityId";
        private const string PresetAssigneeKey = BaseKey + "Preset.AssigneeAccountId";
        private const string PresetTeamKey = BaseKey + "Preset.TeamValue";

        public static string BaseUrl
        {
            get => EditorPrefs.GetString(BaseUrlKey, string.Empty);
            set => EditorPrefs.SetString(BaseUrlKey, value ?? string.Empty);
        }

        public static string Email
        {
            get => EditorPrefs.GetString(EmailKey, string.Empty);
            set => EditorPrefs.SetString(EmailKey, value ?? string.Empty);
        }

        // Intentionally session-only. It is cleared when the Unity Editor closes.
        public static string SessionToken
        {
            get => SessionState.GetString(TokenSessionKey, string.Empty);
            set => SessionState.SetString(TokenSessionKey, value ?? string.Empty);
        }

        public static void ClearSessionToken()
        {
            SessionState.EraseString(TokenSessionKey);
        }

        // "pt" or "en". Defaults to Portuguese.
        public static string Language
        {
            get => EditorPrefs.GetString(LanguageKey, "pt");
            set => EditorPrefs.SetString(LanguageKey, string.IsNullOrEmpty(value) ? "pt" : value);
        }

        public static void ClearConnectionInfo()
        {
            EditorPrefs.DeleteKey(BaseUrlKey);
            EditorPrefs.DeleteKey(EmailKey);
            ClearSessionToken();
        }

        // --- Create-form presets (persist across sessions) ---

        public static string PresetProject
        {
            get => EditorPrefs.GetString(PresetProjectKey, string.Empty);
            set => EditorPrefs.SetString(PresetProjectKey, value ?? string.Empty);
        }

        public static string PresetIssueTypeName
        {
            get => EditorPrefs.GetString(PresetIssueTypeKey, string.Empty);
            set => EditorPrefs.SetString(PresetIssueTypeKey, value ?? string.Empty);
        }

        public static string PresetPriorityId
        {
            get => EditorPrefs.GetString(PresetPriorityKey, string.Empty);
            set => EditorPrefs.SetString(PresetPriorityKey, value ?? string.Empty);
        }

        public static string PresetAssigneeAccountId
        {
            get => EditorPrefs.GetString(PresetAssigneeKey, string.Empty);
            set => EditorPrefs.SetString(PresetAssigneeKey, value ?? string.Empty);
        }

        public static string PresetTeamValue
        {
            get => EditorPrefs.GetString(PresetTeamKey, string.Empty);
            set => EditorPrefs.SetString(PresetTeamKey, value ?? string.Empty);
        }

        public static void ClearPresets()
        {
            EditorPrefs.DeleteKey(PresetProjectKey);
            EditorPrefs.DeleteKey(PresetIssueTypeKey);
            EditorPrefs.DeleteKey(PresetPriorityKey);
            EditorPrefs.DeleteKey(PresetAssigneeKey);
            EditorPrefs.DeleteKey(PresetTeamKey);
        }
    }
}
