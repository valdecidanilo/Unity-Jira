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
    /// configuration is the environment it starts with. Keeping that in a file at the
    /// project root — rather than in <c>EditorPrefs</c> — is deliberate: a developer
    /// debugging a run needs to see exactly what the process was given, and the agent
    /// itself can read its own connection settings from there.
    /// <para>
    /// The file carries <c>JIRA_URL</c>, <c>JIRA_EMAIL</c> and <c>JIRA_API_TOKEN</c>,
    /// which is what lets the agent reach Jira on its own instead of every read going
    /// through the window. That makes the file a credential store: it is created with
    /// empty values, the Editor adds it to <c>.gitignore</c> on sight, and the header
    /// says so — a token committed to a shared repository is the failure mode this
    /// file invites, and the only real defence is that it never gets committed.
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
        public const string DefaultFileName = ".env";

        /// <summary>Jira connection keys, in the order the template writes them.</summary>
        public const string KeyUrl = "JIRA_URL";
        public const string KeyEmail = "JIRA_EMAIL";
        public const string KeyToken = "JIRA_API_TOKEN";

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
        /// Anchored to the project root rather than to a run's working directory: the
        /// file must resolve to the same place whether it is being edited in the
        /// settings tab or exported into a run whose repository root sits elsewhere.
        /// A configured path wins, so a team that keeps agent variables somewhere else
        /// is not forced to move them; a relative override resolves from the project.
        /// </remarks>
        public static string Resolve()
        {
            string configured = JiraPreferences.AgentEnvPath;

            if (string.IsNullOrWhiteSpace(configured))
                return Path.Combine(ProjectRoot, DefaultFileName);

            return Path.IsPathRooted(configured)
                ? configured
                : Path.Combine(ProjectRoot, configured);
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

                File.WriteAllText(path, content ?? string.Empty, new UTF8Encoding(false));

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

        /// <summary>Starting content for a project that has no env file yet.</summary>
        public static string Template()
        {
            var sb = new StringBuilder(1024);
            sb.AppendLine("# Variáveis exportadas para a CLI do agente antes de cada execução.");
            sb.AppendLine("# Environment exported to the agent CLI before every run.");
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
    /// Creates the env file the first time the package is loaded in a project.
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
        private const string FlagKey = "OxenteGames.JiraCommunication.Agent.EnvBootstrapped.";

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

                if (AgentEnvFile.EnsureCreated())
                    AssetDatabase.Refresh();

                AgentEnvFile.EnsureIgnored();
            }
            catch (Exception)
            {
                // Creating a convenience file must never break a project's load.
            }
        }
    }
}
