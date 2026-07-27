namespace OxenteGames.JiraCommunication.Models
{
    internal sealed class JiraCreateIssueResult
    {
        public bool Success { get; }
        public string IssueId { get; }
        public string IssueKey { get; }
        public string Message { get; }

        private JiraCreateIssueResult(bool success, string issueId, string issueKey, string message)
        {
            Success = success;
            IssueId = issueId;
            IssueKey = issueKey;
            Message = message;
        }

        public static JiraCreateIssueResult Ok(string issueId, string issueKey)
        {
            return new JiraCreateIssueResult(true, issueId, issueKey, $"Issue {issueKey} criada com sucesso.");
        }

        public static JiraCreateIssueResult Fail(string message)
        {
            return new JiraCreateIssueResult(false, null, null, message);
        }
    }
}
