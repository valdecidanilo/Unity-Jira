using System;
using System.Collections.Generic;

namespace OxenteGames.JiraCommunication.Agents
{
    /// <summary>Which local agent CLI backs a run.</summary>
    internal static class AgentProvider
    {
        public const string ClaudeCode = "claude-code";
        public const string Codex = "codex";

        /// <summary>Maps the AI provider stored in preferences to its agent CLI.</summary>
        public static string FromAiProvider(string aiProvider)
        {
            return aiProvider == Settings.JiraPreferences.ProviderOpenAi ? Codex : ClaudeCode;
        }

        public static string DisplayName(string provider)
        {
            return provider == Codex ? "Codex CLI" : "Claude Code";
        }
    }

    /// <summary>
    /// Normalized event kinds. Every runner translates its CLI dialect into these so
    /// the console and the store never depend on a specific provider's wire format.
    /// </summary>
    internal enum AgentEventKind
    {
        /// <summary>Session started; carries the session id when the CLI reports one.</summary>
        Started,

        /// <summary>Model reasoning / narration text.</summary>
        Thinking,

        /// <summary>Assistant text intended for the user.</summary>
        Text,

        /// <summary>The agent invoked a tool.</summary>
        ToolUse,

        /// <summary>A tool returned.</summary>
        ToolResult,

        /// <summary>Terminal event carrying the final answer and cost/duration.</summary>
        Result,

        /// <summary>Transport or CLI level failure.</summary>
        Error,

        /// <summary>A line we could parse as JSON but do not model. Kept for debugging.</summary>
        Unknown
    }

    /// <summary>One normalized entry in a run transcript.</summary>
    internal sealed class AgentEvent
    {
        public AgentEventKind Kind;

        /// <summary>Primary human-readable payload (text, tool name, error message).</summary>
        public string Text = string.Empty;

        /// <summary>Secondary detail: tool arguments summary, result subtype, session id.</summary>
        public string Detail = string.Empty;

        /// <summary>Wall-clock, from the CLI when available.</summary>
        public double DurationMs;

        /// <summary>Reported cost in USD, when the CLI provides it.</summary>
        public double CostUsd;

        /// <summary>True when a <see cref="AgentEventKind.Result"/> ended in failure.</summary>
        public bool IsError;

        public static AgentEvent Simple(AgentEventKind kind, string text, string detail = null)
        {
            return new AgentEvent
            {
                Kind = kind,
                Text = text ?? string.Empty,
                Detail = detail ?? string.Empty
            };
        }
    }

    /// <summary>Lifecycle of a run, derived from the run directory on disk.</summary>
    internal enum AgentRunStatus
    {
        Running,
        Succeeded,
        Failed,
        Canceled,

        /// <summary>Process is gone but no exit marker was written (Unity or the OS killed it).</summary>
        Orphaned
    }

    /// <summary>What the caller asks the agent to do.</summary>
    internal sealed class AgentRequest
    {
        /// <summary>The full instruction handed to the agent over stdin.</summary>
        public string Prompt = string.Empty;

        /// <summary>Working directory. Defaults to the repository root.</summary>
        public string WorkingDirectory = string.Empty;

        /// <summary>Agent CLI to drive. See <see cref="AgentProvider"/>.</summary>
        public string Provider = AgentProvider.ClaudeCode;

        /// <summary>Absolute path to the CLI executable, resolved by <see cref="AgentCliLocator"/>.</summary>
        public string ExecutablePath = string.Empty;

        /// <summary>Jira issue this run is about, for labelling and history. Optional.</summary>
        public string IssueKey = string.Empty;

        /// <summary>Short label shown in the run list.</summary>
        public string Title = string.Empty;

        /// <summary>
        /// Permission posture passed to the CLI. Kept as a plain string because the
        /// accepted values belong to the CLI, not to us.
        /// </summary>
        public string PermissionMode = AgentPermission.Default;

        /// <summary>Resume an existing CLI session instead of starting fresh. Optional.</summary>
        public string ResumeSessionId = string.Empty;
    }

    /// <summary>
    /// Permission postures we expose. These are passed through to the CLI verbatim,
    /// so the safe default stays the CLI's own default rather than something we invent.
    /// </summary>
    internal static class AgentPermission
    {
        /// <summary>CLI default: the agent may read freely and asks before writing.</summary>
        public const string Default = "default";

        /// <summary>Read-only investigation. Nothing on disk changes.</summary>
        public const string Plan = "plan";

        /// <summary>Unattended edits. Required for a headless run that must change files.</summary>
        public const string AcceptEdits = "acceptEdits";

        public static readonly string[] All = { Default, Plan, AcceptEdits };
    }

    /// <summary>
    /// Metadata for one run, reconstructed from its directory. This is the unit that
    /// survives a domain reload: nothing about a run lives only in memory.
    /// </summary>
    internal sealed class AgentRunInfo
    {
        public string RunId = string.Empty;
        public string Directory = string.Empty;
        public string Provider = AgentProvider.ClaudeCode;
        public string Title = string.Empty;
        public string IssueKey = string.Empty;
        public string SessionId = string.Empty;
        public DateTime StartedAtUtc;
        public int ProcessId;
        public AgentRunStatus Status = AgentRunStatus.Running;
        public int ExitCode;

        /// <summary>Byte offset already consumed from the stream file by the tailer.</summary>
        public long StreamOffset;

        /// <summary>Normalized transcript, appended as the stream is tailed.</summary>
        public readonly List<AgentEvent> Events = new List<AgentEvent>();

        /// <summary>Final answer text from the terminal result event.</summary>
        public string FinalText = string.Empty;

        public double DurationMs;
        public double CostUsd;

        public bool IsRunning => Status == AgentRunStatus.Running;

        public string DisplayTitle =>
            !string.IsNullOrWhiteSpace(Title) ? Title :
            !string.IsNullOrWhiteSpace(IssueKey) ? IssueKey : RunId;
    }
}
