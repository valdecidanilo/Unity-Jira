using System.Collections.Generic;
using System.Text;

namespace OxenteGames.JiraCommunication.Models
{
    /// <summary>
    /// Builds Atlassian Document Format (ADF) comment bodies, with optional @mentions.
    /// </summary>
    internal static class JiraAdf
    {
        public static string BuildCommentBody(string text, IReadOnlyList<JiraUser> mentions)
        {
            var sb = new StringBuilder(256);
            sb.Append("{\"type\":\"doc\",\"version\":1,\"content\":[");

            bool wroteNode = false;

            string safeText = string.IsNullOrEmpty(text) ? string.Empty : text;
            string[] lines = safeText.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

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
    }
}
