using UnityEditor;

namespace OxenteGames.JiraCommunication.Settings
{
    internal static class JiraPreferences
    {
        private const string BaseKey = "OxenteGames.JiraCommunication.";
        private const string BaseUrlKey = BaseKey + "BaseUrl";
        private const string EmailKey = BaseKey + "Email";
        private const string TokenSessionKey = BaseKey + "Token.Session";

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
    }
}
