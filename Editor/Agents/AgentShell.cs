using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace OxenteGames.JiraCommunication.Agents
{
    internal struct ShellResult
    {
        public bool Success;
        public int ExitCode;
        public string StdOut;
        public string StdErr;

        public string FirstLine
        {
            get
            {
                string source = !string.IsNullOrWhiteSpace(StdOut) ? StdOut : StdErr;
                if (string.IsNullOrWhiteSpace(source))
                    return string.Empty;

                foreach (string line in source.Split('\n'))
                {
                    string trimmed = line.Trim();
                    if (trimmed.Length > 0)
                        return trimmed;
                }

                return source.Trim();
            }
        }
    }

    /// <summary>
    /// Shell primitives shared by the agent layer: short probe commands, launching a
    /// detached run script, and killing a process tree.
    /// </summary>
    /// <remarks>
    /// Everything goes through the platform shell rather than invoking a binary
    /// directly. On Windows an npm global install of these CLIs produces a
    /// <c>claude.cmd</c> shim, which <c>UseShellExecute = false</c> cannot execute on
    /// its own — only <c>cmd.exe</c> applies PATHEXT. Routing through the shell also
    /// gives us stream redirection for free, which is what keeps a run's output
    /// independent of the Editor process.
    /// </remarks>
    internal static class AgentShell
    {
        // Deliberately not const: a const would let the compiler fold every
        // platform branch and report the other one as unreachable, filling the
        // Editor console with CS0162 warnings from this package.
#if UNITY_EDITOR_WIN
        public static readonly bool IsWindows = true;
#else
        public static readonly bool IsWindows = false;
#endif

        public static string ShellExecutable => IsWindows ? "cmd.exe" : "/bin/sh";

        private static string ShellArguments(string command)
        {
            // cmd.exe needs the whole command wrapped in quotes after /c so that
            // inner quotes survive; sh takes it as a single argv entry.
            return IsWindows ? "/c \"" + command + "\"" : "-c \"" + command.Replace("\"", "\\\"") + "\"";
        }

        /// <summary>Runs a short command and captures its output. For probes, not for runs.</summary>
        public static Task<ShellResult> RunAsync(string command, string workingDirectory, int timeoutSeconds = 20)
        {
            return Task.Run(() =>
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = ShellExecutable,
                    Arguments = ShellArguments(command),
                    WorkingDirectory = ResolveWorkingDirectory(workingDirectory),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                try
                {
                    using (var process = new Process { StartInfo = startInfo })
                    {
                        process.Start();

                        // Read both streams before waiting, or a full pipe buffer deadlocks.
                        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
                        Task<string> stderr = process.StandardError.ReadToEndAsync();

                        if (!process.WaitForExit(Math.Max(1, timeoutSeconds) * 1000))
                        {
                            TryKill(process);
                            return new ShellResult
                            {
                                Success = false,
                                ExitCode = -1,
                                StdOut = string.Empty,
                                StdErr = "timeout"
                            };
                        }

                        return new ShellResult
                        {
                            Success = process.ExitCode == 0,
                            ExitCode = process.ExitCode,
                            StdOut = stdout.Result ?? string.Empty,
                            StdErr = stderr.Result ?? string.Empty
                        };
                    }
                }
                catch (Exception exception)
                {
                    return new ShellResult
                    {
                        Success = false,
                        ExitCode = -1,
                        StdOut = string.Empty,
                        StdErr = exception.Message
                    };
                }
            });
        }

        /// <summary>
        /// Starts a run script without redirecting any stream into the Editor. The
        /// script writes its own output to files, so the child does not depend on a
        /// pipe that a domain reload would tear down.
        /// </summary>
        /// <returns>The launched shell's process id, or 0 when the launch failed.</returns>
        public static int StartDetached(string scriptPath, string workingDirectory, out string error)
        {
            error = string.Empty;

            string command = IsWindows
                ? "\"" + scriptPath + "\""
                : "\"" + scriptPath.Replace("\"", "\\\"") + "\"";

            var startInfo = new ProcessStartInfo
            {
                FileName = ShellExecutable,
                Arguments = ShellArguments(command),
                WorkingDirectory = ResolveWorkingDirectory(workingDirectory),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = false,
                RedirectStandardError = false,
                RedirectStandardInput = false
            };

            try
            {
                var process = new Process { StartInfo = startInfo };
                if (!process.Start())
                {
                    error = "could-not-start";
                    return 0;
                }

                int pid = process.Id;

                // We deliberately do not dispose or wait: ownership of the child ends
                // here. Progress and completion are observed through the run files.
                return pid;
            }
            catch (Win32Exception exception)
            {
                error = "shell-not-found: " + exception.Message;
                return 0;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return 0;
            }
        }

        /// <summary>
        /// Opens the command in a visible terminal window for interactive use. Such a
        /// run is not tracked: there is no stream file to tail.
        /// </summary>
        public static bool OpenInTerminal(string command, string workingDirectory, out string error)
        {
            error = string.Empty;

            try
            {
                ProcessStartInfo startInfo;

                if (IsWindows)
                {
                    startInfo = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        // /k keeps the window open after the agent exits.
                        Arguments = "/k \"" + command + "\"",
                        WorkingDirectory = ResolveWorkingDirectory(workingDirectory),
                        UseShellExecute = true,
                        CreateNoWindow = false
                    };
                }
                else if (UnityEngine.Application.platform == UnityEngine.RuntimePlatform.OSXEditor)
                {
                    string script =
                        "tell application \\\"Terminal\\\" to do script \\\"cd " +
                        EscapeForAppleScript(ResolveWorkingDirectory(workingDirectory)) + " && " +
                        EscapeForAppleScript(command) + "\\\"";

                    startInfo = new ProcessStartInfo
                    {
                        FileName = "/usr/bin/osascript",
                        Arguments = "-e \"" + script + "\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                }
                else
                {
                    startInfo = new ProcessStartInfo
                    {
                        FileName = "x-terminal-emulator",
                        Arguments = "-e " + ShellExecutable + " -c \"" + command.Replace("\"", "\\\"") + "; exec " + ShellExecutable + "\"",
                        WorkingDirectory = ResolveWorkingDirectory(workingDirectory),
                        UseShellExecute = false,
                        CreateNoWindow = false
                    };
                }

                Process process = Process.Start(startInfo);
                return process != null;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        private static string EscapeForAppleScript(string value)
        {
            return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\\\\\"");
        }

        /// <summary>
        /// Kills the launched shell and every descendant. Killing only the shell would
        /// orphan the CLI and its Node child, which would keep writing to the stream.
        /// </summary>
        public static Task KillTreeAsync(int processId)
        {
            if (processId <= 0)
                return Task.CompletedTask;

            string command = IsWindows
                ? "taskkill /T /F /PID " + processId
                // Children first, then the shell itself; ignore failures for already-dead pids.
                : "pkill -TERM -P " + processId + " ; kill -TERM " + processId + " ; exit 0";

            return RunAsync(command, null, 10);
        }

        /// <summary>
        /// True when a process with this id exists and did not start before the run we
        /// recorded. The start-time comparison guards against pid reuse, which matters
        /// because a run directory can outlive several Editor sessions.
        /// </summary>
        public static bool IsProcessAlive(int processId, DateTime startedAtUtc)
        {
            if (processId <= 0)
                return false;

            try
            {
                using (Process process = Process.GetProcessById(processId))
                {
                    if (process.HasExited)
                        return false;

                    if (startedAtUtc == default(DateTime))
                        return true;

                    // A reused pid would have started after our run did. Allow a minute
                    // of slack for clock skew between the recorded and reported times.
                    DateTime processStart = process.StartTime.ToUniversalTime();
                    return processStart <= startedAtUtc.AddMinutes(1);
                }
            }
            catch (ArgumentException)
            {
                // No process with that id.
                return false;
            }
            catch (Exception)
            {
                // Permission or platform quirk: assume alive rather than declaring a
                // healthy run dead, since the exit marker is the authoritative signal.
                return true;
            }
        }

        private static void TryKill(Process process)
        {
            try
            {
                if (!process.HasExited)
                    process.Kill();
            }
            catch (Exception)
            {
                // Nothing useful to do if the process already went away.
            }
        }

        private static string ResolveWorkingDirectory(string workingDirectory)
        {
            if (!string.IsNullOrWhiteSpace(workingDirectory) && Directory.Exists(workingDirectory))
                return workingDirectory;

            return Environment.CurrentDirectory;
        }
    }
}
