using System;
using System.Collections.Generic;
using System.Globalization;
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
        private const string AgentProviderKey = BaseKey + "Agent.Provider";
        private const string AgentCliPathKey = BaseKey + "Agent.CliPath";
        private const string AgentPermissionKey = BaseKey + "Agent.Permission";
        private const string AgentModelKey = BaseKey + "Agent.Model";
        private const string AgentEnvEnabledKey = BaseKey + "Agent.EnvEnabled";
        private const string AgentEnvPathKey = BaseKey + "Agent.EnvPath";
        private const string AgentTokenBudgetKey = BaseKey + "Agent.TokenBudget";
        private const string AgentWindowHoursKey = BaseKey + "Agent.UsageWindowHours";
        private const string AgentPlanOnlyKey = BaseKey + "Agent.PlanOnly";
        private const string GitEnabledKey = BaseKey + "Git.Enabled";
        private const string GitRepoPathKey = BaseKey + "Git.RepoPath";
        private const string GitBaseBranchKey = BaseKey + "Git.BaseBranch";
        private const string GitBranchTemplateKey = BaseKey + "Git.BranchTemplate";
        private const string GitCommitTemplateKey = BaseKey + "Git.CommitTemplate";

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

        // --- Local agent (CLI) integration ---
        // No credential is stored here: the agent CLI authenticates with the account
        // the developer already logged into, so unlike the HTTP drafting path above
        // there is no token for this feature to keep.

        /// <summary>
        /// Which agent CLI to drive. Stored separately from <see cref="AiProvider"/>:
        /// the assistant is an API-key HTTP feature and the agent is a local CLI on the
        /// developer's own plan, so one must not decide the other.
        /// </summary>
        public static string AgentProviderId
        {
            get => Agents.AgentProvider.Sanitize(
                EditorPrefs.GetString(AgentProviderKey, Agents.AgentProvider.ClaudeCode));
            set => EditorPrefs.SetString(AgentProviderKey, Agents.AgentProvider.Sanitize(value));
        }

        /// <summary>
        /// Explicit path to a provider's CLI. Empty means auto-discovery, which is the
        /// normal case; this exists for installs that discovery cannot see, such as a
        /// Unity launched from Finder that never inherited the user's shell PATH.
        /// </summary>
        public static string GetAgentCliPath(string provider)
        {
            return EditorPrefs.GetString(AgentCliPathKey + "." + provider, string.Empty);
        }

        public static void SetAgentCliPath(string provider, string value)
        {
            EditorPrefs.SetString(AgentCliPathKey + "." + provider, value ?? string.Empty);
        }

        /// <summary>
        /// Permission posture for headless runs.
        /// </summary>
        /// <remarks>
        /// Defaults to "accept edits" — the posture the feature is actually for. It
        /// used to default to the read-only "plan" posture, on the reasoning that
        /// enabling the feature should not silently modify the project; in practice
        /// that made a fresh install answer every request with a plan and refuse to
        /// act, which reads as the agent being broken rather than as a safety choice.
        /// The dropdown in the agent console still offers "plan" for developers who
        /// want it, and the run is a git working tree either way.
        /// </remarks>
        public static string AgentPermission
        {
            get => EditorPrefs.GetString(AgentPermissionKey, Agents.AgentPermission.AcceptEdits);
            set => EditorPrefs.SetString(AgentPermissionKey,
                string.IsNullOrEmpty(value) ? Agents.AgentPermission.AcceptEdits : value);
        }

        /// <summary>
        /// CLI model override for headless runs, per provider. Empty — the default —
        /// means no <c>--model</c> flag is passed, so the CLI keeps whatever the
        /// developer configured. Stored per provider because the identifiers are not
        /// interchangeable between CLIs.
        /// </summary>
        public static string GetAgentModel(string provider)
        {
            return EditorPrefs.GetString(AgentModelKey + "." + provider, string.Empty);
        }

        public static void SetAgentModel(string provider, string value)
        {
            EditorPrefs.SetString(AgentModelKey + "." + provider, value ?? string.Empty);
        }

        /// <summary>
        /// Keeps a run on the developer's subscription by removing the variables that
        /// would send it to a billed API account instead.
        /// </summary>
        /// <remarks>
        /// On by default, and the reason is money: an <c>ANTHROPIC_API_KEY</c> left in
        /// the machine's environment silently switches Claude Code from the plan the
        /// developer is logged into to pay-per-token billing, and a background run
        /// started from a button in the Editor is exactly where that goes unnoticed.
        /// Turn it off only to deliberately bill an API account.
        /// </remarks>
        public static bool AgentPlanOnly
        {
            get => EditorPrefs.GetBool(AgentPlanOnlyKey, true);
            set => EditorPrefs.SetBool(AgentPlanOnlyKey, value);
        }

        /// <summary>
        /// Whether the project's env file is exported into the agent process.
        /// </summary>
        /// <remarks>
        /// On by default: a repository with no env file simply exports nothing, and a
        /// team that put one there did so to have it used. The switch exists so a
        /// developer can rule the file out while diagnosing a run.
        /// </remarks>
        public static bool AgentEnvEnabled
        {
            get => EditorPrefs.GetBool(AgentEnvEnabledKey, true);
            set => EditorPrefs.SetBool(AgentEnvEnabledKey, value);
        }

        /// <summary>
        /// Env file location. Empty means <c>.env</c> at the repository root; a
        /// relative value resolves from there, an absolute one is used as given.
        /// </summary>
        public static string AgentEnvPath
        {
            get => EditorPrefs.GetString(AgentEnvPathKey, string.Empty);
            set => EditorPrefs.SetString(AgentEnvPathKey, value ?? string.Empty);
        }

        /// <summary>
        /// Tokens the developer expects to have per quota window.
        /// </summary>
        /// <remarks>
        /// A local figure, not a reading of the plan: no CLI reports the account's
        /// remaining quota, so the percentage in the agent tab is measured against
        /// this. Zero turns the percentage off and leaves the raw counters, which is
        /// the honest state for someone who has not calibrated a number yet.
        /// </remarks>
        public static long AgentTokenBudget
        {
            get
            {
                string stored = EditorPrefs.GetString(AgentTokenBudgetKey, "0");
                return long.TryParse(stored, out long value) && value > 0 ? value : 0;
            }
            set => EditorPrefs.SetString(AgentTokenBudgetKey,
                (value > 0 ? value : 0).ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Length of a quota window in hours. Defaults to the five-hour cycle the
        /// Claude plans use.
        /// </summary>
        public static int AgentUsageWindowHours
        {
            get
            {
                int stored = EditorPrefs.GetInt(AgentWindowHoursKey, 5);
                return stored < 1 ? 1 : (stored > 168 ? 168 : stored);
            }
            set => EditorPrefs.SetInt(AgentWindowHoursKey, value < 1 ? 1 : value);
        }

        // --- Git / GitHub integration ---
        // Convention-only: no remote actions, no secrets. Persisted across sessions.

        public static bool GitEnabled
        {
            get => EditorPrefs.GetBool(GitEnabledKey, false);
            set => EditorPrefs.SetBool(GitEnabledKey, value);
        }

        // Empty = auto-detect the repository root from the Unity project folder.
        public static string GitRepoPath
        {
            get => EditorPrefs.GetString(GitRepoPathKey, string.Empty);
            set => EditorPrefs.SetString(GitRepoPathKey, value ?? string.Empty);
        }

        public static string GitBaseBranch
        {
            get => EditorPrefs.GetString(GitBaseBranchKey, "main");
            set => EditorPrefs.SetString(GitBaseBranchKey, value ?? string.Empty);
        }

        public static string GitBranchTemplate
        {
            get => EditorPrefs.GetString(GitBranchTemplateKey,
                Git.GitConventions.DefaultBranchTemplate);
            set => EditorPrefs.SetString(GitBranchTemplateKey, value ?? string.Empty);
        }

        public static string GitCommitTemplate
        {
            get => EditorPrefs.GetString(GitCommitTemplateKey,
                Git.GitConventions.DefaultCommitTemplate);
            set => EditorPrefs.SetString(GitCommitTemplateKey, value ?? string.Empty);
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
