using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace OxenteGames.JiraCommunication.Agents
{
    /// <summary>One command an ai-jira install exposes to the agent.</summary>
    internal struct AiJiraCommand
    {
        /// <summary>Skill name, which is also how the agent is asked for it.</summary>
        public string Name;

        /// <summary>Absolute path of the script behind it, when the install has one.</summary>
        public string ScriptPath;

        /// <summary>True when <c>skills/&lt;name&gt;/SKILL.md</c> is present in the install.</summary>
        public bool HasSkill;

        /// <summary>True when the command shells out to the GitHub CLI.</summary>
        public bool RequiresGh;

        public bool Available => HasSkill || !string.IsNullOrWhiteSpace(ScriptPath);
    }

    /// <summary>Outcome of probing the machine for an ai-jira install.</summary>
    internal struct AiJiraInfo
    {
        public bool Found;

        /// <summary>Install root — what <c>install.ps1</c> exports as <c>JIRA_CLI_HOME</c>.</summary>
        public string Home;

        public AiJiraCommand[] Commands;

        /// <summary>Absolute path of the PowerShell host, or empty when none answered.</summary>
        public string PowerShellPath;

        /// <summary>Absolute path of the GitHub CLI, or empty. Only <c>jira-pr</c> needs it.</summary>
        public string GhPath;

        /// <summary>Populated when <see cref="Found"/> is false.</summary>
        public string Error;

        public string[] SearchedPaths;

        public bool HasPowerShell => !string.IsNullOrWhiteSpace(PowerShellPath);
        public bool HasGh => !string.IsNullOrWhiteSpace(GhPath);

        public AiJiraCommand Command(string name)
        {
            if (Commands == null)
                return default(AiJiraCommand);

            foreach (AiJiraCommand command in Commands)
            {
                if (string.Equals(command.Name, name, StringComparison.Ordinal))
                    return command;
            }

            return default(AiJiraCommand);
        }

        /// <summary>The search trail as readable lines, for logging or the clipboard.</summary>
        public string Diagnostics
        {
            get
            {
                var sb = new StringBuilder();
                sb.Append("host is windows: ").AppendLine(AgentShell.IsWindows ? "yes" : "no");
                sb.Append("found: ").AppendLine(Found ? "yes" : "no");

                if (!string.IsNullOrWhiteSpace(Home))
                    sb.Append("home: ").AppendLine(Home);
                if (!string.IsNullOrWhiteSpace(PowerShellPath))
                    sb.Append("powershell: ").AppendLine(PowerShellPath);
                if (!string.IsNullOrWhiteSpace(GhPath))
                    sb.Append("gh: ").AppendLine(GhPath);
                if (!string.IsNullOrWhiteSpace(Error))
                    sb.Append("error: ").AppendLine(Error);

                if (Commands != null && Commands.Length > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("commands:");
                    foreach (AiJiraCommand command in Commands)
                    {
                        sb.Append("  ")
                          .Append(command.Available ? "[ok  ] " : "[miss] ")
                          .Append(command.Name)
                          .Append(command.HasSkill ? "  skill" : "  no-skill")
                          .AppendLine(string.IsNullOrWhiteSpace(command.ScriptPath)
                              ? "  no-script"
                              : "  " + command.ScriptPath);
                    }
                }

                sb.AppendLine();
                sb.AppendLine("searched:");

                if (SearchedPaths != null)
                {
                    foreach (string line in SearchedPaths)
                        sb.Append("  ").AppendLine(line);
                }

                return sb.ToString();
            }
        }
    }

    /// <summary>
    /// Discovers a local <c>ai-jira</c> install and reports what it can do.
    /// </summary>
    /// <remarks>
    /// <c>ai-jira</c> (github.com/Mikael-Cavalcanti/ai-jira) is a set of PowerShell
    /// scripts plus one skill per command, meant to be driven by a coding agent rather
    /// than called as a library. It is not a dependency of this package and cannot be
    /// one: it is installed per machine, by hand, outside any Unity project. So the
    /// window probes for it and lights up an extra tab when it is there, instead of
    /// shipping a tab that is dead for everyone who never installed it.
    /// <para>
    /// Everything about the probe is deliberately tolerant. A partial install — the
    /// scripts present but the skills never wired into the CLI, or the other way
    /// round — still counts as found, because the tab's job is then to say which half
    /// is missing. Reporting "not installed" for a machine that plainly has it is the
    /// failure that wastes the most time.
    /// </para>
    /// <para>
    /// The install is Windows-only PowerShell, so on macOS and Linux this reports not
    /// found and the tab never appears. That is the correct outcome, not a gap: there
    /// is nothing to run there.
    /// </para>
    /// </remarks>
    internal static class AiJiraLocator
    {
        /// <summary>User-scope variable <c>install.ps1</c> sets to the install root.</summary>
        public const string HomeVariable = "JIRA_CLI_HOME";

        /// <summary>Where the README tells people to clone it.</summary>
        public const string DefaultFolderName = ".ai-jira";

        public const string RepositoryUrl = "https://github.com/Mikael-Cavalcanti/ai-jira";

        public const string CommandInit = "jira-init";
        public const string CommandCard = "jira-card";
        public const string CommandPr = "jira-pr";
        public const string CommandSync = "jira-sync";

        /// <summary>The commands, in the order they appear in a real workflow.</summary>
        public static readonly string[] KnownCommands =
        {
            CommandInit,
            CommandCard,
            CommandPr,
            CommandSync
        };

        /// <summary>
        /// Script each command runs, relative to the install root.
        /// </summary>
        /// <remarks>
        /// The skill name and the script name are not the same word — <c>jira-card</c>
        /// runs <c>jira-new.ps1</c> — so the mapping is written out rather than derived.
        /// </remarks>
        private static readonly Dictionary<string, string> ScriptForCommand =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { CommandInit, "jira-init.ps1" },
                { CommandCard, "jira-new.ps1" },
                { CommandPr, "jira-pr.ps1" },
                { CommandSync, "jira-sync.ps1" }
            };

        /// <summary>File whose presence proves a directory really is the install root.</summary>
        private const string MarkerScript = "jira-lib.ps1";

        private static AiJiraInfo? _cache;

        public static void InvalidateCache()
        {
            _cache = null;
        }

        /// <summary>Cached probe result, or null when the machine was never probed.</summary>
        public static AiJiraInfo? Cached => _cache;

        public static async Task<AiJiraInfo> LocateAsync(bool forceRefresh = false)
        {
            if (!forceRefresh && _cache.HasValue)
                return _cache.Value;

            AiJiraInfo info = await ProbeAsync();
            _cache = info;
            return info;
        }

        private static async Task<AiJiraInfo> ProbeAsync()
        {
            var trail = new List<string>();
            string home = ResolveHome(trail);

            if (string.IsNullOrWhiteSpace(home))
            {
                return new AiJiraInfo
                {
                    Found = false,
                    Error = "not-installed",
                    SearchedPaths = trail.ToArray()
                };
            }

            AiJiraCommand[] commands = ResolveCommands(home, trail);

            // The hosts are probed even when nothing will run right now: the tab has to
            // be able to say "installed, but PowerShell did not answer" rather than
            // offering a button that fails the moment it is pressed.
            string powerShell = await ResolvePowerShellAsync(trail);
            string gh = await ResolveOnPathAsync("gh");
            trail.Add((string.IsNullOrWhiteSpace(gh) ? "[miss] " : "[hit ] ") + "PATH: gh");

            return new AiJiraInfo
            {
                Found = true,
                Home = home,
                Commands = commands,
                PowerShellPath = powerShell,
                GhPath = gh,
                SearchedPaths = trail.ToArray()
            };
        }

        /// <summary>
        /// The install root, or empty.
        /// </summary>
        /// <remarks>
        /// The exported variable is checked first and in both scopes. The process copy
        /// is what a run would actually inherit, but an Editor launched from Explorer
        /// before the install ran holds a stale environment, and the user scope is
        /// where <c>install.ps1</c> wrote it — so a machine that installed ai-jira
        /// after opening Unity is still detected without a restart.
        /// </remarks>
        private static string ResolveHome(List<string> trail)
        {
            foreach (string candidate in HomeCandidates(trail))
            {
                if (string.IsNullOrWhiteSpace(candidate))
                    continue;

                string marker = Path.Combine(Path.Combine(candidate, "bin"), MarkerScript);

                try
                {
                    if (File.Exists(marker))
                    {
                        trail.Add("[hit ] " + candidate);
                        return candidate;
                    }
                }
                catch (Exception)
                {
                    // An unreadable path is simply not the install root.
                }

                trail.Add("[miss] " + candidate + " (no bin/" + MarkerScript + ")");
            }

            return string.Empty;
        }

        private static IEnumerable<string> HomeCandidates(List<string> trail)
        {
            string exported = ReadEnvironment(HomeVariable);
            if (!string.IsNullOrWhiteSpace(exported))
            {
                trail.Add("[env ] " + HomeVariable + "=" + exported);
                yield return exported.Trim().Trim('"');
            }
            else
            {
                trail.Add("[skip] " + HomeVariable + ": not set");
            }

            string profile;
            try
            {
                profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            }
            catch (Exception)
            {
                profile = string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(profile))
                yield return Path.Combine(profile, DefaultFolderName);
        }

        /// <summary>Reads a variable from the process, then from the user scope.</summary>
        private static string ReadEnvironment(string name)
        {
            try
            {
                string value = Environment.GetEnvironmentVariable(name);
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }
            catch (Exception)
            {
                // Fall through to the user scope.
            }

            try
            {
                // Only Windows has a user scope; elsewhere this returns null rather
                // than throwing, which is the answer we want anyway.
                return Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User)
                       ?? string.Empty;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        private static AiJiraCommand[] ResolveCommands(string home, List<string> trail)
        {
            var commands = new List<AiJiraCommand>(KnownCommands.Length);

            foreach (string name in KnownCommands)
            {
                string script = Path.Combine(Path.Combine(home, "bin"), ScriptForCommand[name]);
                string skill = Path.Combine(
                    Path.Combine(Path.Combine(home, "skills"), name), "SKILL.md");

                bool hasScript = SafeExists(script);
                bool hasSkill = SafeExists(skill);

                if (!hasScript)
                    trail.Add("[miss] " + script);

                if (!hasSkill)
                    trail.Add("[miss] " + skill);

                commands.Add(new AiJiraCommand
                {
                    Name = name,
                    ScriptPath = hasScript ? script : string.Empty,
                    HasSkill = hasSkill,
                    RequiresGh = name == CommandPr
                });
            }

            return commands.ToArray();
        }

        /// <summary>
        /// True when the agent CLI can actually see the skills.
        /// </summary>
        /// <remarks>
        /// <c>install.ps1</c> writes a pointer per CLI into the developer's home. The
        /// scripts being present says nothing about whether that step ran, and a
        /// missing pointer is the one failure that looks like the agent ignoring an
        /// instruction: the prompt names <c>jira-card</c>, the agent has never heard of
        /// it, and it improvises.
        /// </remarks>
        public static bool SkillsWiredFor(string provider)
        {
            string home;
            try
            {
                home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            }
            catch (Exception)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(home))
                return false;

            string root = Path.Combine(home, provider == AgentProvider.Codex ? ".codex" : ".claude");

            foreach (string name in KnownCommands)
            {
                if (SafeExists(Path.Combine(Path.Combine(Path.Combine(root, "skills"), name), "SKILL.md")))
                    return true;

                string flat = provider == AgentProvider.Codex ? "prompts" : "commands";
                if (SafeExists(Path.Combine(Path.Combine(root, flat), name + ".md")))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Adds the variables an ai-jira run needs to the set already exported for the
        /// agent.
        /// </summary>
        /// <remarks>
        /// Two gaps, both small and both fatal if left open. The install exports
        /// <c>JIRA_CLI_HOME</c> into the user scope, which a long-running Editor
        /// process does not pick up, so it is re-exported from the path we just
        /// probed. And ai-jira reads the Jira host from <c>JIRA_BASE_URL</c> while this
        /// package has always written <c>JIRA_URL</c> — same value, different name,
        /// and without the alias every ai-jira command starts by asking for a URL the
        /// developer already typed into the connection tab.
        /// <para>
        /// Nothing is overwritten. A developer who set either variable by hand meant
        /// it, and a package quietly replacing a credential's target host is the kind
        /// of surprise that is very hard to trace from inside an agent transcript.
        /// </para>
        /// </remarks>
        public static void AppendVariables(IList<AgentEnvVariable> exported, AiJiraInfo info)
        {
            if (exported == null || !info.Found)
                return;

            if (!string.IsNullOrWhiteSpace(info.Home) && !Contains(exported, HomeVariable))
                exported.Add(new AgentEnvVariable { Key = HomeVariable, Value = info.Home });

            if (Contains(exported, "JIRA_BASE_URL"))
                return;

            string url = Value(exported, AgentEnvFile.KeyUrl);
            if (!string.IsNullOrWhiteSpace(url))
                exported.Add(new AgentEnvVariable { Key = "JIRA_BASE_URL", Value = url });
        }

        private static bool Contains(IList<AgentEnvVariable> variables, string key)
        {
            return !string.IsNullOrEmpty(Value(variables, key));
        }

        private static string Value(IList<AgentEnvVariable> variables, string key)
        {
            foreach (AgentEnvVariable variable in variables)
            {
                if (string.Equals(variable.Key, key, StringComparison.OrdinalIgnoreCase))
                    return variable.Value ?? string.Empty;
            }

            return string.Empty;
        }

        private static bool SafeExists(string path)
        {
            try
            {
                return !string.IsNullOrWhiteSpace(path) && File.Exists(path);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// The PowerShell host that will run the scripts.
        /// </summary>
        /// <remarks>
        /// <c>pwsh</c> is preferred over <c>powershell</c>: the scripts are written for
        /// it, and Windows PowerShell 5.1 defaults to a different output encoding,
        /// which garbles the accented text these cards carry.
        /// </remarks>
        private static async Task<string> ResolvePowerShellAsync(List<string> trail)
        {
            foreach (string candidate in new[] { "pwsh", "powershell" })
            {
                string resolved = await ResolveOnPathAsync(candidate);
                if (!string.IsNullOrWhiteSpace(resolved))
                {
                    trail.Add("[hit ] PATH: " + resolved);
                    return resolved;
                }

                trail.Add("[miss] PATH: " + candidate);
            }

            return string.Empty;
        }

        private static async Task<string> ResolveOnPathAsync(string command)
        {
            string probe = AgentShell.IsWindows ? "where " + command : "command -v " + command;
            ShellResult result = await AgentShell.RunAsync(probe, null, 10);

            if (!result.Success)
                return string.Empty;

            string first = result.FirstLine;
            return first.IndexOf(command, StringComparison.OrdinalIgnoreCase) >= 0
                ? first
                : string.Empty;
        }
    }
}
