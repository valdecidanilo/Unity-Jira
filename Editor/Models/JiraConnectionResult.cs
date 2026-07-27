namespace OxenteGames.JiraCommunication.Models
{
    internal sealed class JiraConnectionResult
    {
        public bool Success { get; }
        public long StatusCode { get; }
        public string Message { get; }
        public JiraUser User { get; }

        private JiraConnectionResult(bool success, long statusCode, string message, JiraUser user)
        {
            Success = success;
            StatusCode = statusCode;
            Message = message;
            User = user;
        }

        public static JiraConnectionResult Ok(JiraUser user)
        {
            return new JiraConnectionResult(true, 200, "Conexão realizada com sucesso.", user);
        }

        public static JiraConnectionResult Fail(long statusCode, string message)
        {
            return new JiraConnectionResult(false, statusCode, message, null);
        }
    }
}
