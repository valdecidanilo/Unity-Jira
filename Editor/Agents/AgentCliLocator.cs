using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using OxenteGames.JiraCommunication.Settings;

namespace OxenteGames.JiraCommunication.Agents
{
    /// <summary>Outcome of probing for one agent CLI.</summary>
    internal struct AgentCliInfo
    {
        public bool Found;
        public string Provider;

        /// <summary>Absolute path when we could resolve one, otherwise the bare command name.</summary>
        public string Path;

        /// <summary>Version string reported by the CLI, when it answered.</summary>
        public string Version;

        /// <summary>Populated when <see cref="Found"/> is false.</summary>
        public string Error;

        /// <summary>The command the run script should invoke.</summary>
        public string Command => string.IsNullOrWhiteSpace(Path) ? AgentCliLocator.CommandName(Provider) : Path;
    }

    /// <summary>
    /// Discovers the locally installed agent CLIs and reports whether the feature can
    /// run at all.
    /// </summary>
    /// <remarks>
    /// This exists because the failure it prevents is the most likely one in practice:
    /// a teammate enables the agent tab on a machine where the CLI was never
    /// installed. Without an explicit probe that surfaces as an unexplained empty
    /// transcript. Results are cached per provider because discovery shells out, and
    /// the cache is cleared whenever the user edits the override path.
    /// </remarks>
    internal static class AgentCliLocator
    {
        private static readonly Dictionary<string, AgentCliInfo> Cache = new Dictionary<string, AgentCliInfo>();

        public static string CommandName(string provider)
        {
            return provider == AgentProvider.Codex ? "codex" : "claude";
        }

        public static string InstallCommand(string provider)
        {
            return provider == AgentProvider.Codex
                ? "npm install -g @openai/codex"
                : "npm install -g @anthropic-ai/claude-code";
        }

        public static string InstallUrl(string provider)
        {
            return provider == AgentProvider.Codex
                ? "https://developers.openai.com/codex/cli"
                : "https://docs.claude.com/en/docs/claude-code/setup";
        }

        public static void InvalidateCache()
        {
            Cache.Clear();
        }

        /// <summary>Cached probe result, or null when this provider was never probed.</summary>
        public static AgentCliInfo? Cached(string provider)
        {
            return Cache.TryGetValue(provider ?? string.Empty, out AgentCliInfo info)
                ? info
                : (AgentCliInfo?)null;
        }

        public static async Task<AgentCliInfo> LocateAsync(string provider, bool forceRefresh = false)
        {
            provider = string.IsNullOrWhiteSpace(provider) ? AgentProvider.ClaudeCode : provider;

            if (!forceRefresh && Cache.TryGetValue(provider, out AgentCliInfo cached))
                return cached;

            AgentCliInfo info = await ProbeAsync(provider);
            Cache[provider] = info;
            return info;
        }

        private static async Task<AgentCliInfo> ProbeAsync(string provider)
        {
            // 1. An explicit override always wins, so a user with a non-standard
            //    install is never blocked by our discovery heuristics.
            string overridePath = JiraPreferences.GetAgentCliPath(provider);
            if (!string.IsNullOrWhiteSpace(overridePath))
            {
                if (!File.Exists(overridePath))
                {
                    return new AgentCliInfo
                    {
                        Found = false,
                        Provider = provider,
                        Path = overridePath,
                        Error = "override-missing"
                    };
                }

                string overrideVersion = await ReadVersionAsync(Quote(overridePath));
                return new AgentCliInfo
                {
                    Found = true,
                    Provider = provider,
                    Path = overridePath,
                    Version = overrideVersion
                };
            }

            string command = CommandName(provider);

            // 2. Ask the shell to resolve it, so PATHEXT and shell functions are honored.
            string resolved = await ResolveOnPathAsync(command);
            if (!string.IsNullOrWhiteSpace(resolved))
            {
                string pathVersion = await ReadVersionAsync(command);
                return new AgentCliInfo
                {
                    Found = true,
                    Provider = provider,
                    Path = resolved,
                    Version = pathVersion
                };
            }

            // 3. Fall back to the well-known install locations. Unity's Editor process
            //    does not always inherit the shell PATH a user set up interactively,
            //    especially on macOS when launched from Finder.
            foreach (string candidate in WellKnownPaths(command))
            {
                if (!File.Exists(candidate))
                    continue;

                string candidateVersion = await ReadVersionAsync(Quote(candidate));
                return new AgentCliInfo
                {
                    Found = true,
                    Provider = provider,
                    Path = candidate,
                    Version = candidateVersion
                };
            }

            return new AgentCliInfo
            {
                Found = false,
                Provider = provider,
                Path = string.Empty,
                Error = "not-found"
            };
        }

        private static async Task<string> ResolveOnPathAsync(string command)
        {
            string probe = AgentShell.IsWindows ? "where " + command : "command -v " + command;
            ShellResult result = await AgentShell.RunAsync(probe, null, 10);

            if (!result.Success)
                return string.Empty;

            string first = result.FirstLine;
            return first.IndexOf(command, StringComparison.OrdinalIgnoreCase) >= 0 ? first : string.Empty;
        }

        private static async Task<string> ReadVersionAsync(string commandOrPath)
        {
            ShellResult result = await AgentShell.RunAsync(commandOrPath + " --version", null, 25);
            return result.Success ? result.FirstLine : string.Empty;
        }

        private static IEnumerable<string> WellKnownPaths(string command)
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            if (AgentShell.IsWindows)
            {
                string appData = Environment.GetEnvironmentVariable("APPDATA") ?? string.Empty;
                string localAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA") ?? string.Empty;

                if (!string.IsNullOrEmpty(appData))
                {
                    yield return Path.Combine(appData, "npm", command + ".cmd");
                    yield return Path.Combine(appData, "npm", command + ".exe");
                }

                if (!string.IsNullOrEmpty(localAppData))
                {
                    yield return Path.Combine(localAppData, "Programs", command, command + ".exe");
                    yield return Path.Combine(localAppData, command, "bin", command + ".exe");
                }

                if (!string.IsNullOrEmpty(home))
                {
                    yield return Path.Combine(home, ".local", "bin", command + ".exe");
                    yield return Path.Combine(home, "." + command, "local", command + ".exe");
                }

                yield break;
            }

            if (!string.IsNullOrEmpty(home))
            {
                yield return Path.Combine(home, ".local", "bin", command);
                yield return Path.Combine(home, "." + command, "local", command);
                yield return Path.Combine(home, ".npm-global", "bin", command);
                yield return Path.Combine(home, ".bun", "bin", command);
            }

            yield return "/opt/homebrew/bin/" + command;
            yield return "/usr/local/bin/" + command;
            yield return "/usr/bin/" + command;
        }

        private static string Quote(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;
            return value.IndexOf(' ') >= 0 ? "\"" + value + "\"" : value;
        }
    }
}
