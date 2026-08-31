using System;
using System.Text;
using System.Threading.Tasks;

namespace OxenteGames.JiraCommunication.Agents
{
    /// <summary>What one install or update attempt did.</summary>
    internal struct AiJiraInstallResult
    {
        public bool Success;

        /// <summary>Combined output of every step, for the panel's log.</summary>
        public string Output;

        /// <summary>Populated when <see cref="Success"/> is false.</summary>
        public string Error;
    }

    /// <summary>
    /// Installs or updates the local <c>ai-jira</c> checkout from the Editor.
    /// </summary>
    /// <remarks>
    /// This runs a script from someone else's repository on the developer's machine,
    /// so the shape matters more than the convenience. Two rules follow from that, and
    /// neither is negotiable:
    /// <list type="bullet">
    /// <item>the exact command line is shown before anything runs, built by
    /// <see cref="DescribeInstall"/>, and the panel requires a second, explicit click;</item>
    /// <item>the clone URL is a constant in this file — never assembled from anything
    /// the probe read off disk, and never from a value that reached us through an
    /// agent transcript.</item>
    /// </list>
    /// <para>
    /// Update is a separate path from install rather than a clone that overwrites.
    /// Deleting a directory the developer may have edited — ai-jira's own
    /// <c>config.json</c> lives in it — to re-clone on top would be data loss dressed
    /// up as a convenience.
    /// </para>
    /// </remarks>
    internal static class AiJiraInstaller
    {
        /// <summary>Clone source. A constant on purpose — see the type's remarks.</summary>
        private const string CloneUrl = "https://github.com/Mikael-Cavalcanti/ai-jira.git";

        /// <summary>Generous, because a cold clone on a slow link is not a failure.</summary>
        private const int CloneTimeoutSeconds = 180;

        private const int InstallTimeoutSeconds = 180;

        /// <summary>The command line an install would run, for the confirmation step.</summary>
        public static string DescribeInstall(AiJiraInfo info)
        {
            string target = TargetPath(info);
            var sb = new StringBuilder(256);

            if (info.Found)
                sb.Append("git -C ").Append(Quote(target)).AppendLine(" pull --ff-only");
            else
                sb.Append("git clone ").Append(CloneUrl).Append(' ').AppendLine(Quote(target));

            sb.Append(PowerShellName(info))
              .Append(" -NoProfile -ExecutionPolicy Bypass -File ")
              .Append(Quote(System.IO.Path.Combine(target, "install.ps1")));

            return sb.ToString();
        }

        /// <summary>Whether the machine has what an install needs, and what is missing.</summary>
        public static string BlockedReason(AiJiraInfo info)
        {
            if (!AgentShell.IsWindows)
                return "windows-only";

            if (!info.HasGit)
                return "git-missing";

            if (!info.HasPowerShell)
                return "powershell-missing";

            if (string.IsNullOrWhiteSpace(TargetPath(info)))
                return "no-home";

            return string.Empty;
        }

        /// <summary>
        /// Clones (or fast-forwards) the checkout, then runs its installer.
        /// </summary>
        /// <remarks>
        /// The two steps are run separately rather than chained in one shell line so a
        /// failure names which half broke. A clone that fails and an installer that
        /// fails call for completely different fixes, and "the command returned 1"
        /// distinguishes neither.
        /// <para>
        /// <c>--ff-only</c> on the update path: a checkout the developer committed to
        /// should stop with a message, not get a merge commit nobody asked for.
        /// </para>
        /// </remarks>
        public static async Task<AiJiraInstallResult> RunAsync(AiJiraInfo info)
        {
            string blocked = BlockedReason(info);
            if (!string.IsNullOrEmpty(blocked))
                return new AiJiraInstallResult { Success = false, Error = blocked };

            string target = TargetPath(info);
            var log = new StringBuilder(1024);

            string fetch = info.Found
                ? "git -C " + Quote(target) + " pull --ff-only"
                : "git clone " + CloneUrl + " " + Quote(target);

            ShellResult fetched = await AgentShell.RunAsync(fetch, null, CloneTimeoutSeconds);
            Append(log, fetch, fetched);

            if (!fetched.Success)
            {
                return new AiJiraInstallResult
                {
                    Success = false,
                    Output = log.ToString(),
                    Error = info.Found ? "update-failed" : "clone-failed"
                };
            }

            string install = PowerShellName(info)
                             + " -NoProfile -ExecutionPolicy Bypass -File "
                             + Quote(System.IO.Path.Combine(target, "install.ps1"));

            ShellResult installed = await AgentShell.RunAsync(install, target, InstallTimeoutSeconds);
            Append(log, install, installed);

            // The probe's answer is now stale in every field, including the ones the
            // panel is about to redraw from.
            AiJiraLocator.InvalidateCache();

            return new AiJiraInstallResult
            {
                Success = installed.Success,
                Output = log.ToString(),
                Error = installed.Success ? string.Empty : "install-failed"
            };
        }

        private static void Append(StringBuilder log, string command, ShellResult result)
        {
            log.Append("$ ").AppendLine(command);

            if (!string.IsNullOrWhiteSpace(result.StdOut))
                log.AppendLine(result.StdOut.TrimEnd());

            if (!string.IsNullOrWhiteSpace(result.StdErr))
                log.AppendLine(result.StdErr.TrimEnd());

            log.Append("[exit ").Append(result.ExitCode).AppendLine("]").AppendLine();
        }

        /// <summary>Where to install: the existing checkout, else the default location.</summary>
        private static string TargetPath(AiJiraInfo info)
        {
            return info.Found && !string.IsNullOrWhiteSpace(info.Home)
                ? info.Home
                : AiJiraLocator.DefaultInstallPath();
        }

        /// <summary>
        /// The PowerShell host to invoke, by name rather than by absolute path.
        /// </summary>
        /// <remarks>
        /// The probe's path is used only to decide which of the two exists. Passing the
        /// resolved path through the shell would quote a path containing spaces on
        /// exactly the machines where it matters, and both names resolve on PATH
        /// anyway — that is how the probe found them.
        /// </remarks>
        private static string PowerShellName(AiJiraInfo info)
        {
            string path = info.PowerShellPath ?? string.Empty;
            return path.IndexOf("pwsh", StringComparison.OrdinalIgnoreCase) >= 0
                ? "pwsh"
                : "powershell";
        }

        private static string Quote(string value)
        {
            return string.IsNullOrEmpty(value) ? "\"\"" : "\"" + value + "\"";
        }
    }
}
