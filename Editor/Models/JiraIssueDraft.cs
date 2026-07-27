using System.Text;

namespace OxenteGames.JiraCommunication.Models
{
    /// <summary>
    /// Builds the JSON body for POST /rest/api/3/issue.
    /// Description is converted to Atlassian Document Format (ADF), required by API v3.
    /// </summary>
    internal sealed class JiraIssueDraft
    {
        public string ProjectKey;
        public string IssueTypeId;
        public string Summary;
        public string Description;

        // Parent issue key. Used for a subtask's parent, or as the epic parent
        // on team-managed (next-gen) projects.
        public string ParentKey;

        public string ToJson()
        {
            var sb = new StringBuilder(256);
            sb.Append("{\"fields\":{");
            sb.Append("\"project\":{\"key\":\"").Append(Escape(ProjectKey)).Append("\"},");
            sb.Append("\"issuetype\":{\"id\":\"").Append(Escape(IssueTypeId)).Append("\"},");
            sb.Append("\"summary\":\"").Append(Escape(Summary)).Append('"');

            if (!string.IsNullOrWhiteSpace(ParentKey))
            {
                sb.Append(",\"parent\":{\"key\":\"").Append(Escape(ParentKey.Trim())).Append("\"}");
            }

            if (!string.IsNullOrWhiteSpace(Description))
            {
                sb.Append(",\"description\":").Append(BuildAdf(Description));
            }

            sb.Append("}}");
            return sb.ToString();
        }

        private static string BuildAdf(string text)
        {
            var sb = new StringBuilder(text.Length + 64);
            sb.Append("{\"type\":\"doc\",\"version\":1,\"content\":[");

            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                if (i > 0)
                    sb.Append(',');

                sb.Append("{\"type\":\"paragraph\"");
                if (lines[i].Length > 0)
                {
                    sb.Append(",\"content\":[{\"type\":\"text\",\"text\":\"")
                      .Append(Escape(lines[i]))
                      .Append("\"}]");
                }
                sb.Append('}');
            }

            sb.Append("]}");
            return sb.ToString();
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            var sb = new StringBuilder(value.Length + 8);
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20)
                            sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else
                            sb.Append(c);
                        break;
                }
            }

            return sb.ToString();
        }
    }
}
