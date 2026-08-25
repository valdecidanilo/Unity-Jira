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

        /// <summary>
        /// Every location the probe examined, in order, each marked hit or miss.
        /// </summary>
        /// <remarks>
        /// Exists so a "not found" is diagnosable from inside the Editor. Without it,
        /// telling a wrong platform branch apart from a genuinely absent CLI means
        /// reproducing the probe by hand outside Unity.
        /// </remarks>
        public string[] SearchedPaths;

        /// <summary>The search trail as readable lines, for logging or the clipboard.</summary>
        public string Diagnostics
        {
            get
            {
                var sb = new System.Text.StringBuilder();
                sb.Append("provider: ").AppendLine(Provider);
                sb.Append("command: ").AppendLine(AgentCliLocator.CommandName(Provider));
                sb.Append("host is windows: ").AppendLine(AgentShell.IsWindows ? "yes" : "no");
                sb.Append("found: ").AppendLine(Found ? "yes" : "no");

                if (!string.IsNullOrWhiteSpace(Path))
                    sb.Append("path: ").AppendLine(Path);
                if (!string.IsNullOrWhiteSpace(Version))
                    sb.Append("version: ").AppendLine(Version);
                if (!string.IsNullOrWhiteSpace(Error))
                    sb.Append("error: ").AppendLine(Error);

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
            var trail = new List<string>();

            string overridePath = JiraPreferences.GetAgentCliPath(provider);
            if (!string.IsNullOrWhiteSpace(overridePath))
            {
                if (!File.Exists(overridePath))
                {
                    trail.Add("[miss] override: " + overridePath);
                    return new AgentCliInfo
                    {
                        Found = false,
                        Provider = provider,
                        Path = overridePath,
                        Error = "override-missing",
                        SearchedPaths = trail.ToArray()
                    };
                }

                trail.Add("[hit ] override: " + overridePath);
                string overrideVersion = await ReadVersionAsync(Quote(overridePath));
                return new AgentCliInfo
                {
                    Found = true,
                    Provider = provider,
                    Path = overridePath,
                    Version = overrideVersion,
                    SearchedPaths = trail.ToArray()
                };
            }

            trail.Add("[skip] override: not set");

            string command = CommandName(provider);

            // 2. Ask the shell to resolve it, so PATHEXT and shell functions are honored.
            string resolved = await ResolveOnPathAsync(command);
            if (!string.IsNullOrWhiteSpace(resolved))
            {
                trail.Add("[hit ] PATH: " + resolved);
                string pathVersion = await ReadVersionAsync(command);
                return new AgentCliInfo
                {
                    Found = true,
                    Provider = provider,
                    Path = resolved,
                    Version = pathVersion,
                    SearchedPaths = trail.ToArray()
                };
            }

            trail.Add("[miss] PATH: shell could not resolve " + command);

            // 3. Fall back to the well-known install locations. Unity's Editor process
            //    does not always inherit the shell PATH a user set up interactively,
            //    especially on macOS when launched from Finder.
            foreach (string candidate in WellKnownPaths(command))
            {
                if (!File.Exists(candidate))
                {
                    trail.Add("[miss] " + candidate);
                    continue;
                }

                trail.Add("[hit ] " + candidate);
                string candidateVersion = await ReadVersionAsync(Quote(candidate));
                return new AgentCliInfo
                {
                    Found = true,
                    Provider = provider,
                    Path = candidate,
                    Version = candidateVersion,
                    SearchedPaths = trail.ToArray()
                };
            }

            return new AgentCliInfo
            {
                Found = false,
                Provider = provider,
                Path = string.Empty,
                Error = "not-found",
                SearchedPaths = trail.ToArray()
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

        /// <summary>
        /// CLI binaries shipped inside the Claude desktop app, newest version first.
        /// </summary>
        /// <remarks>
        /// The desktop app bundles a full <c>claude</c> binary under a versioned
        /// directory but does not put it on PATH, so a developer who installed only
        /// the app has a working CLI that nothing can find. Probing here means the
        /// agent tab works for them with no extra install.
        /// <para>
        /// This is a fallback, deliberately ordered after PATH: a CLI the developer
        /// installed themselves should always win over an app-managed copy, whose
        /// directory churns on every app update.
        /// </para>
        /// </remarks>
        private static List<string> DesktopAppBundles(string command)
        {
            var found = new List<string>();
            string executable = AgentShell.IsWindows ? command + ".exe" : command;

            foreach (string root in DesktopAppRoots())
                CollectVersionedBinaries(root, executable, found);

            return found;
        }

        /// <summary>
        /// Every place the desktop app is known to keep its versioned CLI.
        /// </summary>
        /// <remarks>
        /// Windows has two shapes. A regular installer writes the real
        /// <c>%APPDATA%\Claude</c>. A Microsoft Store (MSIX) install runs in a package
        /// container, and while its writes usually surface at the same path, the
        /// container's own copy lives under <c>Packages\Claude*\LocalCache\Roaming</c>.
        /// Probing both means one machine's install shape cannot decide whether the
        /// feature works — the package family name is matched by wildcard because it
        /// carries a publisher hash that must not be hardcoded.
        /// </remarks>
        private static IEnumerable<string> DesktopAppRoots()
        {
            if (!AgentShell.IsWindows)
            {
                string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (!string.IsNullOrEmpty(home))
                {
                    yield return Path.Combine(home, "Library/Application Support/Claude/claude-code");
                    yield return Path.Combine(home, ".claude/claude-code");
                }

                yield break;
            }

            string appData = Environment.GetEnvironmentVariable("APPDATA");
            if (!string.IsNullOrEmpty(appData))
                yield return Path.Combine(Path.Combine(appData, "Claude"), "claude-code");

            string localAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
            if (string.IsNullOrEmpty(localAppData))
                yield break;

            // Store install: AppData\Local\Packages\Claude_<hash>\LocalCache\Roaming\Claude\claude-code
            foreach (string package in SafeDirectories(Path.Combine(localAppData, "Packages"), "Claude*"))
                yield return Path.Combine(package, "LocalCache", "Roaming", "Claude", "claude-code");
        }

        /// <summary>Adds every versioned binary under a root, newest version first.</summary>
        private static void CollectVersionedBinaries(string root, string executable, List<string> found)
        {
            var versions = new List<string>(SafeDirectories(root, "*"));
            if (versions.Count == 0)
                return;

            // Newest first, comparing as versions so 2.1.10 sorts above 2.1.9.
            versions.Sort((left, right) => CompareVersionDirs(right, left));

            foreach (string directory in versions)
            {
                string candidate = Path.Combine(directory, executable);

                try
                {
                    if (File.Exists(candidate) && !found.Contains(candidate))
                        found.Add(candidate);
                }
                catch (Exception)
                {
                    // An unreadable entry is simply not a candidate.
                }
            }
        }

        private static string[] SafeDirectories(string path, string pattern)
        {
            try
            {
                return Directory.Exists(path)
                    ? Directory.GetDirectories(path, pattern)
                    : new string[0];
            }
            catch (Exception)
            {
                // Missing or permission-denied: no candidates from this root.
                return new string[0];
            }
        }

        private static int CompareVersionDirs(string left, string right)
        {
            string leftName = Path.GetFileName(left) ?? string.Empty;
            string rightName = Path.GetFileName(right) ?? string.Empty;

            if (Version.TryParse(leftName, out Version leftVersion) &&
                Version.TryParse(rightName, out Version rightVersion))
            {
                return leftVersion.CompareTo(rightVersion);
            }

            return string.Compare(leftName, rightName, StringComparison.OrdinalIgnoreCase);
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

                // Last: the copy the desktop app manages for itself.
                foreach (string bundled in DesktopAppBundles(command))
                    yield return bundled;

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

            foreach (string bundled in DesktopAppBundles(command))
                yield return bundled;
        }

        private static string Quote(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;
            return value.IndexOf(' ') >= 0 ? "\"" + value + "\"" : value;
        }
    }
}
