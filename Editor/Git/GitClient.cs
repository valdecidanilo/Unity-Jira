using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace OxenteGames.JiraCommunication.Git
{
    /// <summary>
    /// Result of a single <c>git</c> invocation.
    /// </summary>
    internal struct GitResult
    {
        public bool Success;
        public int ExitCode;
        public string StdOut;
        public string StdErr;

        /// <summary>First non-empty line of stderr (or stdout) for compact status display.</summary>
        public string ShortMessage
        {
            get
            {
                string source = !string.IsNullOrWhiteSpace(StdErr) ? StdErr : StdOut;
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

        public static GitResult NotFound => new GitResult
        {
            Success = false,
            ExitCode = -1,
            StdOut = string.Empty,
            StdErr = "git-not-found"
        };
    }

    /// <summary>
    /// Thin wrapper around the local <c>git</c> executable. All calls run off the
    /// main thread via <see cref="Task.Run"/>; because the Unity Editor installs a
    /// synchronization context, awaiting these resumes back on the main thread.
    /// </summary>
    internal static class GitClient
    {
        /// <summary>True when the caught exception means git is not installed / not on PATH.</summary>
        public static bool IsGitMissing(GitResult result)
        {
            return !result.Success && result.ExitCode == -1 && result.StdErr == "git-not-found";
        }

        public static Task<GitResult> RunAsync(string arguments, string workingDirectory)
        {
            return Task.Run(() =>
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = arguments,
                    WorkingDirectory = string.IsNullOrEmpty(workingDirectory) ? Environment.CurrentDirectory : workingDirectory,
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

                        // Read both streams concurrently to avoid a full-buffer deadlock.
                        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
                        Task<string> stderrTask = process.StandardError.ReadToEndAsync();
                        process.WaitForExit();

                        string stdout = stdoutTask.Result ?? string.Empty;
                        string stderr = stderrTask.Result ?? string.Empty;

                        return new GitResult
                        {
                            Success = process.ExitCode == 0,
                            ExitCode = process.ExitCode,
                            StdOut = stdout,
                            StdErr = stderr
                        };
                    }
                }
                catch (Win32Exception)
                {
                    // git executable not found on PATH.
                    return GitResult.NotFound;
                }
                catch (Exception exception)
                {
                    return new GitResult
                    {
                        Success = false,
                        ExitCode = -1,
                        StdOut = string.Empty,
                        StdErr = exception.Message
                    };
                }
            });
        }

        /// <summary>Resolves the repository root (absolute path) that contains <paramref name="startDir"/>.</summary>
        public static async Task<string> GetRepoRootAsync(string startDir)
        {
            GitResult result = await RunAsync("rev-parse --show-toplevel", startDir);
            return result.Success ? result.StdOut.Trim() : string.Empty;
        }

        /// <summary>Current branch name, or empty when detached / not a repo.</summary>
        public static async Task<string> GetCurrentBranchAsync(string repoRoot)
        {
            GitResult result = await RunAsync("rev-parse --abbrev-ref HEAD", repoRoot);
            return result.Success ? result.StdOut.Trim() : string.Empty;
        }

        /// <summary>True when a local branch with the given name already exists.</summary>
        public static async Task<bool> BranchExistsAsync(string repoRoot, string branch)
        {
            if (string.IsNullOrWhiteSpace(branch))
                return false;

            GitResult result = await RunAsync(
                $"show-ref --verify --quiet refs/heads/{QuoteArg(branch)}", repoRoot);
            return result.Success;
        }

        /// <summary>
        /// Checks out <paramref name="branch"/> if it exists, otherwise creates it.
        /// When <paramref name="baseBranch"/> is provided the new branch starts from it;
        /// otherwise it branches off the current HEAD.
        /// </summary>
        public static async Task<GitResult> CreateOrCheckoutBranchAsync(string repoRoot, string branch, string baseBranch)
        {
            if (string.IsNullOrWhiteSpace(branch))
                return new GitResult { Success = false, ExitCode = -1, StdErr = "empty-branch" };

            if (await BranchExistsAsync(repoRoot, branch))
                return await RunAsync($"checkout {QuoteArg(branch)}", repoRoot);

            string baseArg = string.IsNullOrWhiteSpace(baseBranch) ? string.Empty : " " + QuoteArg(baseBranch);
            return await RunAsync($"checkout -b {QuoteArg(branch)}{baseArg}", repoRoot);
        }

        private static string QuoteArg(string value)
        {
            // Branch names from our conventions are slugged, but base branch / repo paths
            // can contain spaces; quote defensively.
            if (string.IsNullOrEmpty(value))
                return "\"\"";
            return value.IndexOf(' ') >= 0 ? "\"" + value + "\"" : value;
        }
    }
}
