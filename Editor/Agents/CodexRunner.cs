using System.Collections.Generic;
using System.Text;

namespace OxenteGames.JiraCommunication.Agents
{
    /// <summary>
    /// Drives the OpenAI Codex CLI in non-interactive mode with JSON events.
    /// </summary>
    /// <remarks>
    /// <c>AgentRequest.AdditionalDirectories</c> is deliberately not emitted here.
    /// Codex has no <c>--add-dir</c>: its sandbox already lets a run read outside the
    /// working directory, and widening what it may <em>write</em> means overriding
    /// <c>sandbox_workspace_write.writable_roots</c> — a JSON array passed through
    /// <c>-c</c>, which would have to survive both cmd.exe quoting and a config key
    /// that belongs to another project's release cycle. So the grant is honored on the
    /// Claude side, where the flag exists, and an ai-jira run under Codex writes only
    /// inside the repository.
    /// <para>
    /// This runner exists to prove the seam holds for a second dialect, and its parser
    /// is deliberately more forgiving than the Claude one: it classifies the event
    /// shapes Codex is documented to emit and falls back to surfacing any text it can
    /// find rather than dropping the line. A CLI whose event names drift should
    /// degrade to a readable transcript, never to an empty one.
    /// </para>
    /// </remarks>
    internal sealed class CodexRunner : IAgentRunner
    {
        public string Provider => AgentProvider.Codex;

        // "codex exec" has no resume flag; continuing is a separate subcommand.
        public bool SupportsResume => false;

        public string BuildCommandLine(AgentRequest request)
        {
            var sb = new StringBuilder(256);
            sb.Append(AgentScript.Quote(Executable(request)));

            // "exec" is the non-interactive subcommand; "-" makes it read the prompt
            // from stdin, which the launcher redirects from prompt.txt.
            sb.Append(" exec --json");
            sb.Append(' ').Append(MapSandbox(request.PermissionMode));

            if (!string.IsNullOrWhiteSpace(request.Model))
                sb.Append(" --model ").Append(AgentScript.Quote(request.Model));

            // Codex resumes by session id through its own subcommand rather than a
            // flag on exec, so a follow-up is not expressible here yet. The UI hides
            // the continue action for this provider instead of emitting a wrong flag.
            sb.Append(" -");

            return sb.ToString();
        }

        public string BuildInteractiveCommandLine(AgentRequest request)
        {
            var sb = new StringBuilder(128);
            sb.Append(AgentScript.Quote(Executable(request)));

            if (!string.IsNullOrWhiteSpace(request.Model))
                sb.Append(" --model ").Append(AgentScript.Quote(request.Model));

            return sb.ToString();
        }

        private static string Executable(AgentRequest request)
        {
            return string.IsNullOrWhiteSpace(request.ExecutablePath)
                ? AgentCliLocator.CommandName(AgentProvider.Codex)
                : request.ExecutablePath;
        }

        /// <summary>
        /// Maps our posture onto Codex's sandbox flags.
        /// </summary>
        /// <remarks>
        /// Codex has no headless equivalent of "ask me before writing", so both
        /// <see cref="AgentPermission.Default"/> and <see cref="AgentPermission.Plan"/>
        /// map to a read-only sandbox. Choosing read-only for the ambiguous case keeps
        /// the safer behavior: a run that should have asked does not silently write.
        /// </remarks>
        private static string MapSandbox(string permissionMode)
        {
            return permissionMode == AgentPermission.AcceptEdits
                ? "--sandbox workspace-write"
                : "--sandbox read-only";
        }

        public AgentEvent ParseLine(string line)
        {
            object node = AgentJson.Parse(line);
            if (node == null)
                return null;

            string type = AgentJson.String(node, "type") ?? string.Empty;

            if (type.IndexOf("error", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return new AgentEvent
                {
                    Kind = AgentEventKind.Error,
                    Text = FirstText(node) ?? "error",
                    IsError = true
                };
            }

            // Terminal events for a turn or the whole thread.
            if (type == "turn.completed" || type == "thread.completed" || type == "turn.failed")
            {
                bool failed = type == "turn.failed";
                object usage = AgentJson.Field(node, "usage");

                return new AgentEvent
                {
                    Kind = AgentEventKind.Result,
                    Text = FirstText(node) ?? string.Empty,
                    Detail = type,
                    IsError = failed,
                    CostUsd = AgentJson.Number(usage, "total_cost_usd"),
                    Usage = ParseUsage(usage)
                };
            }

            if (type == "thread.started" || type == "session.created")
            {
                return new AgentEvent
                {
                    Kind = AgentEventKind.Started,
                    Text = AgentJson.String(node, "model") ?? string.Empty,
                    Detail = AgentJson.String(node, "thread_id")
                             ?? AgentJson.String(node, "session_id")
                             ?? string.Empty
                };
            }

            // item.started / item.completed carry the actual work.
            object item = AgentJson.Field(node, "item");
            if (item != null)
                return ParseItem(item, type);

            string fallback = FirstText(node);
            return string.IsNullOrWhiteSpace(fallback)
                ? (string.IsNullOrWhiteSpace(type) ? null : AgentEvent.Simple(AgentEventKind.Unknown, type))
                : AgentEvent.Simple(AgentEventKind.Text, fallback);
        }

        /// <summary>
        /// Token counters from a turn's usage block. Codex names cached input
        /// differently from Claude Code, which is the whole reason each runner maps
        /// its own dialect instead of the store guessing at field names.
        /// </summary>
        private static AgentUsage ParseUsage(object usage)
        {
            if (usage == null)
                return default(AgentUsage);

            return new AgentUsage
            {
                InputTokens = (long)AgentJson.Number(usage, "input_tokens"),
                OutputTokens = (long)AgentJson.Number(usage, "output_tokens"),
                CacheReadTokens = (long)AgentJson.Number(usage, "cached_input_tokens")
            };
        }

        private static AgentEvent ParseItem(object item, string envelopeType)
        {
            string itemType = AgentJson.String(item, "type") ?? string.Empty;

            // Only report tools once, when they start; the completion adds no detail
            // the transcript needs.
            if (itemType == "command_execution" || itemType == "file_change" || itemType == "mcp_tool_call")
            {
                if (envelopeType == "item.started")
                {
                    return AgentEvent.Simple(
                        AgentEventKind.ToolUse,
                        itemType,
                        Truncate(AgentJson.String(item, "command")
                                 ?? AgentJson.String(item, "path")
                                 ?? AgentJson.String(item, "tool")
                                 ?? string.Empty));
                }

                string status = AgentJson.String(item, "status") ?? string.Empty;
                bool isError = status.IndexOf("fail", System.StringComparison.OrdinalIgnoreCase) >= 0;

                return new AgentEvent
                {
                    Kind = AgentEventKind.ToolResult,
                    Text = isError ? "error" : "ok",
                    IsError = isError
                };
            }

            if (itemType == "reasoning")
            {
                string reasoning = FirstText(item);
                return string.IsNullOrWhiteSpace(reasoning)
                    ? null
                    : AgentEvent.Simple(AgentEventKind.Thinking, reasoning);
            }

            if (itemType == "agent_message" || itemType == "assistant_message")
            {
                // Wait for the completed form so a message is not shown twice.
                if (envelopeType == "item.started")
                    return null;

                string text = FirstText(item);
                return string.IsNullOrWhiteSpace(text)
                    ? null
                    : AgentEvent.Simple(AgentEventKind.Text, text);
            }

            string any = FirstText(item);
            return string.IsNullOrWhiteSpace(any)
                ? null
                : AgentEvent.Simple(AgentEventKind.Text, any);
        }

        /// <summary>
        /// Finds the most likely human-readable string on a node, checking the common
        /// field names and then a nested content array.
        /// </summary>
        private static string FirstText(object node)
        {
            string[] keys = { "text", "message", "delta", "content", "result", "summary" };

            foreach (string key in keys)
            {
                string value = AgentJson.String(node, key);
                if (!string.IsNullOrWhiteSpace(value))
                    return value.Trim();
            }

            List<object> content = AgentJson.List(node, "content");
            if (content == null)
                return null;

            foreach (object block in content)
            {
                if (block is string direct && !string.IsNullOrWhiteSpace(direct))
                    return direct.Trim();

                string text = AgentJson.String(block, "text");
                if (!string.IsNullOrWhiteSpace(text))
                    return text.Trim();
            }

            return null;
        }

        private static string Truncate(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            string single = value.Replace('\n', ' ').Replace('\r', ' ').Trim();
            return single.Length > 120 ? single.Substring(0, 120) + "..." : single;
        }
    }
}
