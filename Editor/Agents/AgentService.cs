using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using OxenteGames.JiraCommunication.Git;
using OxenteGames.JiraCommunication.Settings;
using UnityEditor;
using UnityEngine;

namespace OxenteGames.JiraCommunication.Agents
{
    /// <summary>
    /// Tracks every agent run for this project and drives the transcript forward.
    /// </summary>
    /// <remarks>
    /// The design constraint that shapes this class is the Editor's AppDomain reload.
    /// Any script recompile or entering play mode wipes static state and would orphan
    /// a child process, so no run state is authoritative in memory: the run
    /// directories on disk are, and this class rebuilds itself from them on every load.
    /// <para>
    /// Progress is pulled on <see cref="EditorApplication.update"/> rather than pushed
    /// from a process handle, which is why a reload is survivable at all — there is no
    /// pipe to lose. Reads are throttled and byte-capped so tailing cannot make the
    /// Editor stutter, and no call here ever blocks the main thread waiting on a
    /// process.
    /// </para>
    /// </remarks>
    [InitializeOnLoad]
    internal static class AgentService
    {
        /// <summary>Seconds between tail reads. Fast enough to feel live, cheap enough to ignore.</summary>
        private const double PollInterval = 0.25d;

        private static readonly List<AgentRunInfo> RunList = new List<AgentRunInfo>();
        private static readonly Dictionary<string, AgentRunPaths> PathCache =
            new Dictionary<string, AgentRunPaths>();
        private static readonly HashSet<string> Hydrated = new HashSet<string>();

        private static double _nextPollAt;
        private static bool _restored;

        /// <summary>Raised when a run is added, removed, or changes status.</summary>
        public static event Action RunsChanged;

        /// <summary>Raised when new transcript events were appended to a run.</summary>
        public static event Action<AgentRunInfo> RunUpdated;

        static AgentService()
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        /// <summary>Runs known to this project, newest first.</summary>
        public static IReadOnlyList<AgentRunInfo> Runs
        {
            get
            {
                EnsureRestored();
                return RunList;
            }
        }

        public static bool HasActiveRun
        {
            get
            {
                EnsureRestored();
                foreach (AgentRunInfo run in RunList)
                {
                    if (run.IsRunning)
                        return true;
                }

                return false;
            }
        }

        public static AgentRunInfo Find(string runId)
        {
            EnsureRestored();

            foreach (AgentRunInfo run in RunList)
            {
                if (run.RunId == runId)
                    return run;
            }

            return null;
        }

        public static IAgentRunner CreateRunner(string provider)
        {
            return provider == AgentProvider.Codex
                ? (IAgentRunner)new CodexRunner()
                : new ClaudeCodeRunner();
        }

        // --- Restore ---------------------------------------------------------

        private static void EnsureRestored()
        {
            if (_restored)
                return;

            _restored = true;
            RunList.Clear();
            PathCache.Clear();
            Hydrated.Clear();

            foreach (string runId in AgentRunStore.ListRunIds())
            {
                AgentRunInfo info = AgentRunStore.LoadInfo(runId);
                if (info == null)
                    continue;

                Classify(info);
                RunList.Add(info);
            }

            // An in-flight run must resume streaming immediately; finished ones are
            // hydrated only when the user opens them, so a long history costs nothing.
            // The exception is a run recent enough to still count against the current
            // quota window and never accounted for — its tokens only exist inside its
            // stream, so leaving it unread would under-report usage after a restart.
            DateTime usageHorizon = DateTime.UtcNow.AddHours(
                -Math.Max(1, JiraPreferences.AgentUsageWindowHours));

            foreach (AgentRunInfo run in RunList)
            {
                if (run.IsRunning ||
                    (run.StartedAtUtc >= usageHorizon && !AgentUsageLedger.HasRecorded(run.RunId)))
                {
                    Hydrate(run);
                }
            }
        }

        private static AgentRunPaths PathsFor(AgentRunInfo info)
        {
            if (PathCache.TryGetValue(info.RunId, out AgentRunPaths cached))
                return cached;

            AgentRunPaths paths = AgentRunStore.PathsFor(info.RunId);
            PathCache[info.RunId] = paths;
            return paths;
        }

        /// <summary>Determines a run's status from the markers on disk.</summary>
        private static void Classify(AgentRunInfo info)
        {
            AgentRunPaths paths = PathsFor(info);

            if (AgentRunStore.IsCanceled(paths))
            {
                info.Status = AgentRunStatus.Canceled;
                return;
            }

            if (AgentRunStore.TryReadExit(paths, out int exitCode))
            {
                info.ExitCode = exitCode;
                info.Status = exitCode == 0 ? AgentRunStatus.Succeeded : AgentRunStatus.Failed;
                return;
            }

            // No exit marker. Either still running, or the process died without the
            // launcher getting to write one.
            if (AgentShell.IsProcessAlive(info.ProcessId, info.StartedAtUtc))
            {
                info.Status = AgentRunStatus.Running;
                return;
            }

            info.Status = AgentRunStatus.Orphaned;
        }

        /// <summary>Replays a run's whole stream to rebuild its transcript.</summary>
        public static void Hydrate(AgentRunInfo info)
        {
            if (info == null || Hydrated.Contains(info.RunId))
                return;

            Hydrated.Add(info.RunId);
            info.StreamOffset = 0;
            info.Events.Clear();
            Drain(info);
        }

        // --- Start / cancel --------------------------------------------------

        /// <summary>
        /// Resolves the CLI, writes the run directory and launches the agent.
        /// </summary>
        /// <returns>The tracked run, or null when the CLI could not be resolved.</returns>
        public static async Task<AgentRunInfo> StartAsync(AgentRequest request, Action<string> onError)
        {
            EnsureRestored();

            if (request == null)
                return null;

            if (string.IsNullOrWhiteSpace(request.Prompt))
            {
                onError?.Invoke("empty-prompt");
                return null;
            }

            AgentCliInfo cli = await AgentCliLocator.LocateAsync(request.Provider);
            if (!cli.Found)
            {
                onError?.Invoke(cli.Error ?? "not-found");
                return null;
            }

            request.ExecutablePath = cli.Path;

            if (string.IsNullOrWhiteSpace(request.WorkingDirectory))
                request.WorkingDirectory = await ResolveWorkingDirectoryAsync();

            AgentRunStore.PruneOldRuns();

            string runId = AgentRunStore.NewRunId();
            AgentRunPaths paths;

            try
            {
                paths = AgentRunStore.CreateRun(runId);
                AgentRunStore.WritePrompt(paths, request.Prompt);

                IAgentRunner runner = CreateRunner(request.Provider);
                string commandLine = runner.BuildCommandLine(request);
                AgentRunStore.WriteScript(paths,
                    AgentScript.Build(paths, request.WorkingDirectory, commandLine,
                        AgentEnvFile.Load()));
            }
            catch (Exception exception)
            {
                onError?.Invoke(exception.Message);
                return null;
            }

            DateTime startedAt = DateTime.UtcNow;
            int pid = AgentShell.StartDetached(paths.Script, request.WorkingDirectory, out string launchError);

            // Record the request even on failure: the directory is the audit trail.
            AgentRunStore.WriteRequest(paths, request, pid, startedAt);

            if (pid == 0)
            {
                AgentRunStore.WriteExit(paths, -1);
                onError?.Invoke(string.IsNullOrWhiteSpace(launchError) ? "launch-failed" : launchError);
                return null;
            }

            var info = new AgentRunInfo
            {
                RunId = runId,
                Directory = paths.Directory,
                Provider = request.Provider,
                Title = request.Title,
                IssueKey = request.IssueKey,
                Instruction = request.Instruction ?? string.Empty,
                ThreadId = string.IsNullOrWhiteSpace(request.ThreadId) ? runId : request.ThreadId,
                Model = request.Model ?? string.Empty,
                ResumedFrom = request.ResumeSessionId ?? string.Empty,
                StartedAtUtc = startedAt,
                ProcessId = pid,
                Status = AgentRunStatus.Running
            };

            PathCache[runId] = paths;
            Hydrated.Add(runId);
            RunList.Insert(0, info);

            RunsChanged?.Invoke();
            return info;
        }

        /// <summary>Kills a run's process tree and marks it canceled.</summary>
        public static async void Cancel(string runId)
        {
            AgentRunInfo info = Find(runId);
            if (info == null || !info.IsRunning)
                return;

            AgentRunPaths paths = PathsFor(info);

            // Mark first: if the kill takes the launcher down before it can write the
            // exit code, this marker is the only thing that distinguishes a deliberate
            // cancel from a crash.
            AgentRunStore.MarkCanceled(paths);
            info.Status = AgentRunStatus.Canceled;
            RunsChanged?.Invoke();

            await AgentShell.KillTreeAsync(info.ProcessId);

            Drain(info);
            RunUpdated?.Invoke(info);
        }

        public static void Delete(string runId)
        {
            AgentRunInfo info = Find(runId);
            if (info == null)
                return;

            if (info.IsRunning)
                return;

            AgentRunStore.Delete(runId);
            RunList.Remove(info);
            PathCache.Remove(runId);
            Hydrated.Remove(runId);
            RunsChanged?.Invoke();
        }

        public static void Refresh()
        {
            _restored = false;
            EnsureRestored();
            RunsChanged?.Invoke();
        }

        /// <summary>
        /// Working directory for a run: the configured repository, else the detected
        /// repository root, else the project folder.
        /// </summary>
        public static async Task<string> ResolveWorkingDirectoryAsync()
        {
            string configured = JiraPreferences.GitRepoPath;
            if (!string.IsNullOrWhiteSpace(configured) && Directory.Exists(configured))
                return configured;

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
                                 ?? Environment.CurrentDirectory;

            string repoRoot = await GitClient.GetRepoRootAsync(projectRoot);
            return string.IsNullOrWhiteSpace(repoRoot) ? projectRoot : repoRoot;
        }

        /// <summary>
        /// Every run of one conversation, oldest first.
        /// </summary>
        /// <remarks>
        /// A conversation is several runs because each turn is its own detached
        /// process — that is what keeps a turn alive across a domain reload. The thread
        /// id is what stitches them back into the single exchange the developer had.
        /// </remarks>
        public static List<AgentRunInfo> Thread(string threadId)
        {
            EnsureRestored();
            var turns = new List<AgentRunInfo>();

            if (string.IsNullOrWhiteSpace(threadId))
                return turns;

            foreach (AgentRunInfo run in RunList)
            {
                if (run.ThreadId == threadId)
                    turns.Add(run);
            }

            turns.Reverse();
            return turns;
        }

        /// <summary>
        /// The newest run of every conversation, newest first — one entry per thread.
        /// </summary>
        public static List<AgentRunInfo> Threads()
        {
            EnsureRestored();
            var heads = new List<AgentRunInfo>();
            var seen = new HashSet<string>();

            foreach (AgentRunInfo run in RunList)
            {
                string threadId = string.IsNullOrWhiteSpace(run.ThreadId) ? run.RunId : run.ThreadId;
                if (seen.Add(threadId))
                    heads.Add(run);
            }

            return heads;
        }

        /// <summary>The turn to resume when continuing a conversation, or null.</summary>
        public static AgentRunInfo LastResumable(string threadId)
        {
            List<AgentRunInfo> turns = Thread(threadId);

            for (int i = turns.Count - 1; i >= 0; i--)
            {
                if (turns[i].CanContinue)
                    return turns[i];
            }

            return null;
        }

        /// <summary>True while any turn of a conversation is still running.</summary>
        public static bool IsThreadBusy(string threadId)
        {
            foreach (AgentRunInfo run in Thread(threadId))
            {
                if (run.IsRunning)
                    return true;
            }

            return false;
        }

        // --- Pump ------------------------------------------------------------

        private static void Tick()
        {
            if (!_restored)
                return;

            double now = EditorApplication.timeSinceStartup;
            if (now < _nextPollAt)
                return;

            _nextPollAt = now + PollInterval;

            // Snapshot: handlers may mutate the list while we iterate.
            AgentRunInfo[] snapshot = RunList.ToArray();
            bool statusChanged = false;

            foreach (AgentRunInfo run in snapshot)
            {
                if (!run.IsRunning)
                    continue;

                bool appended = Drain(run);

                AgentRunPaths paths = PathsFor(run);

                if (AgentRunStore.TryReadExit(paths, out int exitCode))
                {
                    // Drain whatever the CLI wrote between the last read and its exit,
                    // so the terminal result event is never lost to a race.
                    while (Drain(run))
                    {
                    }

                    run.ExitCode = exitCode;
                    run.Status = exitCode == 0 ? AgentRunStatus.Succeeded : AgentRunStatus.Failed;

                    if (run.Status == AgentRunStatus.Failed && string.IsNullOrWhiteSpace(run.FinalText))
                        run.FinalText = BuildFailureText(paths, exitCode);

                    statusChanged = true;
                    appended = true;
                }
                else if (!AgentShell.IsProcessAlive(run.ProcessId, run.StartedAtUtc))
                {
                    while (Drain(run))
                    {
                    }

                    run.Status = AgentRunStatus.Orphaned;
                    if (string.IsNullOrWhiteSpace(run.FinalText))
                        run.FinalText = BuildFailureText(paths, -1);

                    statusChanged = true;
                    appended = true;
                }

                if (appended)
                    RunUpdated?.Invoke(run);
            }

            if (statusChanged)
                RunsChanged?.Invoke();
        }

        /// <summary>Reads and parses whatever is new on a run's stream.</summary>
        /// <returns>True when at least one event was appended.</returns>
        private static bool Drain(AgentRunInfo run)
        {
            AgentRunPaths paths = PathsFor(run);
            long offset = run.StreamOffset;
            List<string> lines = AgentRunStore.ReadNewLines(paths, ref offset);
            run.StreamOffset = offset;

            if (lines.Count == 0)
                return false;

            IAgentRunner runner = CreateRunner(run.Provider);
            bool appended = false;

            foreach (string line in lines)
            {
                AgentEvent parsed;

                try
                {
                    parsed = runner.ParseLine(line);
                }
                catch (Exception)
                {
                    // A parser bug must not stop the transcript.
                    parsed = AgentEvent.Simple(AgentEventKind.Unknown, "unparsed");
                }

                if (parsed == null)
                    continue;

                run.Events.Add(parsed);
                appended = true;

                if (parsed.Kind == AgentEventKind.Started && !string.IsNullOrWhiteSpace(parsed.Detail))
                    run.SessionId = parsed.Detail;

                if (parsed.Kind != AgentEventKind.Result)
                    continue;

                run.FinalText = parsed.Text;
                run.DurationMs = parsed.DurationMs;
                run.CostUsd = parsed.CostUsd;
                run.Usage = parsed.Usage;

                // Recorded here rather than on status change: the result event is the
                // only place the counters exist, and it is seen exactly once per run
                // whether the run just finished or its stream is being replayed.
                AgentUsageLedger.Record(run);
            }

            return appended;
        }

        /// <summary>
        /// Explains a failure using stderr, which is where a CLI reports the problems
        /// that never make it into the JSON stream — a bad flag, a missing login.
        /// </summary>
        private static string BuildFailureText(AgentRunPaths paths, int exitCode)
        {
            string stderr = AgentRunStore.ReadStdErr(paths);
            if (string.IsNullOrWhiteSpace(stderr))
                return "exit " + exitCode;

            string trimmed = stderr.Trim();
            const int limit = 800;
            if (trimmed.Length > limit)
                trimmed = trimmed.Substring(trimmed.Length - limit);

            return trimmed;
        }
    }
}
