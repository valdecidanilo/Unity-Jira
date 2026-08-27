using System.Collections.Generic;
using System.Text;

namespace OxenteGames.JiraCommunication.Agents
{
    /// <summary>
    /// Drives one agent CLI: builds its launch command and translates its stream
    /// dialect into <see cref="AgentEvent"/>.
    /// </summary>
    /// <remarks>
    /// This is the sibling of <c>IAiIssueClient</c>. That interface covers the
    /// stateless HTTP call that drafts issue fields; this one covers a long-running
    /// local agent that can read the project, edit files and run commands. They are
    /// deliberately separate: neither can do the other's job.
    /// </remarks>
    internal interface IAgentRunner
    {
        /// <summary>See <see cref="AgentProvider"/>.</summary>
        string Provider { get; }

        /// <summary>
        /// True when this CLI can continue a previous session from
        /// <see cref="AgentRequest.ResumeSessionId"/> in headless mode. The UI hides
        /// the continue action when false rather than emitting a flag the CLI would
        /// reject.
        /// </summary>
        bool SupportsResume { get; }

        /// <summary>
        /// The command line that runs the agent headlessly, emitting one JSON event per
        /// line on stdout. Redirection is added by <see cref="AgentScript"/>.
        /// </summary>
        string BuildCommandLine(AgentRequest request);

        /// <summary>The equivalent command for a visible terminal, where output is not tracked.</summary>
        string BuildInteractiveCommandLine(AgentRequest request);

        /// <summary>
        /// Converts one stream line into a normalized event, or null to ignore it.
        /// Must never throw: an unrecognized shape is data, not a failure.
        /// </summary>
        AgentEvent ParseLine(string line);
    }

    /// <summary>
    /// Wraps a provider command line in the launcher script that makes a run
    /// independent of the Editor.
    /// </summary>
    /// <remarks>
    /// Writing a script instead of composing one long shell invocation buys three
    /// things. The command lands in a file, so quoting is authored once instead of
    /// being escaped through nested shell layers; the prompt arrives on stdin from a
    /// file, so its length and contents cannot break the command line; and the exit
    /// code is recorded on disk, which is what lets the Editor tell "finished" from
    /// "still running" after a domain reload. The script is also directly replayable
    /// by hand, which makes a misbehaving run easy to diagnose.
    /// </remarks>
    internal static class AgentScript
    {
        public static string Build(AgentRunPaths paths, string workingDirectory, string commandLine,
            IList<AgentEnvVariable> environment = null)
        {
            var sb = new StringBuilder(512);

            if (AgentShell.IsWindows)
            {
                sb.AppendLine("@echo off");
                // Keep the CLI's UTF-8 output intact through the console layer.
                sb.AppendLine("chcp 65001 > nul");
                sb.AppendLine("cd /d \"" + workingDirectory + "\"");
                AppendEnvironment(sb, environment);

                // "call" is mandatory, not stylistic. An npm global install of these
                // CLIs is a .cmd shim, and a batch file that invokes another .cmd
                // without "call" transfers control instead of returning — the exit
                // line below would never run, and every run would look orphaned.
                sb.AppendLine("call " + commandLine
                              + " < \"" + paths.Prompt + "\""
                              + " > \"" + paths.Stream + "\""
                              + " 2> \"" + paths.StdErr + "\"");
                // The redirect goes first, deliberately. "echo %ERRORLEVEL%>file"
                // expands to "echo 3>file", and cmd reads a digit immediately before
                // ">" as a file-descriptor redirect — writing an empty file. Putting
                // the redirect first also avoids the trailing space that
                // "echo %ERRORLEVEL% > file" would append.
                sb.AppendLine(">\"" + paths.Exit + "\" echo %ERRORLEVEL%");
            }
            else
            {
                sb.AppendLine("#!/bin/sh");
                sb.AppendLine("cd \"" + workingDirectory + "\" || exit 1");
                AppendEnvironment(sb, environment);
                sb.AppendLine(commandLine
                              + " < \"" + paths.Prompt + "\""
                              + " > \"" + paths.Stream + "\""
                              + " 2> \"" + paths.StdErr + "\"");
                sb.AppendLine("echo $? > \"" + paths.Exit + "\"");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Emits the env file's variables as shell assignments ahead of the command.
        /// </summary>
        /// <remarks>
        /// Assignment inside the script is what makes the variables visible to a
        /// detached process: the run is launched by the shell, not by
        /// <c>ProcessStartInfo</c>, so an environment set on the Editor side would
        /// never reach the CLI. Values are escaped for the platform shell and any
        /// newline is dropped, because a value that broke out of its assignment would
        /// become an executable line in a script that runs with the developer's own
        /// permissions.
        /// </remarks>
        private static void AppendEnvironment(StringBuilder sb, IList<AgentEnvVariable> environment)
        {
            if (environment == null || environment.Count == 0)
                return;

            foreach (AgentEnvVariable variable in environment)
            {
                if (string.IsNullOrWhiteSpace(variable.Key))
                    continue;

                string value = (variable.Value ?? string.Empty)
                    .Replace("\r", string.Empty)
                    .Replace("\n", " ");

                if (AgentShell.IsWindows)
                {
                    // cmd has no escape for a quote inside a quoted assignment, so a
                    // quote is removed rather than silently ending the value early.
                    sb.AppendLine("set \"" + variable.Key + "=" + value.Replace("\"", string.Empty) + "\"");
                }
                else
                {
                    sb.AppendLine("export " + variable.Key + "='" + value.Replace("'", "'\\''") + "'");
                }
            }
        }

        /// <summary>Quotes a path or value for the platform shell.</summary>
        public static string Quote(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "\"\"";

            return value.IndexOf(' ') >= 0 ? "\"" + value + "\"" : value;
        }
    }
}
