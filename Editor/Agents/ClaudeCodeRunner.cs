using System.Collections.Generic;
using System.Text;

namespace OxenteGames.JiraCommunication.Agents
{
    /// <summary>
    /// Drives the Claude Code CLI in headless print mode with a JSON event stream.
    /// </summary>
    /// <remarks>
    /// Notably this path handles no API key. The CLI authenticates with whatever
    /// account the developer already logged in with, so the package never stores or
    /// transmits an agent credential — unlike the HTTP issue-drafting path, which
    /// needs a key in <c>EditorPrefs</c>.
    /// </remarks>
    internal sealed class ClaudeCodeRunner : IAgentRunner
    {
        public string Provider => AgentProvider.ClaudeCode;

        public string BuildCommandLine(AgentRequest request)
        {
            var sb = new StringBuilder(256);
            sb.Append(AgentScript.Quote(Executable(request)));

            // -p is headless print mode; stream-json additionally requires --verbose,
            // which is what turns the single final answer into per-step events.
            sb.Append(" -p --output-format stream-json --verbose");
            sb.Append(" --permission-mode ").Append(MapPermission(request.PermissionMode));

            if (!string.IsNullOrWhiteSpace(request.ResumeSessionId))
                sb.Append(" --resume ").Append(AgentScript.Quote(request.ResumeSessionId));

            return sb.ToString();
        }

        public string BuildInteractiveCommandLine(AgentRequest request)
        {
            var sb = new StringBuilder(128);
            sb.Append(AgentScript.Quote(Executable(request)));

            if (!string.IsNullOrWhiteSpace(request.ResumeSessionId))
                sb.Append(" --resume ").Append(AgentScript.Quote(request.ResumeSessionId));

            return sb.ToString();
        }

        private static string Executable(AgentRequest request)
        {
            return string.IsNullOrWhiteSpace(request.ExecutablePath)
                ? AgentCliLocator.CommandName(AgentProvider.ClaudeCode)
                : request.ExecutablePath;
        }

        /// <summary>
        /// Translates our posture to the CLI's flag.
        /// </summary>
        /// <remarks>
        /// <c>bypassPermissions</c> is intentionally not reachable from the UI. A
        /// headless agent with every guardrail off, launched from a button in the
        /// Editor, is not a default anybody should be able to pick by accident.
        /// </remarks>
        private static string MapPermission(string permissionMode)
        {
            switch (permissionMode)
            {
                case AgentPermission.Plan: return "plan";
                case AgentPermission.AcceptEdits: return "acceptEdits";
                default: return "default";
            }
        }

        public AgentEvent ParseLine(string line)
        {
            object node = AgentJson.Parse(line);
            if (node == null)
                return null;

            string type = AgentJson.String(node, "type");

            switch (type)
            {
                case "system":
                    return ParseSystem(node);

                case "assistant":
                    return ParseAssistant(node);

                case "user":
                    return ParseToolResult(node);

                case "result":
                    return ParseResult(node);

                case "stream_event":
                    // Token-level deltas. Ignored: the console renders whole blocks.
                    return null;

                default:
                    return string.IsNullOrWhiteSpace(type)
                        ? null
                        : AgentEvent.Simple(AgentEventKind.Unknown, type);
            }
        }

        private static AgentEvent ParseSystem(object node)
        {
            string subtype = AgentJson.String(node, "subtype");
            if (subtype != "init")
                return null;

            string sessionId = AgentJson.String(node, "session_id") ?? string.Empty;
            string model = AgentJson.String(node, "model") ?? string.Empty;

            return new AgentEvent
            {
                Kind = AgentEventKind.Started,
                Text = model,
                Detail = sessionId
            };
        }

        private static AgentEvent ParseAssistant(object node)
        {
            object message = AgentJson.Field(node, "message");
            List<object> content = AgentJson.List(message, "content");
            if (content == null)
                return null;

            // One assistant message can carry several blocks. The first meaningful one
            // becomes the event; text is preferred so the user sees narration promptly.
            AgentEvent toolEvent = null;

            foreach (object block in content)
            {
                string blockType = AgentJson.String(block, "type");

                if (blockType == "text")
                {
                    string text = AgentJson.String(block, "text");
                    if (!string.IsNullOrWhiteSpace(text))
                        return AgentEvent.Simple(AgentEventKind.Text, text.Trim());
                }
                else if (blockType == "thinking")
                {
                    string thinking = AgentJson.String(block, "thinking");
                    if (!string.IsNullOrWhiteSpace(thinking))
                        return AgentEvent.Simple(AgentEventKind.Thinking, thinking.Trim());
                }
                else if (blockType == "tool_use" && toolEvent == null)
                {
                    string name = AgentJson.String(block, "name") ?? "tool";
                    toolEvent = AgentEvent.Simple(
                        AgentEventKind.ToolUse, name, SummarizeToolInput(AgentJson.Field(block, "input")));
                }
            }

            return toolEvent;
        }

        private static AgentEvent ParseToolResult(object node)
        {
            object message = AgentJson.Field(node, "message");
            List<object> content = AgentJson.List(message, "content");
            if (content == null)
                return null;

            foreach (object block in content)
            {
                if (AgentJson.String(block, "type") != "tool_result")
                    continue;

                bool isError = AgentJson.Bool(block, "is_error");
                return new AgentEvent
                {
                    Kind = AgentEventKind.ToolResult,
                    Text = isError ? "error" : "ok",
                    IsError = isError
                };
            }

            return null;
        }

        private static AgentEvent ParseResult(object node)
        {
            bool isError = AgentJson.Bool(node, "is_error");
            string subtype = AgentJson.String(node, "subtype") ?? string.Empty;
            string text = AgentJson.String(node, "result") ?? string.Empty;

            return new AgentEvent
            {
                Kind = AgentEventKind.Result,
                Text = text.Trim(),
                Detail = subtype,
                IsError = isError || subtype == "error_max_turns" || subtype == "error_during_execution",
                DurationMs = AgentJson.Number(node, "duration_ms"),
                CostUsd = AgentJson.Number(node, "total_cost_usd")
            };
        }

        /// <summary>
        /// Renders a tool's free-form input as a short, single-line hint. Tool inputs
        /// can be large (a whole file body), so this only surfaces the fields that
        /// identify the target and always truncates.
        /// </summary>
        private static string SummarizeToolInput(object input)
        {
            if (input == null)
                return string.Empty;

            string[] interesting = { "file_path", "path", "pattern", "command", "url", "description", "prompt" };

            foreach (string key in interesting)
            {
                string value = AgentJson.String(input, key);
                if (string.IsNullOrWhiteSpace(value))
                    continue;

                value = value.Replace('\n', ' ').Replace('\r', ' ').Trim();
                return value.Length > 120 ? value.Substring(0, 120) + "..." : value;
            }

            return string.Empty;
        }
    }
}
