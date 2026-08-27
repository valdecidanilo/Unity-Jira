using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using OxenteGames.JiraCommunication.Settings;

namespace OxenteGames.JiraCommunication.Agents
{
    /// <summary>One finished run's token cost, as recorded in the ledger.</summary>
    internal struct AgentUsageEntry
    {
        public string RunId;
        public string Provider;
        public DateTime FinishedAtUtc;
        public AgentUsage Usage;
        public double CostUsd;
    }

    /// <summary>
    /// Consumption inside the current quota window.
    /// </summary>
    internal struct AgentUsageWindow
    {
        /// <summary>False when nothing has been spent inside a live window.</summary>
        public bool Active;

        public DateTime StartUtc;
        public DateTime EndUtc;
        public AgentUsage Usage;
        public double CostUsd;
        public int RunCount;

        /// <summary>Configured token allowance for one window, or zero when unset.</summary>
        public long Budget;

        /// <summary>Share of the budget already spent, 0..1. Zero when no budget is set.</summary>
        public float Fraction =>
            Budget <= 0 ? 0f : Math.Min(1f, (float)((double)Usage.Total / Budget));

        public long Remaining => Budget <= 0 ? 0 : Math.Max(0, Budget - Usage.Total);

        public TimeSpan TimeToReset
        {
            get
            {
                TimeSpan left = EndUtc - DateTime.UtcNow;
                return left < TimeSpan.Zero ? TimeSpan.Zero : left;
            }
        }
    }

    /// <summary>
    /// Records what each run consumed and reports it per quota window.
    /// </summary>
    /// <remarks>
    /// The window has to be reconstructed locally because neither CLI exposes the
    /// plan's remaining quota — the only number either one reports is what the run
    /// itself used. So this keeps an append-only ledger next to the run directories
    /// and groups it the way the plans do: a window opens with the first run after a
    /// quiet period and lasts a fixed number of hours, after which the next run opens
    /// a new one. That makes the percentage an estimate of this machine's own
    /// consumption, not an authoritative quota reading, which is why the UI labels it
    /// against a budget the developer sets.
    /// <para>
    /// Entries are keyed by run id and written once. Replaying a run's stream after a
    /// domain reload re-emits its result event, and without that key every reload
    /// would count the same run again.
    /// </para>
    /// </remarks>
    internal static class AgentUsageLedger
    {
        private const string FileName = "usage.jsonl";

        /// <summary>How long history is kept. Long enough for a monthly view, bounded.</summary>
        private const int RetentionDays = 45;

        private static readonly List<AgentUsageEntry> Entries = new List<AgentUsageEntry>();
        private static readonly HashSet<string> KnownRunIds = new HashSet<string>();
        private static bool _loaded;

        /// <summary>Raised when a run was added to the ledger.</summary>
        public static event Action Changed;

        public static string FilePath => Path.Combine(AgentRunStore.Root, FileName);

        /// <summary>Every recorded run, oldest first.</summary>
        public static IReadOnlyList<AgentUsageEntry> All
        {
            get
            {
                EnsureLoaded();
                return Entries;
            }
        }

        public static bool HasRecorded(string runId)
        {
            EnsureLoaded();
            return !string.IsNullOrEmpty(runId) && KnownRunIds.Contains(runId);
        }

        /// <summary>
        /// Appends a run's consumption. Runs with no counters and runs already in the
        /// ledger are ignored, so callers can record unconditionally.
        /// </summary>
        public static void Record(AgentRunInfo run)
        {
            if (run == null || string.IsNullOrEmpty(run.RunId) || !run.Usage.HasData)
                return;

            EnsureLoaded();

            if (!KnownRunIds.Add(run.RunId))
                return;

            var entry = new AgentUsageEntry
            {
                RunId = run.RunId,
                Provider = run.Provider,
                FinishedAtUtc = FinishedAt(run),
                Usage = run.Usage,
                CostUsd = run.CostUsd
            };

            Entries.Add(entry);
            Append(entry);
            Changed?.Invoke();
        }

        /// <summary>
        /// When a run's consumption happened, derived from the run itself.
        /// </summary>
        /// <remarks>
        /// Not the wall clock at record time. A run that finished before the Editor
        /// was opened is only replayed when its transcript is first read, and stamping
        /// that moment would charge yesterday's tokens to today's window.
        /// </remarks>
        private static DateTime FinishedAt(AgentRunInfo run)
        {
            if (run.StartedAtUtc == default(DateTime))
                return DateTime.UtcNow;

            DateTime finished = run.DurationMs > 0
                ? run.StartedAtUtc.AddMilliseconds(run.DurationMs)
                : run.StartedAtUtc;

            return finished > DateTime.UtcNow ? DateTime.UtcNow : finished;
        }

        /// <summary>Drops the whole ledger. Percentages restart from the next run.</summary>
        public static void Clear()
        {
            Entries.Clear();
            KnownRunIds.Clear();
            _loaded = true;

            try
            {
                if (File.Exists(FilePath))
                    File.Delete(FilePath);
            }
            catch (Exception)
            {
                // The in-memory ledger is already empty; a locked file only means the
                // old numbers come back on the next Editor start.
            }

            Changed?.Invoke();
        }

        /// <summary>
        /// The window in force right now.
        /// </summary>
        /// <remarks>
        /// Windows are anchored, not sliding: walking the ledger oldest-first, the
        /// first entry after a gap opens a window that lasts <c>WindowHours</c>, and
        /// everything inside it belongs to that window. A sliding sum would keep
        /// moving the reset time forward as runs come in and could never show a
        /// developer when their allowance actually comes back.
        /// </remarks>
        public static AgentUsageWindow CurrentWindow()
        {
            EnsureLoaded();

            var window = new AgentUsageWindow
            {
                Budget = JiraPreferences.AgentTokenBudget
            };

            TimeSpan length = TimeSpan.FromHours(Math.Max(1, JiraPreferences.AgentUsageWindowHours));
            DateTime now = DateTime.UtcNow;

            DateTime start = default(DateTime);
            var usage = new AgentUsage();
            double cost = 0d;
            int count = 0;

            foreach (AgentUsageEntry entry in Entries)
            {
                bool opensWindow = start == default(DateTime) || entry.FinishedAtUtc >= start + length;

                if (opensWindow)
                {
                    start = entry.FinishedAtUtc;
                    usage = new AgentUsage();
                    cost = 0d;
                    count = 0;
                }

                usage.Add(entry.Usage);
                cost += entry.CostUsd;
                count++;
            }

            if (start == default(DateTime) || start + length <= now)
            {
                // Nothing spent, or the last window already expired: the allowance is
                // whole again and there is no reset to count down to.
                return window;
            }

            window.Active = true;
            window.StartUtc = start;
            window.EndUtc = start + length;
            window.Usage = usage;
            window.CostUsd = cost;
            window.RunCount = count;
            return window;
        }

        // --- Persistence -----------------------------------------------------

        private static void EnsureLoaded()
        {
            if (_loaded)
                return;

            _loaded = true;
            Entries.Clear();
            KnownRunIds.Clear();

            try
            {
                if (!File.Exists(FilePath))
                    return;

                DateTime cutoff = DateTime.UtcNow.AddDays(-RetentionDays);
                bool dropped = false;

                foreach (string line in File.ReadAllLines(FilePath))
                {
                    if (!TryParse(line, out AgentUsageEntry entry))
                        continue;

                    if (entry.FinishedAtUtc < cutoff)
                    {
                        dropped = true;
                        continue;
                    }

                    if (KnownRunIds.Add(entry.RunId))
                        Entries.Add(entry);
                }

                // Entries are appended in completion order, but a hand-edited or
                // partially-written file must not be trusted to be sorted.
                Entries.Sort((a, b) => a.FinishedAtUtc.CompareTo(b.FinishedAtUtc));

                if (dropped)
                    Rewrite();
            }
            catch (Exception)
            {
                // A missing or unreadable ledger means no history, never a broken tab.
            }
        }

        private static bool TryParse(string line, out AgentUsageEntry entry)
        {
            entry = default(AgentUsageEntry);

            object node = AgentJson.Parse(line);
            if (node == null)
                return false;

            string runId = AgentJson.String(node, "runId");
            if (string.IsNullOrWhiteSpace(runId))
                return false;

            string finished = AgentJson.String(node, "at");
            if (!DateTime.TryParse(finished, CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out DateTime at))
            {
                return false;
            }

            entry = new AgentUsageEntry
            {
                RunId = runId,
                Provider = AgentJson.String(node, "provider") ?? AgentProvider.ClaudeCode,
                FinishedAtUtc = at.ToUniversalTime(),
                CostUsd = AgentJson.Number(node, "cost"),
                Usage = new AgentUsage
                {
                    InputTokens = (long)AgentJson.Number(node, "in"),
                    OutputTokens = (long)AgentJson.Number(node, "out"),
                    CacheReadTokens = (long)AgentJson.Number(node, "cacheRead"),
                    CacheWriteTokens = (long)AgentJson.Number(node, "cacheWrite")
                }
            };

            return true;
        }

        private static string Serialize(AgentUsageEntry entry)
        {
            var sb = new StringBuilder(200);
            sb.Append("{\"runId\":\"").Append(entry.RunId).Append('"');
            sb.Append(",\"provider\":\"").Append(entry.Provider).Append('"');
            sb.Append(",\"at\":\"")
              .Append(entry.FinishedAtUtc.ToString("o", CultureInfo.InvariantCulture)).Append('"');
            sb.Append(",\"in\":").Append(entry.Usage.InputTokens.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"out\":").Append(entry.Usage.OutputTokens.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"cacheRead\":")
              .Append(entry.Usage.CacheReadTokens.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"cacheWrite\":")
              .Append(entry.Usage.CacheWriteTokens.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"cost\":").Append(entry.CostUsd.ToString("0.######", CultureInfo.InvariantCulture));
            sb.Append('}');
            return sb.ToString();
        }

        private static void Append(AgentUsageEntry entry)
        {
            try
            {
                Directory.CreateDirectory(AgentRunStore.Root);
                File.AppendAllText(FilePath, Serialize(entry) + "\n", new UTF8Encoding(false));
            }
            catch (Exception)
            {
                // Losing one line costs accuracy in the window, nothing more.
            }
        }

        private static void Rewrite()
        {
            try
            {
                var sb = new StringBuilder(Entries.Count * 200);
                foreach (AgentUsageEntry entry in Entries)
                    sb.Append(Serialize(entry)).Append('\n');

                Directory.CreateDirectory(AgentRunStore.Root);
                File.WriteAllText(FilePath, sb.ToString(), new UTF8Encoding(false));
            }
            catch (Exception)
            {
                // Pruning is housekeeping; failing it only leaves stale lines behind.
            }
        }
    }
}
