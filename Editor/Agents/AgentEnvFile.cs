using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using OxenteGames.JiraCommunication.Settings;

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
    /// configuration is the environment it starts with. Keeping that in a file the
    /// team can read and edit — rather than in <c>EditorPrefs</c> — is deliberate:
    /// these are project settings such as a model override or a tool endpoint, they
    /// belong next to the code, and a developer debugging a run needs to see exactly
    /// what the process was given.
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

        /// <summary>Absolute path of the env file for a repository.</summary>
        /// <remarks>
        /// A configured path wins, so a team that keeps agent variables in
        /// <c>.env.agent</c> or outside the repository is not forced to rename
        /// anything. Relative overrides resolve against the repository root.
        /// </remarks>
        public static string Resolve(string repositoryRoot)
        {
            string configured = JiraPreferences.AgentEnvPath;

            if (!string.IsNullOrWhiteSpace(configured))
            {
                if (Path.IsPathRooted(configured))
                    return configured;

                return string.IsNullOrWhiteSpace(repositoryRoot)
                    ? configured
                    : Path.Combine(repositoryRoot, configured);
            }

            return string.IsNullOrWhiteSpace(repositoryRoot)
                ? string.Empty
                : Path.Combine(repositoryRoot, DefaultFileName);
        }

        public static bool Exists(string repositoryRoot)
        {
            string path = Resolve(repositoryRoot);

            try
            {
                return !string.IsNullOrWhiteSpace(path) && File.Exists(path);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static string Read(string repositoryRoot)
        {
            string path = Resolve(repositoryRoot);

            try
            {
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
        public static string Write(string repositoryRoot, string content)
        {
            string path = Resolve(repositoryRoot);

            if (string.IsNullOrWhiteSpace(path))
                return "no-repository";

            try
            {
                string folder = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(folder))
                    Directory.CreateDirectory(folder);

                File.WriteAllText(path, content ?? string.Empty, new UTF8Encoding(false));
                return null;
            }
            catch (Exception exception)
            {
                return exception.Message;
            }
        }

        /// <summary>Variables exported for a run, in file order.</summary>
        public static List<AgentEnvVariable> Load(string repositoryRoot)
        {
            if (!JiraPreferences.AgentEnvEnabled)
                return new List<AgentEnvVariable>();

            return Parse(Read(repositoryRoot));
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

        /// <summary>Starting content for a repository that has no env file yet.</summary>
        public static string Template()
        {
            var sb = new StringBuilder(512);
            sb.AppendLine("# Variáveis exportadas para a CLI do agente antes de cada execução.");
            sb.AppendLine("# Environment exported to the agent CLI before every run.");
            sb.AppendLine("#");
            sb.AppendLine("# Uma variável por linha, no formato CHAVE=valor. Sem interpolação.");
            sb.AppendLine("# One variable per line, KEY=value. No interpolation.");
            sb.AppendLine();
            sb.AppendLine("# ANTHROPIC_MODEL=claude-sonnet-5");
            sb.AppendLine("# MAX_THINKING_TOKENS=8000");
            sb.AppendLine("# BASH_DEFAULT_TIMEOUT_MS=120000");
            sb.AppendLine("# DISABLE_TELEMETRY=1");
            return sb.ToString();
        }
    }
}
