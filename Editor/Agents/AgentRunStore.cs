using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using OxenteGames.JiraCommunication.Models;
using UnityEngine;

namespace OxenteGames.JiraCommunication.Agents
{
    /// <summary>Absolute paths that make up one run directory.</summary>
    internal sealed class AgentRunPaths
    {
        public string RunId = string.Empty;
        public string Directory = string.Empty;
        public string Request = string.Empty;
        public string Prompt = string.Empty;
        public string Script = string.Empty;
        public string Stream = string.Empty;
        public string StdErr = string.Empty;
        public string Exit = string.Empty;

        /// <summary>
        /// Marker written when the user cancels. Needed because killing the process
        /// tree also kills the shell that would have written the exit code, so a
        /// canceled run is otherwise indistinguishable from a crashed one.
        /// </summary>
        public string Canceled = string.Empty;
    }

    /// <summary>
    /// Owns the on-disk protocol for agent runs.
    /// </summary>
    /// <remarks>
    /// The layout is the whole reason this feature survives the Editor. A run is a
    /// directory under <c>Library/JiraAgent</c>; the CLI writes its stream straight to
    /// a file and the Editor only ever reads. That means a script recompile, entering
    /// play mode, or quitting Unity cannot lose a run in progress, and it gives
    /// transcript persistence and replay without a second mechanism. <c>Library</c> is
    /// per-project and already ignored by version control, so nothing here is
    /// committed by accident.
    /// <para>
    /// There is intentionally no index file. Enumerating directories is self-healing:
    /// an index could disagree with reality after a crash, a directory listing cannot.
    /// </para>
    /// </remarks>
    internal static class AgentRunStore
    {
        private const string FolderName = "JiraAgent";
        private const int MaxTailBytesPerRead = 256 * 1024;

        /// <summary>Runs kept on disk; older ones are pruned when a new run starts.</summary>
        private const int MaxRetainedRuns = 40;

        public static string Root
        {
            get
            {
                // Application.dataPath points at <project>/Assets.
                string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
                                     ?? Environment.CurrentDirectory;
                return Path.Combine(Path.Combine(projectRoot, "Library"), FolderName);
            }
        }

        public static string NewRunId()
        {
            // Sortable prefix so a directory listing is chronological, plus a short
            // suffix so two runs started in the same second cannot collide.
            return DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)
                   + "-" + Guid.NewGuid().ToString("N").Substring(0, 6);
        }

        public static AgentRunPaths PathsFor(string runId)
        {
            string directory = Path.Combine(Root, runId);
            return new AgentRunPaths
            {
                RunId = runId,
                Directory = directory,
                Request = Path.Combine(directory, "request.json"),
                Prompt = Path.Combine(directory, "prompt.txt"),
                Script = Path.Combine(directory, AgentShell.IsWindows ? "run.cmd" : "run.sh"),
                Stream = Path.Combine(directory, "stream.jsonl"),
                StdErr = Path.Combine(directory, "stderr.log"),
                Exit = Path.Combine(directory, "exit"),
                Canceled = Path.Combine(directory, "canceled")
            };
        }

        public static void MarkCanceled(AgentRunPaths paths)
        {
            try
            {
                File.WriteAllText(paths.Canceled,
                    DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture), new UTF8Encoding(false));
            }
            catch (Exception)
            {
                // The in-memory status still reflects the cancel for this session.
            }
        }

        public static bool IsCanceled(AgentRunPaths paths)
        {
            try
            {
                return File.Exists(paths.Canceled);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static AgentRunPaths CreateRun(string runId)
        {
            AgentRunPaths paths = PathsFor(runId);
            Directory.CreateDirectory(paths.Directory);

            // Pre-create the stream so the tailer can open it before the CLI has
            // produced its first line.
            if (!File.Exists(paths.Stream))
                File.WriteAllText(paths.Stream, string.Empty, new UTF8Encoding(false));

            return paths;
        }

        public static void WritePrompt(AgentRunPaths paths, string prompt)
        {
            File.WriteAllText(paths.Prompt, prompt ?? string.Empty, new UTF8Encoding(false));
        }

        public static void WriteScript(AgentRunPaths paths, string body)
        {
            File.WriteAllText(paths.Script, body ?? string.Empty, new UTF8Encoding(false));

            if (AgentShell.IsWindows)
                return;

            // The launcher is invoked through sh, but mark it executable anyway so the
            // user can replay a run from their own terminal.
            try
            {
                AgentShell.RunAsync("chmod +x \"" + paths.Script + "\"", paths.Directory, 5);
            }
            catch (Exception)
            {
                // Not fatal: sh <script> works regardless of the executable bit.
            }
        }

        public static void WriteRequest(AgentRunPaths paths, AgentRequest request, int processId, DateTime startedAtUtc)
        {
            var sb = new StringBuilder(512);
            sb.Append('{');
            Append(sb, "runId", paths.RunId);
            sb.Append(',');
            Append(sb, "provider", request.Provider);
            sb.Append(',');
            Append(sb, "title", request.Title);
            sb.Append(',');
            Append(sb, "issueKey", request.IssueKey);
            sb.Append(',');
            Append(sb, "permissionMode", request.PermissionMode);
            sb.Append(',');
            Append(sb, "workingDirectory", request.WorkingDirectory);
            sb.Append(',');
            Append(sb, "executablePath", request.ExecutablePath);
            sb.Append(',');
            Append(sb, "startedAtUtc", startedAtUtc.ToString("o", CultureInfo.InvariantCulture));
            sb.Append(',');
            sb.Append("\"processId\":").Append(processId.ToString(CultureInfo.InvariantCulture));
            sb.Append('}');

            File.WriteAllText(paths.Request, sb.ToString(), new UTF8Encoding(false));
        }

        private static void Append(StringBuilder sb, string key, string value)
        {
            sb.Append('"').Append(key).Append("\":\"")
              .Append(JiraIssueDraft.JsonEscape(value ?? string.Empty)).Append('"');
        }

        /// <summary>Writes the exit marker ourselves, for failures that happen before launch.</summary>
        public static void WriteExit(AgentRunPaths paths, int exitCode)
        {
            try
            {
                File.WriteAllText(paths.Exit, exitCode.ToString(CultureInfo.InvariantCulture),
                    new UTF8Encoding(false));
            }
            catch (Exception)
            {
                // Best effort; the orphan check will classify the run instead.
            }
        }

        public static bool TryReadExit(AgentRunPaths paths, out int exitCode)
        {
            exitCode = 0;

            try
            {
                if (!File.Exists(paths.Exit))
                    return false;

                string text = File.ReadAllText(paths.Exit).Trim();
                if (text.Length == 0)
                {
                    // The shell created the file but has not flushed the code yet.
                    return false;
                }

                if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out exitCode))
                    return true;

                // Unparseable content still means the run finished; treat as failure.
                exitCode = -1;
                return true;
            }
            catch (IOException)
            {
                // Being written right now; try again on the next tick.
                return false;
            }
        }

        public static string ReadStdErr(AgentRunPaths paths)
        {
            try
            {
                return File.Exists(paths.StdErr) ? File.ReadAllText(paths.StdErr) : string.Empty;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Reads whole lines appended since <paramref name="offset"/> and advances it.
        /// </summary>
        /// <remarks>
        /// Only bytes up to the final newline are consumed, so a line the CLI is still
        /// writing is never handed out half-parsed and no partial-line buffer is needed
        /// between calls. The read is capped so one tick cannot stall the Editor on a
        /// run that produced a large burst of output.
        /// </remarks>
        public static List<string> ReadNewLines(AgentRunPaths paths, ref long offset)
        {
            var lines = new List<string>();

            try
            {
                if (!File.Exists(paths.Stream))
                    return lines;

                // FileShare.ReadWrite is required: the CLI holds this file open for writing.
                using (var stream = new FileStream(paths.Stream, FileMode.Open, FileAccess.Read,
                           FileShare.ReadWrite | FileShare.Delete))
                {
                    if (stream.Length < offset)
                    {
                        // File was replaced or truncated; restart from the beginning.
                        offset = 0;
                    }

                    long available = stream.Length - offset;
                    if (available <= 0)
                        return lines;

                    int count = (int)Math.Min(available, MaxTailBytesPerRead);
                    var buffer = new byte[count];

                    stream.Seek(offset, SeekOrigin.Begin);
                    int read = stream.Read(buffer, 0, count);
                    if (read <= 0)
                        return lines;

                    int lastNewline = -1;
                    for (int i = read - 1; i >= 0; i--)
                    {
                        if (buffer[i] == (byte)'\n')
                        {
                            lastNewline = i;
                            break;
                        }
                    }

                    if (lastNewline < 0)
                    {
                        // No complete line yet. Leave the offset alone and retry later.
                        return lines;
                    }

                    int consumed = lastNewline + 1;
                    string text = new UTF8Encoding(false).GetString(buffer, 0, consumed);
                    offset += consumed;

                    foreach (string line in text.Split('\n'))
                    {
                        string trimmed = line.Trim('\r', ' ', '\t');
                        if (trimmed.Length > 0)
                            lines.Add(trimmed);
                    }
                }
            }
            catch (IOException)
            {
                // Transient sharing violation; the next tick picks up where we left off.
            }
            catch (Exception)
            {
                // Never let tailing throw into the Editor update loop.
            }

            return lines;
        }

        /// <summary>Run ids on disk, newest first.</summary>
        public static List<string> ListRunIds()
        {
            var ids = new List<string>();

            try
            {
                if (!Directory.Exists(Root))
                    return ids;

                foreach (string directory in Directory.GetDirectories(Root))
                    ids.Add(Path.GetFileName(directory));

                // Run ids start with a sortable UTC timestamp.
                ids.Sort(StringComparer.OrdinalIgnoreCase);
                ids.Reverse();
            }
            catch (Exception)
            {
                // Missing or unreadable root: report no history rather than failing.
            }

            return ids;
        }

        /// <summary>Rebuilds run metadata from <c>request.json</c>. Returns null when unreadable.</summary>
        public static AgentRunInfo LoadInfo(string runId)
        {
            AgentRunPaths paths = PathsFor(runId);

            if (!Directory.Exists(paths.Directory))
                return null;

            var info = new AgentRunInfo
            {
                RunId = runId,
                Directory = paths.Directory
            };

            try
            {
                if (File.Exists(paths.Request))
                {
                    object node = AgentJson.Parse(File.ReadAllText(paths.Request));
                    info.Provider = AgentJson.String(node, "provider") ?? AgentProvider.ClaudeCode;
                    info.Title = AgentJson.String(node, "title") ?? string.Empty;
                    info.IssueKey = AgentJson.String(node, "issueKey") ?? string.Empty;
                    info.ProcessId = (int)AgentJson.Number(node, "processId");

                    string started = AgentJson.String(node, "startedAtUtc");
                    if (!string.IsNullOrWhiteSpace(started) &&
                        DateTime.TryParse(started, CultureInfo.InvariantCulture,
                            DateTimeStyles.RoundtripKind, out DateTime parsed))
                    {
                        info.StartedAtUtc = parsed.ToUniversalTime();
                    }
                }
            }
            catch (Exception)
            {
                // Keep the run visible with whatever we could recover.
            }

            return info;
        }

        /// <summary>Deletes one run directory.</summary>
        public static void Delete(string runId)
        {
            try
            {
                string directory = Path.Combine(Root, runId);
                if (Directory.Exists(directory))
                    Directory.Delete(directory, true);
            }
            catch (Exception)
            {
                // A locked file only means the directory lingers; not worth surfacing.
            }
        }

        /// <summary>Drops the oldest finished runs so the folder cannot grow without bound.</summary>
        public static void PruneOldRuns()
        {
            List<string> ids = ListRunIds();
            if (ids.Count <= MaxRetainedRuns)
                return;

            for (int i = MaxRetainedRuns; i < ids.Count; i++)
            {
                AgentRunPaths paths = PathsFor(ids[i]);

                // Only prune runs that already finished.
                if (TryReadExit(paths, out int _))
                    Delete(ids[i]);
            }
        }
    }
}
