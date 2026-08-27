using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using OxenteGames.JiraCommunication.Settings;
using UnityEditor;
using UnityEngine;

namespace OxenteGames.JiraCommunication.Agents
{
    /// <summary>One variable read from the agent's env file.</summary>
    internal struct AgentEnvVariable
    {
        public string Key;
        public string Value;
    }

    /// <summary>
    /// Reads the env file whose variables are exported into the agent process.
    /// </summary>
    /// <remarks>
    /// The agent runs as a detached shell script, so the only way to give it
    /// configuration is the environment it starts with. Keeping that in a file —
    /// rather than in <c>EditorPrefs</c> — is deliberate: a developer debugging a run
    /// needs to see exactly what the process was given, and the agent itself can read
    /// its own connection settings from there.
    /// <para>
    /// The location is <c>~/.claude/jira.env</c>, and that is not our invention: it is
    /// where the Jira skill for Claude Code already looks. Writing a second file
    /// somewhere else produced exactly the failure this was meant to prevent — the
    /// window reporting a connection configured while the agent, reading the file it
    /// knows about, reported no credentials. One location, whoever wrote it.
    /// </para>
    /// <para>
    /// The file carries <c>JIRA_URL</c>, <c>JIRA_EMAIL</c> and <c>JIRA_API_TOKEN</c>,
    /// so it is a credential store: it is created with empty values, it lives in the
    /// developer's home rather than in the repository — which is also what keeps it
    /// out of a commit — and the header says as much.
    /// </para>
    /// <para>
    /// Parsing is intentionally lenient and dumb. This is not a dotenv
    /// implementation: there is no interpolation, no <c>export</c> keyword handling
    /// beyond stripping it, and no multi-line values. Anything a shell would treat as
    /// clever is treated here as literal text, because a value that silently expands
    /// into something else is worse than one that does not work.
    /// </para>
    /// </remarks>
    internal static class AgentEnvFile
    {
        /// <summary>
        /// Name of the shared credentials file, as the Jira skill's helper expects it.
        /// </summary>
        public const string DefaultFileName = "jira.env";

        /// <summary>Where earlier versions of this package put the file.</summary>
        private const string LegacyFileName = ".env";

        /// <summary>Marks a file this package generated, for the legacy cleanup.</summary>
        private const string GeneratedMarker = "# jira-unity:generated";

        /// <summary>Jira connection keys, in the order the template writes them.</summary>
        public const string KeyUrl = "JIRA_URL";
        public const string KeyEmail = "JIRA_EMAIL";
        public const string KeyToken = "JIRA_API_TOKEN";

        /// <summary>
        /// The Claude Code configuration folder in the developer's home.
        /// </summary>
        /// <remarks>
        /// Resolved from the user profile rather than from <c>HOME</c>, which is often
        /// unset for a Windows Editor launched from Explorer — the same reason the
        /// CLI locator does not trust it either.
        /// </remarks>
        public static string ClaudeHome
        {
            get
            {
                try
                {
                    string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    return string.IsNullOrWhiteSpace(home)
                        ? string.Empty
                        : Path.Combine(home, ".claude");
                }
                catch (Exception)
                {
                    return string.Empty;
                }
            }
        }

        /// <summary>The Unity project folder — the parent of <c>Assets</c>.</summary>
        public static string ProjectRoot
        {
            get
            {
                try
                {
                    return Directory.GetParent(Application.dataPath)?.FullName
                           ?? Environment.CurrentDirectory;
                }
                catch (Exception)
                {
                    return Environment.CurrentDirectory;
                }
            }
        }

        /// <summary>
        /// Absolute path of the env file.
        /// </summary>
        /// <remarks>
        /// Defaults to <c>~/.claude/jira.env</c> — one file per developer, shared with
        /// the Jira skill, and never inside a repository. A configured path still
        /// wins, for a team that keeps agent variables somewhere else; a relative
        /// override resolves from the project folder, which is the only reading of a
        /// relative path that means anything to someone typing it here.
        /// </remarks>
        public static string Resolve()
        {
            string configured = JiraPreferences.AgentEnvPath;

            if (!string.IsNullOrWhiteSpace(configured))
            {
                return Path.IsPathRooted(configured)
                    ? configured
                    : Path.Combine(ProjectRoot, configured);
            }

            string home = ClaudeHome;
            return string.IsNullOrWhiteSpace(home)
                ? Path.Combine(ProjectRoot, DefaultFileName)
                : Path.Combine(home, DefaultFileName);
        }

        /// <summary>The file an earlier version of this package created, if it is still there.</summary>
        public static string LegacyPath()
        {
            return Path.Combine(ProjectRoot, LegacyFileName);
        }

        public static bool Exists()
        {
            try
            {
                string path = Resolve();
                return !string.IsNullOrWhiteSpace(path) && File.Exists(path);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static string Read()
        {
            try
            {
                string path = Resolve();
                return !string.IsNullOrWhiteSpace(path) && File.Exists(path)
                    ? File.ReadAllText(path)
                    : string.Empty;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        /// <summary>Writes the file, creating its folder. Returns null on success.</summary>
        public static string Write(string content)
        {
            string path = Resolve();

            if (string.IsNullOrWhiteSpace(path))
                return "no-path";

            try
            {
                string folder = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(folder))
                    Directory.CreateDirectory(folder);

                File.WriteAllText(path, Normalize(content), new UTF8Encoding(false));

                // Written here as well as at import: the path is configurable, so a
                // file that moved must be protected where it landed.
                EnsureIgnored();
                return null;
            }
            catch (Exception exception)
            {
                return exception.Message;
            }
        }

        /// <summary>
        /// Forces Unix line endings.
        /// </summary>
        /// <remarks>
        /// Not cosmetic. The Jira skill's helper reads this file with the shell's
        /// <c>.</c> command: a CRLF file assigns a value with a trailing carriage
        /// return, that byte travels into the basic-auth header, and Jira answers 401.
        /// The failure reads exactly like a wrong token, which is the worst possible
        /// way for a line ending to present itself.
        /// </remarks>
        private static string Normalize(string content)
        {
            return (content ?? string.Empty).Replace("\r\n", "\n").Replace("\r", "\n");
        }

        /// <summary>
        /// Creates the file with the commented template when it does not exist yet.
        /// </summary>
        /// <returns>True when a file was created by this call.</returns>
        public static bool EnsureCreated()
        {
            if (Exists())
                return false;

            return Write(Template()) == null;
        }

        /// <summary>Variables exported for a run, in file order.</summary>
        public static List<AgentEnvVariable> Load()
        {
            if (!JiraPreferences.AgentEnvEnabled)
                return new List<AgentEnvVariable>();

            var exported = new List<AgentEnvVariable>();

            // An empty value is dropped rather than exported: the template ships the
            // Jira keys blank, and exporting JIRA_URL="" would make the agent believe
            // it has a connection and fail against an empty host.
            foreach (AgentEnvVariable variable in Parse(Read()))
            {
                if (!string.IsNullOrWhiteSpace(variable.Value))
                    exported.Add(variable);
            }

            return exported;
        }

        /// <summary>
        /// Splits env file text into variables. Blank lines, comments and lines
        /// without a name are skipped rather than reported: an env file is edited by
        /// hand, and a stray line must not stop a run from starting.
        /// </summary>
        public static List<AgentEnvVariable> Parse(string content)
        {
            var variables = new List<AgentEnvVariable>();

            if (string.IsNullOrWhiteSpace(content))
                return variables;

            foreach (string rawLine in content.Split('\n'))
            {
                string line = rawLine.Trim('\r', ' ', '\t');

                if (line.Length == 0 || line[0] == '#')
                    continue;

                if (line.StartsWith("export ", StringComparison.Ordinal))
                    line = line.Substring(7).TrimStart();

                int separator = line.IndexOf('=');
                if (separator <= 0)
                    continue;

                string key = line.Substring(0, separator).Trim();
                if (!IsValidKey(key))
                    continue;

                variables.Add(new AgentEnvVariable
                {
                    Key = key,
                    Value = Unquote(line.Substring(separator + 1).Trim())
                });
            }

            return variables;
        }

        /// <summary>
        /// Names that survive into the process. Anything a shell could not assign is
        /// dropped here rather than producing a launcher script that fails to run.
        /// </summary>
        private static bool IsValidKey(string key)
        {
            if (string.IsNullOrEmpty(key))
                return false;

            if (!char.IsLetter(key[0]) && key[0] != '_')
                return false;

            foreach (char c in key)
            {
                if (!char.IsLetterOrDigit(c) && c != '_')
                    return false;
            }

            return true;
        }

        private static string Unquote(string value)
        {
            if (value.Length >= 2 &&
                ((value[0] == '"' && value[value.Length - 1] == '"') ||
                 (value[0] == '\'' && value[value.Length - 1] == '\'')))
            {
                return value.Substring(1, value.Length - 2);
            }

            return value;
        }

        /// <summary>True when the three Jira keys are filled in.</summary>
        public static bool HasJiraConnection(string content)
        {
            bool url = false, email = false, token = false;

            foreach (AgentEnvVariable variable in Parse(content))
            {
                if (string.IsNullOrWhiteSpace(variable.Value))
                    continue;

                if (variable.Key == KeyUrl)
                    url = true;
                else if (variable.Key == KeyEmail)
                    email = true;
                else if (variable.Key == KeyToken)
                    token = true;
            }

            return url && email && token;
        }

        /// <summary>
        /// Returns the content with the Jira keys set to the connection already
        /// configured in the window, so the token is not typed a second time.
        /// </summary>
        /// <remarks>
        /// Offered as an explicit action rather than done on import. Copying a
        /// personal token into a file on disk is a decision, and the developer makes
        /// it knowingly or not at all.
        /// </remarks>
        public static string FillFromConnection(string content)
        {
            string filled = Upsert(content ?? string.Empty, KeyUrl, JiraPreferences.BaseUrl);
            filled = Upsert(filled, KeyEmail, JiraPreferences.Email);
            return Upsert(filled, KeyToken, JiraPreferences.Token);
        }

        /// <summary>
        /// Sets one key, replacing the existing assignment — commented or not — in
        /// place so the file keeps the order and comments the developer sees.
        /// </summary>
        private static string Upsert(string content, string key, string value)
        {
            string assignment = key + "=" + (value ?? string.Empty);
            var lines = new List<string>(content.Replace("\r\n", "\n").Split('\n'));
            bool replaced = false;

            for (int i = 0; i < lines.Count; i++)
            {
                string candidate = lines[i].Trim().TrimStart('#').TrimStart();

                if (!candidate.StartsWith(key + "=", StringComparison.Ordinal))
                    continue;

                lines[i] = assignment;
                replaced = true;
                break;
            }

            if (!replaced)
                lines.Add(assignment);

            return string.Join(Environment.NewLine, lines.ToArray());
        }

        /// <summary>
        /// Adds the env file to the repository's <c>.gitignore</c> if it is not
        /// already covered.
        /// </summary>
        /// <remarks>
        /// The file holds an API token, and a Unity <c>.gitignore</c> ignores
        /// <c>Library/</c> and friends but almost never <c>.env</c>. Matching is a
        /// plain line comparison — parsing gitignore patterns properly is not worth
        /// it, and the cost of a false negative is a duplicate line.
        /// </remarks>
        public static void EnsureIgnored()
        {
            try
            {
                string path = Resolve();
                string root = ProjectRoot;

                if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(root))
                    return;

                // Only a file inside the project can be expressed as a repository
                // ignore rule; one kept elsewhere is the developer's own business.
                if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                    return;

                string entry = path.Substring(root.Length).TrimStart('\\', '/').Replace('\\', '/');
                if (entry.Length == 0)
                    return;

                string gitignore = Path.Combine(root, ".gitignore");
                string existing = File.Exists(gitignore) ? File.ReadAllText(gitignore) : string.Empty;

                foreach (string line in existing.Split('\n'))
                {
                    string trimmed = line.Trim().TrimStart('/');
                    if (trimmed == entry || trimmed == "/" + entry)
                        return;
                }

                string separator = existing.Length == 0 || existing.EndsWith("\n", StringComparison.Ordinal)
                    ? string.Empty
                    : Environment.NewLine;

                File.AppendAllText(gitignore,
                    separator + Environment.NewLine
                              + "# Agent environment (Jira credentials) — do not commit"
                              + Environment.NewLine + entry + Environment.NewLine,
                    new UTF8Encoding(false));
            }
            catch (Exception)
            {
                // Best effort. The file's own header still warns, and a project
                // without a .gitignore is not one this package should be creating.
            }
        }

        /// <summary>
        /// Deals with the <c>.env</c> an earlier version wrote into the project.
        /// </summary>
        /// <remarks>
        /// Values there are carried over to the shared file — losing a token the
        /// developer already typed would be the worst possible way to change a default
        /// path — and only then is the file removed, and only if this package wrote it
        /// and nothing else was added to it. A <c>.env</c> that belongs to the game,
        /// or one the developer edited beyond our keys, is left exactly where it is.
        /// </remarks>
        /// <returns>True when the legacy file was removed.</returns>
        public static bool RetireLegacyFile()
        {
            try
            {
                string legacy = LegacyPath();
                string current = Resolve();

                if (!File.Exists(legacy) ||
                    string.Equals(legacy, current, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                string legacyText = File.ReadAllText(legacy);
                if (legacyText.IndexOf(GeneratedMarker, StringComparison.Ordinal) < 0 &&
                    legacyText.IndexOf("Variáveis exportadas para a CLI do agente",
                        StringComparison.Ordinal) < 0)
                {
                    // Not ours. Leave it alone.
                    return false;
                }

                string merged = Read();
                bool carried = false;

                foreach (AgentEnvVariable variable in Parse(legacyText))
                {
                    if (string.IsNullOrWhiteSpace(variable.Value))
                        continue;

                    // Only fills gaps: the shared file is the one in use, so a value
                    // already there is the newer of the two.
                    if (!HasValue(merged, variable.Key))
                    {
                        merged = Upsert(merged, variable.Key, variable.Value);
                        carried = true;
                    }
                }

                if (carried)
                    Write(merged);

                File.Delete(legacy);

                string meta = legacy + ".meta";
                if (File.Exists(meta))
                    File.Delete(meta);

                return true;
            }
            catch (Exception)
            {
                // The old file lingering is untidy, not broken: the shared one is what
                // both the window and the agent read.
                return false;
            }
        }

        private static bool HasValue(string content, string key)
        {
            foreach (AgentEnvVariable variable in Parse(content))
            {
                if (variable.Key == key && !string.IsNullOrWhiteSpace(variable.Value))
                    return true;
            }

            return false;
        }

        /// <summary>Starting content for a project that has no env file yet.</summary>
        public static string Template()
        {
            var sb = new StringBuilder(1024);
            sb.AppendLine(GeneratedMarker);
            sb.AppendLine("# Credenciais do Jira lidas pelo agente (skill jira) e exportadas");
            sb.AppendLine("# para a CLI antes de cada execução iniciada pela janela do Unity.");
            sb.AppendLine("# Jira credentials read by the agent (jira skill) and exported to the");
            sb.AppendLine("# CLI before every run started from the Unity window.");
            sb.AppendLine("#");
            sb.AppendLine("# NÃO COMMITE ESTE ARQUIVO — ele guarda o seu token do Jira.");
            sb.AppendLine("# DO NOT COMMIT THIS FILE — it holds your Jira token.");
            sb.AppendLine("#");
            sb.AppendLine("# Uma variável por linha, CHAVE=valor. Sem interpolação: o valor vai literal.");
            sb.AppendLine("# One variable per line, KEY=value. No interpolation.");
            sb.AppendLine();
            sb.AppendLine("# Conexão com o Jira, usada pelo agente no chat.");
            sb.AppendLine("# Jira connection, used by the agent in the chat.");
            sb.AppendLine("# Token: https://id.atlassian.com/manage-profile/security/api-tokens");
            sb.AppendLine(KeyUrl + "=");
            sb.AppendLine(KeyEmail + "=");
            sb.AppendLine(KeyToken + "=");
            sb.AppendLine();
            sb.AppendLine("# Opcionais da CLI / optional CLI settings:");
            sb.AppendLine("# ANTHROPIC_MODEL=claude-sonnet-5");
            sb.AppendLine("# MAX_THINKING_TOKENS=8000");
            sb.AppendLine("# BASH_DEFAULT_TIMEOUT_MS=120000");
            return sb.ToString();
        }
    }

    /// <summary>
    /// Creates the env file the first time the package is loaded, and retires the
    /// copy earlier versions left inside the project.
    /// </summary>
    /// <remarks>
    /// Import-time creation is the point: a settings card pointing at a file that
    /// does not exist reads as "there is nothing to configure here". A file that is
    /// already there, with the keys spelled out and commented, shows what the feature
    /// wants without the developer having to know.
    /// <para>
    /// It runs once per project and never rewrites: the flag is keyed by project path
    /// so deleting the file on purpose keeps it deleted, and a developer who emptied
    /// it does not get the template back on the next recompile.
    /// </para>
    /// </remarks>
    [InitializeOnLoad]
    internal static class AgentEnvBootstrap
    {
        // Versioned: the location changed to ~/.claude/jira.env, and a project
        // already flagged by the previous version would never run the migration
        // that moves its values there.
        private const string FlagKey = "OxenteGames.JiraCommunication.Agent.EnvBootstrapped.v2.";

        static AgentEnvBootstrap()
        {
            // Deferred: this runs during assembly load, where touching the file system
            // and Application.dataPath from a static initializer is asking for trouble.
            EditorApplication.delayCall += Run;
        }

        private static void Run()
        {
            try
            {
                // Keyed by the project path itself, not by its hash: EditorPrefs is
                // per machine, and a hash that is not stable across runs would make
                // this "once per project" flag forget on the next session.
                string flag = FlagKey + AgentEnvFile.ProjectRoot;

                if (EditorPrefs.GetBool(flag, false))
                    return;

                EditorPrefs.SetBool(flag, true);

                AgentEnvFile.EnsureCreated();
                AgentEnvFile.EnsureIgnored();

                if (AgentEnvFile.RetireLegacyFile())
                    AssetDatabase.Refresh();
            }
            catch (Exception)
            {
                // Creating a convenience file must never break a project's load.
            }
        }
    }
}
