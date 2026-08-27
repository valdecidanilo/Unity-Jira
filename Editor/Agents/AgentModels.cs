using System;
using System.Collections.Generic;

namespace OxenteGames.JiraCommunication.Agents
{
    /// <summary>Which local agent CLI backs a run.</summary>
    /// <remarks>
    /// This choice is stored independently of the AI assistant's provider on purpose.
    /// The two features share nothing operationally: the assistant is an HTTP call
    /// billed per token against an API key, while the agent is a local CLI that
    /// authenticates with the developer's own login and consumes their plan. Deriving
    /// one from the other meant a developer whose assistant was set to ChatGPT had
    /// the agent tab silently hunting for the Codex CLI.
    /// </remarks>
    internal static class AgentProvider
    {
        public const string ClaudeCode = "claude-code";
        public const string Codex = "codex";

        /// <summary>Selectable providers, in display order.</summary>
        public static readonly string[] All = { ClaudeCode, Codex };

        public static string DisplayName(string provider)
        {
            return provider == Codex ? "Codex CLI" : "Claude Code";
        }

        /// <summary>Normalizes an unknown or empty stored value to the default.</summary>
        public static string Sanitize(string provider)
        {
            return provider == Codex ? Codex : ClaudeCode;
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

        /// <summary>Token counters reported with a terminal result event.</summary>
        public AgentUsage Usage;

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

    /// <summary>
    /// Token counters for one run, as reported by the CLI.
    /// </summary>
    /// <remarks>
    /// Cached reads are kept apart from fresh input because they are what a resumed
    /// session mostly consumes, and folding them into one number would make a cheap
    /// follow-up look as expensive as the run that built the context.
    /// </remarks>
    internal struct AgentUsage
    {
        public long InputTokens;
        public long OutputTokens;
        public long CacheReadTokens;
        public long CacheWriteTokens;

        /// <summary>Everything the run moved through the model, cache included.</summary>
        public long Total => InputTokens + OutputTokens + CacheReadTokens + CacheWriteTokens;

        public bool HasData => Total > 0;

        public void Add(AgentUsage other)
        {
            InputTokens += other.InputTokens;
            OutputTokens += other.OutputTokens;
            CacheReadTokens += other.CacheReadTokens;
            CacheWriteTokens += other.CacheWriteTokens;
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
        /// What the developer typed, without the surrounding framing. Stored so the
        /// chat can show the message that produced a run instead of the whole prompt.
        /// </summary>
        public string Instruction = string.Empty;

        /// <summary>
        /// Conversation this run belongs to — the run id of the first turn. Empty on a
        /// fresh conversation, in which case the run adopts its own id.
        /// </summary>
        public string ThreadId = string.Empty;

        /// <summary>
        /// Permission posture passed to the CLI. Kept as a plain string because the
        /// accepted values belong to the CLI, not to us.
        /// </summary>
        public string PermissionMode = AgentPermission.Default;

        /// <summary>Resume an existing CLI session instead of starting fresh. Optional.</summary>
        public string ResumeSessionId = string.Empty;

        /// <summary>
        /// Tool patterns pre-approved for this run, in the CLI's own syntax, or empty.
        /// </summary>
        /// <remarks>
        /// Needed because a headless run cannot answer a permission prompt: whatever
        /// is not allowed up front is denied, silently, mid-task.
        /// </remarks>
        public string AllowedTools = string.Empty;

        /// <summary>
        /// Strip the variables that would move this run off the developer's plan and
        /// onto a billed API account. See <c>JiraPreferences.AgentPlanOnly</c>.
        /// </summary>
        public bool PlanOnly = true;

        /// <summary>
        /// CLI model id, or empty to leave the CLI's own configuration alone.
        /// See <see cref="AgentModelCatalog"/>.
        /// </summary>
        public string Model = AgentModelCatalog.CliDefault;
    }

    /// <summary>
    /// Models offered per provider, as CLI model identifiers.
    /// </summary>
    /// <remarks>
    /// The first entry is always <see cref="CliDefault"/> (empty), which means "do not
    /// pass --model at all". That has to be the default: the developer already
    /// configured a model in the CLI, and a package that silently overrides it would
    /// be changing behavior they did not ask us to change.
    /// <para>
    /// Codex intentionally offers only the default. Its model identifiers are not
    /// something this package should guess at — extending the array is a one-line
    /// change once they are known.
    /// </para>
    /// </remarks>
    internal static class AgentModelCatalog
    {
        /// <summary>Sentinel meaning "leave the CLI's own model configuration alone".</summary>
        public const string CliDefault = "";

        private static readonly string[] ClaudeModels =
        {
            CliDefault,
            "claude-opus-5",
            "claude-sonnet-5",
            "claude-haiku-4-5"
        };

        private static readonly string[] CodexModels = { CliDefault };

        public static string[] Ids(string provider)
        {
            return provider == AgentProvider.Codex ? CodexModels : ClaudeModels;
        }

        /// <summary>True when this provider offers a real choice beyond the default.</summary>
        public static bool HasChoices(string provider)
        {
            return Ids(provider).Length > 1;
        }

        /// <summary>Normalizes a stored value back to a known id, or to the default.</summary>
        public static string Sanitize(string provider, string model)
        {
            if (string.IsNullOrWhiteSpace(model))
                return CliDefault;

            foreach (string id in Ids(provider))
            {
                if (id == model)
                    return id;
            }

            // A model the catalog no longer lists: fall back rather than pass an id the
            // CLI would reject.
            return CliDefault;
        }
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

        /// <summary>The developer's own message for this turn, for the chat bubble.</summary>
        public string Instruction = string.Empty;

        /// <summary>
        /// Conversation this run belongs to. Every turn of a continued session shares
        /// the first run's id, which is what lets the chat show one thread instead of
        /// a list of unrelated runs.
        /// </summary>
        public string ThreadId = string.Empty;

        /// <summary>
        /// CLI session id reported by the run, and the handle used to continue it.
        /// Empty until the CLI emits its init event.
        /// </summary>
        public string SessionId = string.Empty;

        /// <summary>Session this run continued from, when it was a follow-up.</summary>
        public string ResumedFrom = string.Empty;

        /// <summary>Model this run was pinned to, or empty for the CLI default.</summary>
        public string Model = string.Empty;

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

        /// <summary>Tokens this run reported. Zero until it produces a result event.</summary>
        public AgentUsage Usage;

        public bool IsRunning => Status == AgentRunStatus.Running;

        /// <summary>
        /// True when this run can be continued. Requires a session id, which only
        /// exists once the CLI reported one, and a run that is no longer live.
        /// </summary>
        public bool CanContinue => !IsRunning && !string.IsNullOrWhiteSpace(SessionId);

        public string DisplayTitle =>
            !string.IsNullOrWhiteSpace(Title) ? Title :
            !string.IsNullOrWhiteSpace(IssueKey) ? IssueKey : RunId;
    }
}
