using System.Collections.Generic;
using System.Text;

namespace OxenteGames.JiraCommunication.Models
{
    /// <summary>
    /// Builds Atlassian Document Format (ADF) comment bodies, with optional @mentions.
    /// </summary>
    internal static class JiraAdf
    {
        public static string BuildTextDocument(string text)
        {
            return BuildCommentBody(text, null);
        }

        public static string BuildTextDocumentWithImages(
            string text,
            IReadOnlyList<JiraAttachmentInfo> attachments)
        {
            var sb = new StringBuilder(384);
            sb.Append("{\"type\":\"doc\",\"version\":1,\"content\":[");
            AppendTextParagraphs(sb, text, out bool wroteNode);

            if (attachments != null)
            {
                foreach (JiraAttachmentInfo attachment in attachments)
                {
                    if (attachment == null ||
                        !attachment.IsImage ||
                        string.IsNullOrWhiteSpace(attachment.content))
                    {
                        continue;
                    }

                    if (wroteNode)
                        sb.Append(',');

                    sb.Append(
                        "{\"type\":\"mediaSingle\",\"attrs\":{\"layout\":\"center\"},\"content\":[{\"type\":\"media\",\"attrs\":{\"type\":\"external\",\"url\":\"")
                      .Append(JiraIssueDraft.JsonEscape(attachment.content))
                      .Append('"');
                    if (!string.IsNullOrWhiteSpace(attachment.filename))
                    {
                        sb.Append(",\"alt\":\"")
                          .Append(JiraIssueDraft.JsonEscape(
                              attachment.filename))
                          .Append('"');
                    }
                    sb.Append("}}]}");
                    wroteNode = true;
                }
            }

            sb.Append("]}");
            return sb.ToString();
        }

        public static string ExtractPlainText(JiraAdfNode document)
        {
            if (document == null)
                return string.Empty;

            var sb = new StringBuilder();
            AppendPlainText(document, sb);
            return sb.ToString().TrimEnd();
        }

        private static void AppendPlainText(JiraAdfNode node, StringBuilder sb)
        {
            if (node == null)
                return;

            if (string.Equals(node.type, "text", System.StringComparison.OrdinalIgnoreCase))
                sb.Append(node.text);
            else if (string.Equals(node.type, "mention", System.StringComparison.OrdinalIgnoreCase))
                sb.Append(node.attrs?.text);
            else if (string.Equals(node.type, "hardBreak", System.StringComparison.OrdinalIgnoreCase))
                sb.Append('\n');

            if (node.content != null)
            {
                foreach (JiraAdfNode child in node.content)
                    AppendPlainText(child, sb);
            }

            if (string.Equals(node.type, "paragraph", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(node.type, "heading", System.StringComparison.OrdinalIgnoreCase))
            {
                if (sb.Length > 0 && sb[sb.Length - 1] != '\n')
                    sb.Append('\n');
            }
        }

        public static string BuildCommentBody(string text, IReadOnlyList<JiraUser> mentions)
        {
            var sb = new StringBuilder(256);
            sb.Append("{\"type\":\"doc\",\"version\":1,\"content\":[");

            AppendTextParagraphs(sb, text, out bool wroteNode);

            if (mentions != null && mentions.Count > 0)
            {
                if (wroteNode)
                    sb.Append(',');

                sb.Append("{\"type\":\"paragraph\",\"content\":[");
                sb.Append("{\"type\":\"text\",\"text\":\"cc: \"}");

                bool first = true;
                foreach (JiraUser user in mentions)
                {
                    if (user == null || string.IsNullOrWhiteSpace(user.accountId))
                        continue;

                    if (!first)
                        sb.Append(",{\"type\":\"text\",\"text\":\" \"}");
                    first = false;

                    string name = string.IsNullOrWhiteSpace(user.displayName) ? user.accountId : user.displayName;
                    sb.Append(",{\"type\":\"mention\",\"attrs\":{\"id\":\"")
                      .Append(JiraIssueDraft.JsonEscape(user.accountId))
                      .Append("\",\"text\":\"@")
                      .Append(JiraIssueDraft.JsonEscape(name))
                      .Append("\"}}");
                }

                sb.Append("]}");
            }

            sb.Append("]}");
            return sb.ToString();
        }

        private static void AppendTextParagraphs(
            StringBuilder sb,
            string text,
            out bool wroteNode)
        {
            wroteNode = false;
            string safeText = string.IsNullOrEmpty(text)
                ? string.Empty
                : text;
            string[] lines = safeText
                .Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Split('\n');

            foreach (string line in lines)
            {
                if (wroteNode)
                    sb.Append(',');

                sb.Append("{\"type\":\"paragraph\"");
                if (line.Length > 0)
                {
                    sb.Append(",\"content\":[{\"type\":\"text\",\"text\":\"")
                      .Append(JiraIssueDraft.JsonEscape(line))
                      .Append("\"}]");
                }
                sb.Append('}');
                wroteNode = true;
            }
        }
    }
}
