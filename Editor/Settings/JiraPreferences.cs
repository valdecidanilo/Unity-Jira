using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;

namespace OxenteGames.JiraCommunication.Settings
{
    internal static class JiraPreferences
    {
        private const string BaseKey = "OxenteGames.JiraCommunication.";
        private const string PinnedIssuesKey = BaseKey + "Resolve.Pinned";
        private const string BaseUrlKey = BaseKey + "BaseUrl";
        private const string EmailKey = BaseKey + "Email";
        private const string TokenKey = BaseKey + "Token";
        private const string LanguageKey = BaseKey + "Language";
        private const string PresetProjectKey = BaseKey + "Preset.ProjectKey";
        private const string PresetIssueTypeKey = BaseKey + "Preset.IssueTypeName";
        private const string PresetPriorityKey = BaseKey + "Preset.PriorityId";
        private const string PresetAssigneeKey = BaseKey + "Preset.AssigneeAccountId";
        private const string PresetTeamKey = BaseKey + "Preset.TeamValue";
        private const string AiTokenKey = BaseKey + "Ai.Token";
        private const string AiModelKey = BaseKey + "Ai.Model";
        private const string AiProviderKey = BaseKey + "Ai.Provider";

        // Not cryptographic security. It only keeps tokens from being stored as
        // readable plaintext in the EditorPrefs registry/plist.
        private static readonly byte[] ObfuscationKey =
            Encoding.UTF8.GetBytes("OxenteGames.JiraCommunication.Token");

        private static string Obfuscate(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            byte[] bytes = Encoding.UTF8.GetBytes(value);
            for (int i = 0; i < bytes.Length; i++)
                bytes[i] ^= ObfuscationKey[i % ObfuscationKey.Length];

            return Convert.ToBase64String(bytes);
        }

        private static string Deobfuscate(string stored)
        {
            if (string.IsNullOrEmpty(stored))
                return string.Empty;

            try
            {
                byte[] bytes = Convert.FromBase64String(stored);
                for (int i = 0; i < bytes.Length; i++)
                    bytes[i] ^= ObfuscationKey[i % ObfuscationKey.Length];

                return Encoding.UTF8.GetString(bytes);
            }
            catch (FormatException)
            {
                return string.Empty;
            }
        }

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

        // Persisted across Editor sessions so the user does not have to re-enter
        // the token every time. Stored obfuscated (not encrypted).
        public static string Token
        {
            get => Deobfuscate(EditorPrefs.GetString(TokenKey, string.Empty));
            set => EditorPrefs.SetString(TokenKey, Obfuscate(value));
        }

        public static void ClearToken()
        {
            EditorPrefs.DeleteKey(TokenKey);
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
            ClearToken();
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

        // AI assistant. Provider and tokens are persisted across sessions (like the Jira token).
        public const string ProviderAnthropic = "anthropic";
        public const string ProviderOpenAi = "openai";

        public static string AiProvider
        {
            get => EditorPrefs.GetString(AiProviderKey, ProviderAnthropic);
            set => EditorPrefs.SetString(AiProviderKey, string.IsNullOrEmpty(value) ? ProviderAnthropic : value);
        }

        public static string GetAiToken(string provider)
        {
            return Deobfuscate(EditorPrefs.GetString(AiTokenKey + "." + provider, string.Empty));
        }

        public static void SetAiToken(string provider, string value)
        {
            EditorPrefs.SetString(AiTokenKey + "." + provider, Obfuscate(value));
        }

        public static string GetAiModel(string provider)
        {
            string fallback = provider == ProviderOpenAi ? "gpt-4o" : "claude-sonnet-5";
            return EditorPrefs.GetString(AiModelKey + "." + provider, fallback);
        }

        public static void SetAiModel(string provider, string value)
        {
            EditorPrefs.SetString(AiModelKey + "." + provider, value ?? string.Empty);
        }

        public static void ClearPresets()
        {
            EditorPrefs.DeleteKey(PresetProjectKey);
            EditorPrefs.DeleteKey(PresetIssueTypeKey);
            EditorPrefs.DeleteKey(PresetPriorityKey);
            EditorPrefs.DeleteKey(PresetAssigneeKey);
            EditorPrefs.DeleteKey(PresetTeamKey);
        }

        // --- Pinned issues (Resolve tab) ---

        public static List<string> GetPinnedIssues()
        {
            string raw = EditorPrefs.GetString(PinnedIssuesKey, string.Empty);
            var list = new List<string>();
            if (string.IsNullOrWhiteSpace(raw))
                return list;

            foreach (string key in raw.Split(','))
            {
                string trimmed = key.Trim();
                if (trimmed.Length > 0 && !list.Contains(trimmed))
                    list.Add(trimmed);
            }

            return list;
        }

        public static bool IsIssuePinned(string issueKey)
        {
            return GetPinnedIssues().Contains(issueKey);
        }

        public static void ToggleIssuePinned(string issueKey)
        {
            if (string.IsNullOrWhiteSpace(issueKey))
                return;

            List<string> pinned = GetPinnedIssues();
            if (!pinned.Remove(issueKey))
                pinned.Insert(0, issueKey);

            EditorPrefs.SetString(PinnedIssuesKey, string.Join(",", pinned));
        }
    }
}
